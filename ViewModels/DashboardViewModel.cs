using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Client.Commands;
using Client.Models;
using Client.Services;

namespace Client.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly ICameraService _cameraService;
        private readonly InMemoryLogService _logService;
        private readonly ISettingsService _settingsService;
        private readonly IYoloDetector _detector;
        private readonly ModelManagerService _modelManager;
        private Action? _navigateToSettings;
        private Action<DetectionResult, byte[]>? _navigateToResult;
        private CameraInfo _cameraInfo;
        private DetectionResult? _lastResult;
        private string _statusMessage;
        private bool _isCameraConnected;
        private bool _isDetecting;
        private bool _isShowingResult;
        private BitmapSource? _liveFrame;
        private BitmapSource? _displayFrame;
        private DbItem? _selectedItem;
        private bool _isSettingsMode;
        private string _cameraIp = string.Empty;
        private string _triggerMode = "Software";
        private int _captureWidth = 1920;
        private int _captureHeight = 1080;

        public DbItem? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public CameraInfo CameraInfo
        {
            get => _cameraInfo;
            set => SetProperty(ref _cameraInfo, value);
        }

        public DetectionResult? LastResult
        {
            get => _lastResult;
            set => SetProperty(ref _lastResult, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsCameraConnected
        {
            get => _isCameraConnected;
            set => SetProperty(ref _isCameraConnected, value);
        }

        public BitmapSource? DisplayFrame
        {
            get => _displayFrame;
            set => SetProperty(ref _displayFrame, value);
        }

        public bool IsShowingResult
        {
            get => _isShowingResult;
            set => SetProperty(ref _isShowingResult, value);
        }

        public bool IsDetecting
        {
            get => _isDetecting;
            set => SetProperty(ref _isDetecting, value);
        }

        public bool IsSettingsMode
        {
            get => _isSettingsMode;
            set => SetProperty(ref _isSettingsMode, value);
        }

        public string CameraIp
        {
            get => _cameraIp;
            set => SetProperty(ref _cameraIp, value);
        }

        public string TriggerMode
        {
            get => _triggerMode;
            set => SetProperty(ref _triggerMode, value);
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

        // ====== TEST IMAGE MODE CODE ======
        private byte[]? _testSelectedImageBytes;
        private bool _hasTestImage;
        public bool HasTestImage
        {
            get => _hasTestImage;
            set => SetProperty(ref _hasTestImage, value);
        }
        public ICommand SelectTestImageCommand { get; }
        public ICommand ClearTestImageCommand { get; }
        // ==================================

        public ObservableCollection<string> Logs => _logService.Entries;

        public ICommand ConnectCameraCommand { get; }
        public ICommand DisconnectCameraCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand ToggleSettingsCommand { get; }
        public ICommand CancelSettingsCommand { get; }

        public DashboardViewModel(IYoloDetector detector, ModelManagerService modelManager, ISettingsService settingsService)
        {
            _logService = InMemoryLogService.Instance;
            _cameraService = new CameraService();
            _cameraService.FrameCaptured += OnFrameCaptured;
            _detector = detector;
            _modelManager = modelManager;
            _settingsService = settingsService;
            
            _cameraInfo = new CameraInfo();
            _statusMessage = "Ready";
            _isCameraConnected = false;
            _isDetecting = false;
            _isShowingResult = false;
            _liveFrame = null;
            _displayFrame = null;

            ConnectCameraCommand = new RelayCommand(async _ => await ConnectCamera(), _ => !IsCameraConnected && !IsSettingsMode);
            DisconnectCameraCommand = new RelayCommand(async _ => await DisconnectCamera(), _ => IsCameraConnected && !IsSettingsMode);
            // ====== TEST IMAGE MODE CODE ======
            StartCommand = new RelayCommand(async _ => await Start(), _ => (IsCameraConnected || HasTestImage) && !IsDetecting && !IsSettingsMode);
            SelectTestImageCommand = new RelayCommand(_ => SelectTestImage());
            ClearTestImageCommand = new RelayCommand(_ => ClearTestImage());
            // ==================================
            ToggleSettingsCommand = new RelayCommand(_ => ToggleSettings());
            CancelSettingsCommand = new RelayCommand(_ => CancelSettings());

            _logService.LogInfo("Dashboard started");
        }

        private async Task ConnectCamera()
        {
            StatusMessage = "Connecting to camera...";
            _logService.LogInfo("Connecting to camera...");
            
            bool success = await _cameraService.ConnectAsync();
            
            if (success)
            {
                IsCameraConnected = true;
                CameraInfo.IsConnected = true;
                CameraInfo.Status = "Connected";
                CameraInfo.DeviceName = "Camera / Simulator";
                StatusMessage = "Camera connected successfully";
                _logService.LogInfo("Camera connected successfully");
            }
            else
            {
                IsCameraConnected = false;
                StatusMessage = "Failed to connect camera";
                _logService.LogError("Failed to connect camera");
            }
        }

        private async Task DisconnectCamera()
        {
            StatusMessage = "Disconnecting camera...";
            _logService.LogInfo("Disconnecting camera...");
            
            await _cameraService.DisconnectAsync();
            
            IsCameraConnected = false;
            CameraInfo.IsConnected = false;
            CameraInfo.Status = "Disconnected";
            _liveFrame = null;
            DisplayFrame = null;
            IsShowingResult = false;
            StatusMessage = "Camera disconnected";
            _logService.LogInfo("Camera disconnected");
        }

        private async Task Start()
        {
            if (IsDetecting) return;

            if (SelectedItem == null)
            {
                StatusMessage = "Please select a material sample first";
                _logService.LogError("Start failed: No material sample selected.");
                return;
            }

            IsDetecting = true;
            StatusMessage = "Starting local detection...";
            _logService.LogInfo("Start local detection");

            try
            {
                // ====== TEST IMAGE MODE CODE ======
                byte[]? imageData = _testSelectedImageBytes ?? await _cameraService.CaptureAsync();
                // ==================================
                if (imageData == null)
                {
                    StatusMessage = "Failed to capture image";
                    _logService.LogError("Failed to capture image");
                    return;
                }

                var settings = _settingsService.LoadSettings();
                float confThreshold = (float)settings.AiModels.ConfidenceThreshold;
                float nmsThreshold = (float)settings.AiModels.NmsThreshold;
                int targetClassCode = SelectedItem.ClassCode;

                StatusMessage = "Running local AI inference...";
                _logService.LogInfo($"Inference on model: {System.IO.Path.GetFileName(_detector.CurrentModelPath)} targetClass={targetClassCode} conf={confThreshold}");

                // Chạy AI local trên luồng phụ để tránh block UI
                var localResult = await Task.Run(() => _detector.Detect(imageData, confThreshold, nmsThreshold, targetClassCode));

                if (!localResult.Result.IsSuccess)
                {
                    StatusMessage = $"Inference failed: {localResult.Result.ErrorMessage}";
                    _logService.LogError(StatusMessage);
                    return;
                }

                string? rawPath = null;
                string? resultPath = null;

                // Tự động lưu ảnh gốc và kết quả detect
                try
                {
                    bool shouldSave = settings.Image.AutoSave;
                    if (shouldSave && settings.Image.SaveMode == "NGOnly")
                    {
                        // NG (Not Good) means count is 0 (no items of selected class code detected)
                        shouldSave = localResult.Result.Count == 0;
                    }

                    if (shouldSave)
                    {
                        var saveDir = settings.Image.SaveDirectory;
                        if (string.IsNullOrWhiteSpace(saveDir))
                        {
                            saveDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CapturedImages");
                        }
                        if (!System.IO.Path.IsPathRooted(saveDir))
                        {
                            saveDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, saveDir);
                        }

                        var rawFolder = System.IO.Path.Combine(saveDir, "Raw");
                        var resultFolder = System.IO.Path.Combine(saveDir, "Result");

                        if (!System.IO.Directory.Exists(rawFolder)) System.IO.Directory.CreateDirectory(rawFolder);
                        if (!System.IO.Directory.Exists(resultFolder)) System.IO.Directory.CreateDirectory(resultFolder);

                        var filename = $"{DateTime.Now:yyyyMMdd_HHmmssfff}.jpg";
                        var fullRawPath = System.IO.Path.Combine(rawFolder, filename);
                        var fullResultPath = System.IO.Path.Combine(resultFolder, filename);

                        System.IO.File.WriteAllBytes(fullRawPath, imageData);
                        if (localResult.AnnotatedImageBytes != null)
                        {
                            System.IO.File.WriteAllBytes(fullResultPath, localResult.AnnotatedImageBytes);
                        }

                        // Save relative paths to database
                        rawPath = $"\\Raw\\{filename}";
                        resultPath = $"\\Result\\{filename}";
                        localResult.Result.ImagePath = resultPath; // Keep compatibility

                        _logService.LogInfo($"Saved raw image: {fullRawPath} and result image: {fullResultPath}");
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Failed to save image: {ex.Message}");
                }

                // Lưu kết quả detect vào SQL Server
                StatusMessage = "Saving detection result to database...";
                var dbService = new DatabaseService(_settingsService);
                await dbService.SaveDetectionResultAsync(localResult.Result, SelectedItem, rawPath, resultPath);

                LastResult = localResult.Result;
                StatusMessage = $"Detected: {localResult.Result.Count} objects | {localResult.Result.ProcessingTimeMs:0.##} ms";
                _logService.LogInfo(StatusMessage);

                _navigateToResult?.Invoke(localResult.Result, localResult.AnnotatedImageBytes ?? Array.Empty<byte>());
            }
            catch (Exception ex)
            {
                StatusMessage = $"Local AI process failed: {ex.Message}";
                _logService.LogError("Local AI process failed", ex);
            }
            finally
            {
                IsDetecting = false;
            }
        }

        private void OnFrameCaptured(object? sender, BitmapSource frame)
        {
            _liveFrame = frame;
            // ====== TEST IMAGE MODE CODE ======
            if (HasTestImage) return;
            // ==================================
            if (!IsShowingResult)
            {
                DisplayFrame = frame;
            }
        }



        private void ToggleSettings()
        {
            if (!IsSettingsMode)
            {
                StatusMessage = "Loading camera settings...";
                try
                {
                    var settings = _settingsService.LoadSettings();
                    CameraIp = settings.Camera.IpAddress;
                    TriggerMode = settings.Camera.TriggerMode;
                    CaptureWidth = settings.Camera.CaptureWidth;
                    CaptureHeight = settings.Camera.CaptureHeight;
                    StatusMessage = "Editing camera settings";
                    _logService.LogInfo("Entered Camera Settings mode");
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to load camera settings: {ex.Message}";
                    _logService.LogError($"Failed to load camera settings: {ex.Message}");
                }
                IsSettingsMode = true;
            }
            else
            {
                StatusMessage = "Saving camera settings...";
                try
                {
                    var settings = _settingsService.LoadSettings();
                    if (settings.Camera == null)
                    {
                        settings.Camera = new CameraSettings();
                    }
                    settings.Camera.IpAddress = CameraIp;
                    settings.Camera.TriggerMode = TriggerMode;
                    settings.Camera.CaptureWidth = CaptureWidth;
                    settings.Camera.CaptureHeight = CaptureHeight;

                    _settingsService.SaveSettings(settings);
                    StatusMessage = "Camera settings saved successfully!";
                    _logService.LogInfo("Camera settings saved successfully!");
                    IsSettingsMode = false;
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to save camera settings: {ex.Message}";
                    _logService.LogError($"Failed to save camera settings: {ex.Message}");
                }
            }
        }

        private void CancelSettings()
        {
            IsSettingsMode = false;
            StatusMessage = "Editing cancelled";
            _logService.LogInfo("Cancelled camera settings editing");
        }

        // ====== TEST IMAGE MODE CODE ======
        private void SelectTestImage()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All Files (*.*)|*.*",
                Title = "Select Test Image"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(openFileDialog.FileName);
                    var bitmap = ByteArrayToBitmapSource(bytes);
                    if (bitmap != null)
                    {
                        _testSelectedImageBytes = bytes;
                        HasTestImage = true;
                        DisplayFrame = bitmap;
                        StatusMessage = $"Loaded test image: {System.IO.Path.GetFileName(openFileDialog.FileName)}";
                        _logService.LogInfo($"Loaded test image from file: {openFileDialog.FileName}");
                    }
                    else
                    {
                        StatusMessage = "Failed to load the selected image file.";
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error loading test image: {ex.Message}";
                    _logService.LogError($"Error loading test image: {ex.Message}");
                }
            }
        }

        private void ClearTestImage()
        {
            _testSelectedImageBytes = null;
            HasTestImage = false;
            DisplayFrame = _liveFrame; // Revert to live frame if available
            StatusMessage = "Cleared test image";
            _logService.LogInfo("Cleared test image");
        }
        // ==================================

        public void SetNavigateToSettings(Action navigateToSettings)
        {
            _navigateToSettings = navigateToSettings;
        }

        public void SetNavigateToResult(Action<DetectionResult, byte[]> navigateToResult)
        {
            _navigateToResult = navigateToResult;
        }

        private BitmapSource? ByteArrayToBitmapSource(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                using (var stream = new System.IO.MemoryStream(bytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
