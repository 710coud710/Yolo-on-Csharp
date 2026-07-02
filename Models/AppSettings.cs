namespace Client.Models
{
    public class AppSettings
    {
        public AiModelSettings AiModels { get; set; }
        public CameraSettings Camera { get; set; }
        public ImageSettings Image { get; set; }
        public GeneralSettings General { get; set; }
        public List<int> SelectedClassIds { get; set; }
        public List<SelectedMaterialClassInfo> SelectedClasses { get; set; }

        public AppSettings()
        {
            AiModels = new AiModelSettings();
            Camera = new CameraSettings();
            Image = new ImageSettings();
            General = new GeneralSettings();
            SelectedClassIds = new List<int>();
            SelectedClasses = new List<SelectedMaterialClassInfo>();
            DatabaseConnectionString = string.Empty;
        }

        public string DatabaseConnectionString { get; set; }
    }

    public class GeneralSettings
    {
        public string ModelProcess { get; set; } // "Capture" or "Stream"
        public bool AllClass { get; set; }
        public double RoiX { get; set; }
        public double RoiY { get; set; }
        public double RoiWidth { get; set; }
        public double RoiHeight { get; set; }

        public GeneralSettings()
        {
            ModelProcess = "Capture";
            AllClass = false;
            RoiX = 0;
            RoiY = 0;
            RoiWidth = 100;
            RoiHeight = 100;
        }
    }

    public class SelectedMaterialClassInfo
    {
        public int LabelId { get; set; }
        public string Label { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }

        public SelectedMaterialClassInfo()
        {
            Label = string.Empty;
            MaterialCode = string.Empty;
            MaterialName = string.Empty;
        }
    }

    public class AiModelSettings
    {
        public string ModelsDirectory { get; set; }
        public double ConfidenceThreshold { get; set; }
        public double NmsThreshold { get; set; }
        public bool UseGpu { get; set; }

        public AiModelSettings()
        {
            ModelsDirectory = "Models";
            ConfidenceThreshold = 0.25;
            NmsThreshold = 0.45;
            UseGpu = false;
        }
    }

    public class CameraSettings
    {
        public string IpAddress { get; set; }
        public string TriggerMode { get; set; }
        public int CaptureWidth { get; set; }
        public int CaptureHeight { get; set; }
        public int Fps { get; set; }
        public bool TestMode { get; set; }
        
        public CameraSettings()
        {
            IpAddress = string.Empty;
            TriggerMode = string.Empty;
            CaptureWidth = 1920;
            CaptureHeight = 1080;
            Fps = 30;
            TestMode = false;
        }
    }

    public class ImageSettings
    {
        public bool AutoSave { get; set; }
        public string SaveDirectory { get; set; }
        public int Quality { get; set; }
        public string SaveMode { get; set; }
        
        public ImageSettings()
        {
            SaveDirectory = "CapturedImages";
            SaveMode = "All";
        }
    }
}
