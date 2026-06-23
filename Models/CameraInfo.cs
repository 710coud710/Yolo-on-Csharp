namespace Client.Models
{
    public class CameraInfo
    {
        public string? DeviceName { get; set; }
        public string? SerialNumber { get; set; }
        public bool IsConnected { get; set; }
        public string Status { get; set; }

        public CameraInfo()
        {
            Status = "Disconnected";
        }
    }
}
