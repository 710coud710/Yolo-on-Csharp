using System;
using System.IO;
using System.Windows.Input;
using Client.Commands;
using Client.Constants;
using Client.Models;
using Client.Services;

namespace Client.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly ModelManagerService _modelManager;
        
        private string _modelsDirectory = "Models";
        private double _confidenceThreshold = 0.25;
        private double _nmsThreshold = 0.45;
        private bool _useGpu = false;
        
        private string _cameraIp = string.Empty;
        private int _captureWidth = 1920;
        private int _captureHeight = 1080;
        private string _triggerMode = "Software";
        
        private bool _autoSave = true;
        private string _saveDirectory = "CapturedImages";
        private int _imageQuality = 90;
        private string _saveMode = "NGOnly";
        
        private string _databaseConnectionString = string.Empty;
        private bool? _testConnectionOk = null;

        private string _statusMessage = "Ready";
        private string _settingsFilePath = string.Empty;

        public string ModelsDirectory
        {
            get => _modelsDirectory;
            set => SetProperty(ref _modelsDirectory, value);
        }

        public double ConfidenceThreshold
        {
            get => _confidenceThreshold;
            set => SetProperty(ref _confidenceThreshold, value);
        }

        public double NmsThreshold
        {
            get => _nmsThreshold;
            set => SetProperty(ref _nmsThreshold, value);
        }

        public bool UseGpu
        {
            get => _useGpu;
            set => SetProperty(ref _useGpu, value);
        }

        public string SettingsFilePath
        {
            get => _settingsFilePath;
            set => SetProperty(ref _settingsFilePath, value);
        }

        public string CameraIp
        {
            get => _cameraIp;
            set => SetProperty(ref _cameraIp, value);
        }

        public int CaptureWidth
        {
            get => _captureWidth;
            set => SetProperty(ref _captureWidth, value);
        }

        public int CaptureHeight
        {
            get => _captureHeight;
            set => SetProperty(ref _captureHeight, value);
        }

        public string TriggerMode
        {
            get => _triggerMode;
            set => SetProperty(ref _triggerMode, value);
        }

        public bool AutoSave
        {
            get => _autoSave;
            set => SetProperty(ref _autoSave, value);
        }

        public string SaveDirectory
        {
            get => _saveDirectory;
            set => SetProperty(ref _saveDirectory, value);
        }

        public int ImageQuality
        {
            get => _imageQuality;
            set => SetProperty(ref _imageQuality, value);
        }

        public string DatabaseConnectionString
        {
            get => _databaseConnectionString;
            set => SetProperty(ref _databaseConnectionString, value);
        }

        public bool? TestConnectionOk
        {
            get => _testConnectionOk;
            set => SetProperty(ref _testConnectionOk, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string SaveMode
        {
            get => _saveMode;
            set => SetProperty(ref _saveMode, value);
        }

        public ICommand SaveSettingsCommand { get; }
        public ICommand ResetSettingsCommand { get; }
        public ICommand BrowseSaveDirectoryCommand { get; }
        public ICommand ScanModelsCommand { get; }
        public ICommand TestConnectionCommand { get; }

        public SettingsViewModel(ISettingsService settingsService, ModelManagerService modelManager)
        {
            _settingsService = settingsService;
            _modelManager = modelManager;
            
            LoadSettings();

            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
            ResetSettingsCommand = new RelayCommand(_ => ResetSettings());
            BrowseSaveDirectoryCommand = new RelayCommand(_ => BrowseSaveDirectory());
            ScanModelsCommand = new RelayCommand(_ => ScanModels());
            TestConnectionCommand = new RelayCommand(_ => TestConnection());

            _statusMessage = "Ready";
            _settingsFilePath = _settingsService.GetSettingsFilePath();
        }

        private void LoadSettings()
        {
            try
            {
                var settings = _settingsService.LoadSettings();
                
                ModelsDirectory = settings.AiModels?.ModelsDirectory ?? DefaultSettings.ModelsDirectory;
                ConfidenceThreshold = settings.AiModels?.ConfidenceThreshold ?? DefaultSettings.ConfidenceThreshold;
                NmsThreshold = settings.AiModels?.NmsThreshold ?? DefaultSettings.NmsThreshold;
                UseGpu = settings.AiModels?.UseGpu ?? DefaultSettings.UseGpu;

                CameraIp = settings.Camera.IpAddress;
                CaptureWidth = settings.Camera.CaptureWidth;
                CaptureHeight = settings.Camera.CaptureHeight;
                TriggerMode = settings.Camera.TriggerMode;

                AutoSave = settings.Image.AutoSave;
                SaveDirectory = settings.Image.SaveDirectory;
                ImageQuality = settings.Image.Quality;
                SaveMode = settings.Image.SaveMode ?? DefaultSettings.ImageSaveMode;
                DatabaseConnectionString = settings.DatabaseConnectionString ?? DefaultSettings.DatabaseConnectionString;

                StatusMessage = "Settings loaded successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load settings: {ex.Message}";
            }
        }

        private void SaveSettings()
        {
            StatusMessage = "Saving settings...";
            
            try
            {
                // Validate confidence
                if (ConfidenceThreshold < 0.0 || ConfidenceThreshold > 1.0)
                {
                    StatusMessage = "Error: Confidence Threshold must be between 0.0 and 1.0";
                    return;
                }

                var settings = new AppSettings
                {
                    AiModels = new AiModelSettings
                    {
                        ModelsDirectory = ModelsDirectory,
                        ConfidenceThreshold = ConfidenceThreshold,
                        NmsThreshold = NmsThreshold,
                        UseGpu = UseGpu
                    },
                    Camera = new CameraSettings
                    {
                        IpAddress = CameraIp,
                        TriggerMode = TriggerMode,
                        CaptureWidth = CaptureWidth,
                        CaptureHeight = CaptureHeight
                    },
                    Image = new ImageSettings
                    {
                        AutoSave = AutoSave,
                        SaveDirectory = SaveDirectory,
                        Quality = ImageQuality,
                        SaveMode = SaveMode
                    },
                    DatabaseConnectionString = DatabaseConnectionString
                };

                _settingsService.SaveSettings(settings);
                StatusMessage = "Settings saved successfully !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save settings: {ex.Message}";
            }
        }

        private void ResetSettings()
        {
            StatusMessage = "Resetting to default settings...";
            
            ModelsDirectory = DefaultSettings.ModelsDirectory;
            ConfidenceThreshold = DefaultSettings.ConfidenceThreshold;
            NmsThreshold = DefaultSettings.NmsThreshold;
            UseGpu = DefaultSettings.UseGpu;

            CameraIp = DefaultSettings.CameraIpAddress;
            CaptureWidth = DefaultSettings.CameraCaptureWidth;
            CaptureHeight = DefaultSettings.CameraCaptureHeight;
            TriggerMode = DefaultSettings.CameraTriggerMode;

            AutoSave = DefaultSettings.ImageAutoSave;
            SaveDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultSettings.ImageSaveDirectoryName);
            ImageQuality = DefaultSettings.ImageQuality;
            SaveMode = DefaultSettings.ImageSaveMode;
            DatabaseConnectionString = DefaultSettings.DatabaseConnectionString;
            TestConnectionOk = null;
            
            StatusMessage = "Settings reset to default";
        }

        private void ScanModels()
        {
            try
            {
                var models = _modelManager.GetAvailableModels();
                StatusMessage = $"Scanned directory. Found {models.Count} ONNX models.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Scan failed: {ex.Message}";
            }
        }

        private void BrowseSaveDirectory()
        {
            // Mở thư mục lưu ảnh cục bộ
            try
            {
                var fullPath = SaveDirectory;
                if (!Path.IsPathRooted(fullPath))
                {
                    fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fullPath);
                }
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }
                System.Diagnostics.Process.Start("explorer.exe", fullPath);
                StatusMessage = $"Opened save directory: {fullPath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Browse failed: {ex.Message}";
            }
        }

        private void TestConnection()
        {
            StatusMessage = "Testing SQL connection...";
            var dbService = new DatabaseService(_settingsService);
            if (dbService.TestConnection(DatabaseConnectionString, out string error))
            {
                TestConnectionOk = true;
                StatusMessage = "SQL connection successful!";
            }
            else
            {
                TestConnectionOk = false;
                StatusMessage = $"SQL connection failed: {error}";
            }
        }
    }
}
