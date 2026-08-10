using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Basler.Pylon;

namespace Client.Services
{
    public class CameraService : ICameraService
    {
        private VideoCapture? _videoCapture;
        private Camera? _baslerCamera;
        private Task? _captureTask;
        private CancellationTokenSource? _cts;
        private BitmapSource? _currentFrame;
        private byte[]? _lastFrameBytes;
        private byte[]? _baslerPixelBuffer;
        private byte[]? _rawFrameBuffer;
        private int _rawFrameWidth;
        private int _rawFrameHeight;
        private readonly object _frameLock = new object();

        public bool IsConnected { get; private set; }

        public event EventHandler<BitmapSource>? FrameCaptured;

        public async Task<bool> ConnectAsync(int width, int height, int fps)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var baslerCamera = new Camera();
                    _baslerCamera = baslerCamera;
                    baslerCamera.Open();

                    try
                    {
                        if (baslerCamera.Parameters[PLCamera.OffsetX].IsWritable)
                            baslerCamera.Parameters[PLCamera.OffsetX].SetValue(0);
                        if (baslerCamera.Parameters[PLCamera.OffsetY].IsWritable)
                            baslerCamera.Parameters[PLCamera.OffsetY].SetValue(0);
                        if (baslerCamera.Parameters[PLCamera.Width].IsWritable)
                            baslerCamera.Parameters[PLCamera.Width].SetValue(width);
                        if (baslerCamera.Parameters[PLCamera.Height].IsWritable)
                            baslerCamera.Parameters[PLCamera.Height].SetValue(height);
                        if (baslerCamera.Parameters[PLCamera.AcquisitionFrameRateEnable].IsWritable)
                            baslerCamera.Parameters[PLCamera.AcquisitionFrameRateEnable].SetValue(true);
                        if (baslerCamera.Parameters[PLCamera.AcquisitionFrameRate].IsWritable)
                            baslerCamera.Parameters[PLCamera.AcquisitionFrameRate].SetValue((double)fps);
                    }
                    catch
                    {
                        // Ignore
                    }

                    try
                    {
                        if (baslerCamera.Parameters[PLCameraInstance.MaxNumBuffer].IsWritable)
                            baslerCamera.Parameters[PLCameraInstance.MaxNumBuffer].SetValue(10);
                    }
                    catch
                    {
                        // Ignore
                    }

                    baslerCamera.StreamGrabber?.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByUser);

                    var cts = new CancellationTokenSource();
                    _cts = cts;
                    _captureTask = Task.Factory.StartNew(
                        () => CaptureLoop(cts.Token),
                        cts.Token,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default);

                    IsConnected = true;
                    return true;
                }
                catch
                {
                    if (_baslerCamera != null)
                    {
                        try { _baslerCamera.Close(); } catch {}
                        try { _baslerCamera.Dispose(); } catch {}
                        _baslerCamera = null;
                    }
                }

                try
                {
                    _videoCapture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
                    
                    if (!_videoCapture.IsOpened())
                    {
                        _videoCapture.Dispose();
                        _videoCapture = null;
                        return false;
                    }

                    _videoCapture.Set(VideoCaptureProperties.FourCC, OpenCvSharp.FourCC.FromString("MJPG"));
                    _videoCapture.Set(VideoCaptureProperties.FrameWidth, width);
                    _videoCapture.Set(VideoCaptureProperties.FrameHeight, height);
                    _videoCapture.Set(VideoCaptureProperties.Fps, fps);

                    var cts = new CancellationTokenSource();
                    _cts = cts;
                    _captureTask = Task.Factory.StartNew(
                        () => CaptureLoop(cts.Token),
                        cts.Token,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default);

                    IsConnected = true;
                    return true;
                }
                catch
                {
                    IsConnected = false;
                    if (_videoCapture != null)
                    {
                        _videoCapture.Dispose();
                        _videoCapture = null;
                    }
                    return false;
                }
            });
        }

        private void CaptureLoop(CancellationToken token)
        {
            if (_baslerCamera != null)
            {
                BaslerCaptureLoop(token);
                return;
            }

            using (var mat = new Mat())
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (_videoCapture == null || _videoCapture.IsDisposed || !_videoCapture.IsOpened())
                        {
                            break;
                        }

                        // Đọc frame thô từ camera
                        if (_videoCapture.Read(mat))
                        {
                            if (!mat.Empty())
                            {
                                // Chuyển đổi sang BitmapSource cho WPF
                                var bitmapSource = BitmapSourceConverter.ToBitmapSource(mat);
                                bitmapSource.Freeze();
                                _currentFrame = bitmapSource;

                                // Lưu mảng byte thô để nén JPEG ON-DEMAND trong CaptureAsync
                                int bufferSize = mat.Width * mat.Height * mat.Channels();
                                lock (_frameLock)
                                {
                                    if (_rawFrameBuffer == null || _rawFrameBuffer.Length != bufferSize)
                                    {
                                        _rawFrameBuffer = new byte[bufferSize];
                                    }
                                    System.Runtime.InteropServices.Marshal.Copy(mat.Data, _rawFrameBuffer, 0, bufferSize);
                                    _rawFrameWidth = mat.Width;
                                    _rawFrameHeight = mat.Height;
                                }

                                // Kích hoạt sự kiện cập nhật hình ảnh lên giao diện
                                FrameCaptured?.Invoke(this, bitmapSource);
                            }
                        }
                    }
                    catch
                    {
                        // Bỏ qua lỗi bắt hình để luồng tiếp tục chạy
                    }

                    // Trễ 5ms để tránh chiếm dụng CPU quá mức
                    Thread.Sleep(5);
                }
            }
        }

        private void BaslerCaptureLoop(CancellationToken token)
        {
            var baslerCamera = _baslerCamera;
            if (baslerCamera == null) return;

            PixelDataConverter converter = new PixelDataConverter();
            converter.OutputPixelFormat = PixelType.BGR8packed;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!baslerCamera.IsOpen)
                    {
                        break;
                    }

                    using (IGrabResult? grabResult = baslerCamera.StreamGrabber!.RetrieveResult(1000, TimeoutHandling.ThrowException))
                    {
                        if (grabResult != null && grabResult.GrabSucceeded)
                        {
                            int width = grabResult.Width;
                            int height = grabResult.Height;
                            int bufferSize = width * height * 3;

                            // Khởi tạo/cập nhật mảng byte dùng lại (0 allocations)
                            if (_baslerPixelBuffer == null || _baslerPixelBuffer.Length != bufferSize)
                            {
                                _baslerPixelBuffer = new byte[bufferSize];
                            }

                            converter.Convert(_baslerPixelBuffer, grabResult);

                            // Chuyển đổi sang BitmapSource cho WPF
                            var bitmapSource = BitmapSource.Create(
                                width,
                                height,
                                96,
                                96,
                                PixelFormats.Bgr24,
                                null,
                                _baslerPixelBuffer,
                                width * 3);

                            bitmapSource.Freeze();
                            _currentFrame = bitmapSource;

                            // Lưu mảng byte thô để nén JPEG ON-DEMAND trong CaptureAsync
                            lock (_frameLock)
                            {
                                if (_rawFrameBuffer == null || _rawFrameBuffer.Length != bufferSize)
                                {
                                    _rawFrameBuffer = new byte[bufferSize];
                                }
                                Buffer.BlockCopy(_baslerPixelBuffer, 0, _rawFrameBuffer, 0, bufferSize);
                                _rawFrameWidth = width;
                                _rawFrameHeight = height;
                            }

                            // Kích hoạt sự kiện cập nhật hình ảnh lên giao diện
                            FrameCaptured?.Invoke(this, bitmapSource);
                        }
                    }
                }
                catch
                {
                    // Bỏ qua lỗi bắt hình để luồng tiếp tục chạy
                }

                // Trễ 5ms để tránh chiếm dụng CPU quá mức
                Thread.Sleep(5);
            }
        }

        public async Task DisconnectAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    IsConnected = false;

                    // 1. Hủy luồng bắt hình nền
                    if (_cts != null)
                    {
                        _cts.Cancel();
                        try
                        {
                            _captureTask?.Wait(2000); // Đợi tối đa 2s
                        }
                        catch
                        {
                            // Bỏ qua lỗi dừng task
                        }
                        _cts.Dispose();
                        _cts = null;
                    }

                    // 2. Giải phóng Basler Camera
                    var baslerCamera = _baslerCamera;
                    if (baslerCamera != null)
                    {
                        try
                        {
                            if (baslerCamera.StreamGrabber != null && baslerCamera.StreamGrabber.IsGrabbing)
                            {
                                baslerCamera.StreamGrabber.Stop();
                            }
                        }
                        catch {}
                        try
                        {
                            if (baslerCamera.IsOpen)
                            {
                                baslerCamera.Close();
                            }
                        }
                        catch {}
                        try
                        {
                            baslerCamera.Dispose();
                        }
                        catch {}
                        _baslerCamera = null;
                    }

                    // 3. Giải phóng OpenCV VideoCapture
                    if (_videoCapture != null)
                    {
                        if (!_videoCapture.IsDisposed)
                        {
                            _videoCapture.Release();
                            _videoCapture.Dispose();
                        }
                        _videoCapture = null;
                    }

                    _captureTask = null;
                    _currentFrame = null;
                    lock (_frameLock)
                    {
                        _lastFrameBytes = null;
                        _rawFrameBuffer = null;
                        _baslerPixelBuffer = null;
                        _rawFrameWidth = 0;
                        _rawFrameHeight = 0;
                    }
                }
                catch
                {
                    // Bỏ qua lỗi ngắt kết nối
                }
            });
        }

        public async Task<byte[]?> CaptureAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    byte[]? rawBytes = null;
                    int width = 0;
                    int height = 0;

                    lock (_frameLock)
                    {
                        if (_rawFrameBuffer != null)
                        {
                            rawBytes = new byte[_rawFrameBuffer.Length];
                            Buffer.BlockCopy(_rawFrameBuffer, 0, rawBytes, 0, _rawFrameBuffer.Length);
                            width = _rawFrameWidth;
                            height = _rawFrameHeight;
                        }
                        else if (_lastFrameBytes != null)
                        {
                            // Trả về bản sao mảng byte đã được lưu trong luồng nền (dự phòng)
                            var result = new byte[_lastFrameBytes.Length];
                            Buffer.BlockCopy(_lastFrameBytes, 0, result, 0, _lastFrameBytes.Length);
                            return result;
                        }
                    }

                    if (rawBytes != null && width > 0 && height > 0)
                    {
                        using (Mat mat = new Mat(height, width, MatType.CV_8UC3, rawBytes))
                        {
                            byte[] jpegBytes;
                            if (Cv2.ImEncode(".jpg", mat, out jpegBytes))
                            {
                                return jpegBytes;
                            }
                        }
                    }

                    if (_currentFrame == null)
                        return null;

                    // Mã hóa dự phòng từ BitmapSource nếu dữ liệu byte Mat chưa sẵn sàng
                    var encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(_currentFrame));
                    
                    using (var memoryStream = new MemoryStream())
                    {
                        encoder.Save(memoryStream);
                        return memoryStream.ToArray();
                    }
                }
                catch
                {
                    return null;
                }
            });
        }

        public List<CameraResolutionOption> GetSupportedResolutions()
        {
            // Trả về danh sách các độ phân giải tiêu chuẩn được hỗ trợ bởi đại đa số các webcam USB
            var list = new List<CameraResolutionOption>
            {
                new CameraResolutionOption { Width = 3840, Height = 2160, Fps = 30 }, // 4K
                new CameraResolutionOption { Width = 2560, Height = 1440, Fps = 30 }, // 2K
                new CameraResolutionOption { Width = 1920, Height = 1080, Fps = 60 }, // Full HD @ 60 FPS
                new CameraResolutionOption { Width = 1920, Height = 1080, Fps = 30 }, // Full HD @ 30 FPS
                new CameraResolutionOption { Width = 1280, Height = 720, Fps = 60 },  // HD @ 60 FPS
                new CameraResolutionOption { Width = 1280, Height = 720, Fps = 30 },  // HD @ 30 FPS
                new CameraResolutionOption { Width = 640, Height = 480, Fps = 30 }    // SD
            };

            return list
                .GroupBy(x => new { x.Width, x.Height, x.Fps })
                .Select(g => g.First())
                .OrderByDescending(x => x.Width * x.Height)
                .ThenByDescending(x => x.Fps)
                .ToList();
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
        }
    }
}
