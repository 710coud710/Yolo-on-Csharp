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

namespace Client.Services
{
    public class CameraService : ICameraService
    {
        private VideoCapture? _videoCapture;
        private Task? _captureTask;
        private CancellationTokenSource? _cts;
        private BitmapSource? _currentFrame;
        private byte[]? _lastFrameBytes;
        private readonly object _frameLock = new object();

        public bool IsConnected { get; private set; }

        public event EventHandler<BitmapSource>? FrameCaptured;

        public async Task<bool> ConnectAsync(int width, int height, int fps)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 1. Khởi tạo VideoCapture của OpenCV sử dụng backend DirectShow (Index 0), MDSMF, DSHOW
                    _videoCapture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
                    
                    if (!_videoCapture.IsOpened())
                    {
                        _videoCapture.Dispose();
                        _videoCapture = null;
                        return false;
                    }

                    // 2. Thiết lập định dạng nén MJPEG và Resolution & FPS mong muốn
                    // Cần thiết lập MJPEG (FourCC) trước khi đặt độ phân giải cao (như 4K 3840x2160)
                    // để tránh nghẽn băng thông USB dẫn đến khởi tạo chậm hoặc lỗi.
                    _videoCapture.Set(VideoCaptureProperties.FourCC, OpenCvSharp.FourCC.FromString("MJPG"));
                    _videoCapture.Set(VideoCaptureProperties.FrameWidth, width);
                    _videoCapture.Set(VideoCaptureProperties.FrameHeight, height);
                    _videoCapture.Set(VideoCaptureProperties.Fps, fps);

                    // 3. Khởi chạy vòng lặp bắt hình nền
                    _cts = new CancellationTokenSource();
                    _captureTask = Task.Factory.StartNew(
                        () => CaptureLoop(_cts.Token),
                        _cts.Token,
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

                                // Lưu byte ảnh JPEG trực tiếp từ Mat để tối ưu hóa CaptureAsync
                                byte[] jpegBytes;
                                if (Cv2.ImEncode(".jpg", mat, out jpegBytes))
                                {
                                    lock (_frameLock)
                                    {
                                        _lastFrameBytes = jpegBytes;
                                    }
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

                    // 2. Giải phóng OpenCV VideoCapture
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
                    lock (_frameLock)
                    {
                        if (_lastFrameBytes != null)
                        {
                            // Trả về bản sao mảng byte đã được lưu trong luồng nền
                            var result = new byte[_lastFrameBytes.Length];
                            Buffer.BlockCopy(_lastFrameBytes, 0, result, 0, _lastFrameBytes.Length);
                            return result;
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
