using System.Windows.Media.Imaging;

namespace Client.Services
{
    public interface ICameraService
    {
        Task<bool> ConnectAsync();
        Task DisconnectAsync();
        Task<byte[]?> CaptureAsync();
        bool IsConnected { get; }
        event EventHandler<BitmapSource>? FrameCaptured;
    }
}
