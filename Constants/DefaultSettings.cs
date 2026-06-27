namespace Client.Constants
{
    /// <summary>
    /// Chứa tất cả các giá trị mặc định cho settings
    /// Có thể dễ dàng điều chỉnh tại đây thay vì hardcode trong code
    /// </summary>
    public static class DefaultSettings
    {
        // AI Model Settings
        public const string ModelsDirectory = "Models";
        public const double ConfidenceThreshold = 0.25;
        public const double NmsThreshold = 0.45;
        public const bool UseGpu = false;

        // Camera Settings
        public const string CameraIpAddress = "192.168.1.100";
        public const string CameraTriggerMode = "Software";
        public const int CameraCaptureWidth = 1920;
        public const int CameraCaptureHeight = 1080;
        public const int CameraFps = 30;
        public const bool CameraTestMode = false;

        // Image Settings
        public const bool ImageAutoSave = true;
        public const int ImageQuality = 90;
        public const string ImageSaveDirectoryName = "CapturedImages";
        public const string ImageSaveMode = "All";

        // Database Settings
        // public const string DatabaseConnectionString = "Data Source=192.168.7.103;Persist Security Info=True;User ID=qa_web;Password=Adv@1234!;Pooling=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Application Name=\"SQL Server Management Studio\";Command Timeout=0";
        public const string DatabaseConnectionString =
        "Data Source=192.168.7.103;" +
        "Persist Security Info=True;" +
        "User ID=qa_web;" +
        "Password=Adv@1234!;" +
        "Initial Catalog=VisionCounter;" +
        "Pooling=False;" +
        "MultipleActiveResultSets=False;" +
        "Encrypt=False;" +
        "TrustServerCertificate=True;" +
        "Application Name=\"SQL Server Management Studio\";" +
        "Command Timeout=0";
    }
}
