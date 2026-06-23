using System.Windows.Media.Imaging;
using System.Windows.Media;
using AForge.Video;
using AForge.Video.DirectShow;

namespace Client.Services
{
    public class CameraService : ICameraService
    {
        private VideoCaptureDevice? _videoSource;
        private BitmapSource? _currentFrame;

        public bool IsConnected { get; private set; }

        public event EventHandler<BitmapSource>? FrameCaptured;

        public async Task<bool> ConnectAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // ============================================
                    // WEBCAM CONNECTION (Temporary)
                    // ============================================
                    var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                    
                    if (videoDevices.Count == 0)
                    {
                        return false;
                    }

                    _videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
                    _videoSource.NewFrame += VideoSource_NewFrame;
                    _videoSource.Start();

                    IsConnected = true;
                    return true;

                    // ============================================
                    // HIKROBOT GIGE CAMERA (TODO: Implement later)
                    // ============================================
                    // using MvCameraControl;
                    // 
                    // // Initialize MVS SDK
                    // int nRet = MyCamera.MV_CC_Initialize();
                    // if (nRet != MyCamera.MV_OK)
                    // {
                    //     return false;
                    // }
                    //
                    // // Enumerate devices
                    // MyCamera.MV_CC_DEVICE_INFO_LIST stDevList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
                    // nRet = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE, ref stDevList);
                    // if (nRet != MyCamera.MV_OK || stDevList.nDeviceNum == 0)
                    // {
                    //     return false;
                    // }
                    //
                    // // Create handle
                    // MyCamera.MV_CC_DEVICE_INFO stDevInfo = 
                    //     (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(
                    //         stDevList.pDeviceInfo[0], 
                    //         typeof(MyCamera.MV_CC_DEVICE_INFO));
                    //
                    // IntPtr m_hCamera = IntPtr.Zero;
                    // nRet = MyCamera.MV_CC_CreateHandle_NET(ref m_hCamera, ref stDevInfo);
                    // if (nRet != MyCamera.MV_OK)
                    // {
                    //     return false;
                    // }
                    //
                    // // Open device
                    // nRet = MyCamera.MV_CC_OpenDevice_NET(m_hCamera);
                    // if (nRet != MyCamera.MV_OK)
                    // {
                    //     MyCamera.MV_CC_DestroyHandle_NET(m_hCamera);
                    //     return false;
                    // }
                    //
                    // // Set trigger mode to Software
                    // nRet = MyCamera.MV_CC_SetEnumValue_NET(m_hCamera, "TriggerMode", 1);
                    // if (nRet != MyCamera.MV_OK)
                    // {
                    //     MyCamera.MV_CC_CloseDevice_NET(m_hCamera);
                    //     MyCamera.MV_CC_DestroyHandle_NET(m_hCamera);
                    //     return false;
                    // }
                    //
                    // // Start grabbing
                    // nRet = MyCamera.MV_CC_StartGrabbing_NET(m_hCamera);
                    // if (nRet != MyCamera.MV_OK)
                    // {
                    //     MyCamera.MV_CC_CloseDevice_NET(m_hCamera);
                    //     MyCamera.MV_CC_DestroyHandle_NET(m_hCamera);
                    //     return false;
                    // }
                    //
                    // IsConnected = true;
                    // return true;
                }
                catch
                {
                    IsConnected = false;
                    return false;
                }
            });
        }

        public async Task DisconnectAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // ============================================
                    // WEBCAM DISCONNECTION
                    // ============================================
                    if (_videoSource != null && _videoSource.IsRunning)
                    {
                        _videoSource.SignalToStop();
                        _videoSource.WaitForStop();
                        _videoSource.NewFrame -= VideoSource_NewFrame;
                        _videoSource = null;
                    }

                    // ============================================
                    // HIKROBOT GIGE CAMERA DISCONNECTION (TODO)
                    // ============================================
                    // if (m_hCamera != IntPtr.Zero)
                    // {
                    //     // Stop grabbing
                    //     MyCamera.MV_CC_StopGrabbing_NET(m_hCamera);
                    //     
                    //     // Close device
                    //     MyCamera.MV_CC_CloseDevice_NET(m_hCamera);
                    //     
                    //     // Destroy handle
                    //     MyCamera.MV_CC_DestroyHandle_NET(m_hCamera);
                    //     m_hCamera = IntPtr.Zero;
                    // }

                    IsConnected = false;
                }
                catch
                {
                    IsConnected = false;
                }
            });
        }

        public async Task<byte[]?> CaptureAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // ============================================
                    // WEBCAM CAPTURE
                    // ============================================
                    if (_currentFrame == null)
                        return null;

                    // Convert BitmapSource to byte array (JPEG)
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(_currentFrame));
                    
                    using (var memoryStream = new System.IO.MemoryStream())
                    {
                        encoder.Save(memoryStream);
                        return memoryStream.ToArray();
                    }

                    // ============================================
                    // HIKROBOT GIGE CAMERA CAPTURE (TODO)
                    // ============================================
                    // if (m_hCamera == IntPtr.Zero)
                    //     return null;
                    //
                    // // Software trigger
                    // int nRet = MyCamera.MV_CC_SetCommandValue_NET(m_hCamera, "TriggerSoftware");
                    // if (nRet != MyCamera.MV_OK)
                    // {
                    //     return null;
                    // }
                    //
                    // // Get one frame
                    // MyCamera.MV_FRAME_OUT stFrameOut = new MyCamera.MV_FRAME_OUT();
                    // nRet = MyCamera.MV_CC_GetImageBuffer_NET(m_hCamera, ref stFrameOut, 1000);
                    // if (nRet != MyCamera.MV_OK)
                    // {
                    //     return null;
                    // }
                    //
                    // // Convert to byte array
                    // byte[] imageData = new byte[stFrameOut.stFrameInfo.nWidth * stFrameOut.stFrameInfo.nHeight * 3];
                    // Marshal.Copy(stFrameOut.pBufAddr, imageData, 0, imageData.Length);
                    //
                    // // Free image buffer
                    // MyCamera.MV_CC_FreeImageBuffer_NET(m_hCamera, ref stFrameOut);
                    //
                    // return imageData;
                }
                catch
                {
                    return null;
                }
            });
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                // Convert System.Drawing.Bitmap to BitmapSource
                using (var bitmap = (System.Drawing.Bitmap)eventArgs.Frame.Clone())
                {
                    var bitmapData = bitmap.LockBits(
                        new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        bitmap.PixelFormat);

                    var bitmapSource = BitmapSource.Create(
                        bitmapData.Width,
                        bitmapData.Height,
                        96, 96,
                        PixelFormats.Bgr24,
                        null,
                        bitmapData.Scan0,
                        bitmapData.Stride * bitmapData.Height,
                        bitmapData.Stride);

                    bitmap.UnlockBits(bitmapData);

                    bitmapSource.Freeze();
                    _currentFrame = bitmapSource;

                    FrameCaptured?.Invoke(this, bitmapSource);
                }
            }
            catch
            {
                // Ignore frame errors
            }
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
        }
    }
}
