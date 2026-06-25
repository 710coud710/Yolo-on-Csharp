using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace Client.Services
{
    public class CameraResolutionOption
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Fps { get; set; }

        public string DisplayName => $"{Width}x{Height} @ {Fps} FPS";
    }

    public interface ICameraService
    {
        Task<bool> ConnectAsync(int width, int height, int fps);
        Task DisconnectAsync();
        Task<byte[]?> CaptureAsync();
        bool IsConnected { get; }
        event EventHandler<BitmapSource>? FrameCaptured;
        List<CameraResolutionOption> GetSupportedResolutions();
    }
}
