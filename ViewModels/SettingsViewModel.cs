using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        private readonly ICameraService _cameraService;
        
        private string _modelsDirectory = "Models";
        private double _confidenceThreshold = 0.25;
        private double _nmsThreshold = 0.45;
        private bool _useGpu = false;
        
        private string _cameraIp = string.Empty;
        private int _captureWidth = 1920;
        private int _captureHeight = 1080;
        private int _fps = 30;
        private bool _testMode;
        private string _triggerMode = "Software";
        
        private bool _autoSave = true;
        private string _saveDirectory = "CapturedImages";
        private int _imageQuality = 90;
        private string _saveMode = "All";
        
        private string _databaseConnectionString = string.Empty;
        private bool? _testConnectionOk = null;

        private bool _isUnlocked = false;
        private string _unlockPassword = string.Empty;
        private string _unlockErrorMessage = string.Empty;
        private bool _hasUnlockError = false;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _currentPassword = string.Empty;
        private bool _isChangePasswordVisible = false;

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

        public int Fps
        {
            get => _fps;
            set => SetProperty(ref _fps, value);
        }

        private ObservableCollection<CameraResolutionOption> _supportedResolutions = new();
        public ObservableCollection<CameraResolutionOption> SupportedResolutions
        {
            get => _supportedResolutions;
            set => SetProperty(ref _supportedResolutions, value);
        }

        private CameraResolutionOption? _selectedResolution;
        public CameraResolutionOption? SelectedResolution
        {
            get => _selectedResolution;
            set
            {
                if (SetProperty(ref _selectedResolution, value) && value != null)
                {
                    CaptureWidth = value.Width;
                    CaptureHeight = value.Height;
                    Fps = value.Fps;
                }
            }
        }

        public string TriggerMode
        {
            get => _triggerMode;
            set => SetProperty(ref _triggerMode, value);
        }

        public bool TestMode
        {
            get => _testMode;
            set => SetProperty(ref _testMode, value);
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

        public bool IsUnlocked
        {
            get => _isUnlocked;
            set => SetProperty(ref _isUnlocked, value);
        }

        public string UnlockPassword
        {
            get => _unlockPassword;
            set => SetProperty(ref _unlockPassword, value);
        }

        public string UnlockErrorMessage
        {
            get => _unlockErrorMessage;
            set => SetProperty(ref _unlockErrorMessage, value);
        }

        public bool HasUnlockError
        {
            get => _hasUnlockError;
            set => SetProperty(ref _hasUnlockError, value);
        }

        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public string CurrentPassword
        {
            get => _currentPassword;
            set => SetProperty(ref _currentPassword, value);
        }

        public bool IsChangePasswordVisible
        {
            get => _isChangePasswordVisible;
            set => SetProperty(ref _isChangePasswordVisible, value);
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
        public ICommand UnlockCommand { get; }
        public ICommand ToggleChangePasswordCommand { get; }

        public SettingsViewModel(ISettingsService settingsService, ModelManagerService modelManager, ICameraService cameraService)
        {
            _settingsService = settingsService;
            _modelManager = modelManager;
            _cameraService = cameraService;
            
            LoadSettings();

            SaveSettingsCommand = new RelayCommand(async _ => await SaveSettingsAsync());
            ResetSettingsCommand = new RelayCommand(_ => ResetSettings());
            BrowseSaveDirectoryCommand = new RelayCommand(_ => BrowseSaveDirectory());
            ScanModelsCommand = new RelayCommand(_ => ScanModels());
            TestConnectionCommand = new RelayCommand(_ => TestConnection());
            UnlockCommand = new RelayCommand(async _ => await UnlockAsync());
            ToggleChangePasswordCommand = new RelayCommand(_ => IsChangePasswordVisible = !IsChangePasswordVisible);

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
                Fps = settings.Camera.Fps;
                TestMode = settings.Camera.TestMode;
                TriggerMode = settings.Camera.TriggerMode;

                if (_cameraService != null)
                {
                    SupportedResolutions = new ObservableCollection<CameraResolutionOption>(_cameraService.GetSupportedResolutions());
                    SelectedResolution = SupportedResolutions.FirstOrDefault(r => r.Width == CaptureWidth && r.Height == CaptureHeight && r.Fps == Fps)
                                         ?? SupportedResolutions.FirstOrDefault(r => r.Width == CaptureWidth && r.Height == CaptureHeight)
                                         ?? SupportedResolutions.FirstOrDefault();
                }

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

        private async Task SaveSettingsAsync()
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

                // 1. Change password if requested
                string hostname = Environment.MachineName;
                var dbService = new DatabaseService(_settingsService);
                bool dbConnected = true;

                if (IsChangePasswordVisible && !string.IsNullOrEmpty(NewPassword))
                {
                    bool currentPasswordVerified = false;
                    try
                    {
                        currentPasswordVerified = await dbService.VerifyMachinePasswordAsync(hostname, CurrentPassword);
                    }
                    catch (Exception)
                    {
                        // Fallback offline verification
                        currentPasswordVerified = string.Equals(CurrentPassword, "admin");
                    }

                    if (!currentPasswordVerified)
                    {
                        StatusMessage = "Error: Current machine password is incorrect.";
                        return;
                    }

                    if (!string.Equals(NewPassword, ConfirmPassword))
                    {
                        StatusMessage = "Error: New password and confirmation do not match.";
                        return;
                    }

                    try
                    {
                        bool pwUpdated = await dbService.UpdateMachinePasswordAsync(hostname, NewPassword);
                        if (!pwUpdated)
                        {
                            StatusMessage = "Error: Failed to update password in database.";
                            return;
                        }
                    }
                    catch (Exception)
                    {
                        dbConnected = false;
                        StatusMessage = "Warning: Cannot update password in database because database is offline.";
                    }
                }

                // 2. Save local app settings config file
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
                        CaptureHeight = CaptureHeight,
                        Fps = Fps,
                        TestMode = TestMode
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

                // Clear password fields upon success
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
                IsChangePasswordVisible = false;

                if (dbConnected)
                {
                    StatusMessage = "Settings saved successfully!";
                }
                else
                {
                    StatusMessage = "Warning: Saved locally, but database is offline!";
                }
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
            Fps = DefaultSettings.CameraFps;
            TestMode = DefaultSettings.CameraTestMode;
            TriggerMode = DefaultSettings.CameraTriggerMode;

            if (SupportedResolutions != null)
            {
                SelectedResolution = SupportedResolutions.FirstOrDefault(r => r.Width == CaptureWidth && r.Height == CaptureHeight && r.Fps == Fps)
                                     ?? SupportedResolutions.FirstOrDefault(r => r.Width == CaptureWidth && r.Height == CaptureHeight)
                                     ?? SupportedResolutions.FirstOrDefault();
            }

            AutoSave = DefaultSettings.ImageAutoSave;
            SaveDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultSettings.ImageSaveDirectoryName);
            ImageQuality = DefaultSettings.ImageQuality;
            SaveMode = DefaultSettings.ImageSaveMode;
            DatabaseConnectionString = DefaultSettings.DatabaseConnectionString;
            TestConnectionOk = null;

            StatusMessage = "Settings reset to default";
        }

        public async Task UnlockAsync()
        {
            if (string.IsNullOrEmpty(UnlockPassword))
            {
                UnlockErrorMessage = "Password cannot be empty.";
                HasUnlockError = true;
                return;
            }

            try
            {
                string hostname = Environment.MachineName;
                var dbService = new DatabaseService(_settingsService);
                bool passwordVerified = false;

                try
                {
                    passwordVerified = await dbService.VerifyMachinePasswordAsync(hostname, UnlockPassword);
                }
                catch (Exception)
                {
                    // Fallback to local default password check
                    passwordVerified = string.Equals(UnlockPassword, "admin");
                }

                if (passwordVerified)
                {
                    IsUnlocked = true;
                    UnlockPassword = string.Empty;
                    UnlockErrorMessage = string.Empty;
                    HasUnlockError = false;
                    StatusMessage = "Settings unlocked successfully.";
                }
                else
                {
                    IsUnlocked = false;
                    UnlockErrorMessage = "Incorrect machine password.";
                    HasUnlockError = true;
                }
            }
            catch (Exception ex)
            {
                UnlockErrorMessage = $"Error: {ex.Message}";
                HasUnlockError = true;
            }
        }

        public void ResetLock()
        {
            IsUnlocked = false;
            UnlockPassword = string.Empty;
            UnlockErrorMessage = string.Empty;
            HasUnlockError = false;
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            IsChangePasswordVisible = false;
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
            try
            {
                var initialDir = SaveDirectory;
                if (!string.IsNullOrWhiteSpace(initialDir) && !Path.IsPathRooted(initialDir))
                {
                    initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, initialDir);
                }

                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Image Save Directory",
                    InitialDirectory = Directory.Exists(initialDir) ? initialDir : AppDomain.CurrentDomain.BaseDirectory
                };

                if (dialog.ShowDialog() == true)
                {
                    SaveDirectory = dialog.FolderName;
                    StatusMessage = $"Selected save directory: {SaveDirectory}";
                }
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
