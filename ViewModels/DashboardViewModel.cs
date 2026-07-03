using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using System.Linq;
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
        private bool _isStreamingDetectionActive;
        private int _liveStreamCount;
        private string _modelProcess = "Capture";
        private bool _allClass = false;
        private double _roiX = 0;
        private double _roiY = 0;
        private double _roiWidth = 100;
        private double _roiHeight = 100;

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
            set
            {
                if (SetProperty(ref _isCameraConnected, value))
                {
                    OnPropertyChanged(nameof(IsRoiOverlayVisible));
                }
            }
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

        public bool IsStreamingDetectionActive
        {
            get => _isStreamingDetectionActive;
            set
            {
                if (SetProperty(ref _isStreamingDetectionActive, value))
                {
                    OnPropertyChanged(nameof(IsRoiOverlayVisible));
                }
            }
        }

        public bool IsRoiOverlayVisible => IsCameraConnected && !IsStreamingDetectionActive;

        public int LiveStreamCount
        {
            get => _liveStreamCount;
            set => SetProperty(ref _liveStreamCount, value);
        }

        public string ModelProcess
        {
            get => _modelProcess;
            set => SetProperty(ref _modelProcess, value);
        }

        public bool AllClass
        {
            get => _allClass;
            set => SetProperty(ref _allClass, value);
        }

        public double RoiX
        {
            get => _roiX;
            set
            {
                if (SetProperty(ref _roiX, value))
                {
                    OnPropertyChanged(nameof(RoiLeftStar));
                    OnPropertyChanged(nameof(RoiRightStar));
                }
            }
        }

        public double RoiY
        {
            get => _roiY;
            set
            {
                if (SetProperty(ref _roiY, value))
                {
                    OnPropertyChanged(nameof(RoiTopStar));
                    OnPropertyChanged(nameof(RoiBottomStar));
                }
            }
        }

        public double RoiWidth
        {
            get => _roiWidth;
            set
            {
                if (SetProperty(ref _roiWidth, value))
                {
                    OnPropertyChanged(nameof(RoiWidthStar));
                    OnPropertyChanged(nameof(RoiRightStar));
                }
            }
        }

        public double RoiHeight
        {
            get => _roiHeight;
            set
            {
                if (SetProperty(ref _roiHeight, value))
                {
                    OnPropertyChanged(nameof(RoiHeightStar));
                    OnPropertyChanged(nameof(RoiBottomStar));
                }
            }
        }

        public System.Windows.GridLength RoiLeftStar => new System.Windows.GridLength(System.Math.Max(0, _roiX), System.Windows.GridUnitType.Star);
        public System.Windows.GridLength RoiWidthStar => new System.Windows.GridLength(System.Math.Max(1, _roiWidth), System.Windows.GridUnitType.Star);
        public System.Windows.GridLength RoiRightStar => new System.Windows.GridLength(System.Math.Max(0, 100 - _roiX - _roiWidth), System.Windows.GridUnitType.Star);
        public System.Windows.GridLength RoiTopStar => new System.Windows.GridLength(System.Math.Max(0, _roiY), System.Windows.GridUnitType.Star);
        public System.Windows.GridLength RoiHeightStar => new System.Windows.GridLength(System.Math.Max(1, _roiHeight), System.Windows.GridUnitType.Star);
        public System.Windows.GridLength RoiBottomStar => new System.Windows.GridLength(System.Math.Max(0, 100 - _roiY - _roiHeight), System.Windows.GridUnitType.Star);

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

        private int _fps = 30;
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

        private bool _testMode;
        public bool TestMode
        {
            get => _testMode;
            set => SetProperty(ref _testMode, value);
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
        public ICommand ToggleStreamDetectionCommand { get; }
        public ICommand ToggleSettingsCommand { get; }
        public ICommand CancelSettingsCommand { get; }

        public DashboardViewModel(IYoloDetector detector, ModelManagerService modelManager, ISettingsService settingsService, ICameraService cameraService)
        {
            _logService = InMemoryLogService.Instance;
            _cameraService = cameraService;
            _cameraService.FrameCaptured += OnFrameCaptured;
            _detector = detector;
            _modelManager = modelManager;
            _settingsService = settingsService;

            SupportedResolutions = new ObservableCollection<CameraResolutionOption>(_cameraService.GetSupportedResolutions());
            
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
            StartCommand = new RelayCommand(async _ => await Start(), _ => (IsCameraConnected || (TestMode && HasTestImage)) && !IsDetecting && !IsSettingsMode);
            ToggleStreamDetectionCommand = new RelayCommand(_ => ToggleStreamDetection(), _ => IsCameraConnected || (TestMode && HasTestImage));
            SelectTestImageCommand = new RelayCommand(_ => SelectTestImage());
            ClearTestImageCommand = new RelayCommand(_ => ClearTestImage());
            // ==================================
            ToggleSettingsCommand = new RelayCommand(_ => ToggleSettings());
            CancelSettingsCommand = new RelayCommand(_ => CancelSettings());

            LoadSettings();
            _logService.LogInfo("Dashboard started");
        }

        public void LoadSettings()
        {
            try
            {
                var settings = _settingsService.LoadSettings();
                CameraIp = settings.Camera.IpAddress;
                TriggerMode = settings.Camera.TriggerMode;
                CaptureWidth = settings.Camera.CaptureWidth;
                CaptureHeight = settings.Camera.CaptureHeight;
                Fps = settings.Camera.Fps;
                TestMode = settings.Camera.TestMode;

                ModelProcess = settings.General?.ModelProcess ?? "Capture";
                AllClass = settings.General?.AllClass ?? false;
                RoiX = settings.General?.RoiX ?? 0;
                RoiY = settings.General?.RoiY ?? 0;
                RoiWidth = settings.General?.RoiWidth ?? 100;
                RoiHeight = settings.General?.RoiHeight ?? 100;
                
                if (SupportedResolutions != null)
                {
                    SelectedResolution = SupportedResolutions.FirstOrDefault(r => r.Width == CaptureWidth && r.Height == CaptureHeight && r.Fps == Fps)
                                         ?? SupportedResolutions.FirstOrDefault(r => r.Width == CaptureWidth && r.Height == CaptureHeight)
                                         ?? SupportedResolutions.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to load settings in Dashboard: {ex.Message}");
            }
        }

        private async Task ConnectCamera()
        {
            StatusMessage = "Connecting to camera...";
            _logService.LogInfo("Connecting to camera...");
            
            var settings = _settingsService.LoadSettings();
            bool success = await _cameraService.ConnectAsync(settings.Camera.CaptureWidth, settings.Camera.CaptureHeight, settings.Camera.Fps);
            
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
            IsStreamingDetectionActive = false;
            _liveFrame = null;
            DisplayFrame = null;
            IsShowingResult = false;
            StatusMessage = "Camera disconnected";
            _logService.LogInfo("Camera disconnected");
        }

        private async Task Start()
        {
            if (IsDetecting) return;

            var settings = _settingsService.LoadSettings();
            bool allClass = settings.General?.AllClass == true;
            var dbService = new DatabaseService(_settingsService);

            DbItem? dbItemToSave = SelectedItem;
            if (dbItemToSave == null && allClass)
            {
                try
                {
                    var activeItems = await dbService.GetActiveItemsAsync();
                    dbItemToSave = activeItems.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Failed to load fallback item for database reference: {ex.Message}");
                }
            }

            if (dbItemToSave == null)
            {
                StatusMessage = "Please select a material sample first or check your database connection.";
                _logService.LogError("Start failed: No active item reference available.");
                return;
            }

            IsDetecting = true;
            StatusMessage = "Starting local detection...";
            _logService.LogInfo("Start local detection");

            try
            {
                // ====== TEST IMAGE MODE CODE ======
                byte[]? imageData = (TestMode && _testSelectedImageBytes != null) ? _testSelectedImageBytes : await _cameraService.CaptureAsync();
                // ==================================
                if (imageData == null)
                {
                    StatusMessage = "Failed to capture image";
                    _logService.LogError("Failed to capture image");
                    IsDetecting = false;
                    return;
                }

                float confThreshold = (float)settings.AiModels.ConfidenceThreshold;
                float nmsThreshold = (float)settings.AiModels.NmsThreshold;
                int? targetClassCode = allClass ? null : (int?)dbItemToSave?.ClassCode;
                double rx = settings.General?.RoiX ?? 0;
                double ry = settings.General?.RoiY ?? 0;
                double rw = settings.General?.RoiWidth ?? 100;
                double rh = settings.General?.RoiHeight ?? 100;

                StatusMessage = "Running local AI inference...";
                string targetDesc = targetClassCode.HasValue ? targetClassCode.Value.ToString() : "All Classes";
                _logService.LogInfo($"Inference on model: {System.IO.Path.GetFileName(_detector.CurrentModelPath)} targetClass={targetDesc} conf={confThreshold} iou={nmsThreshold} roi=[{rx}%,{ry}%,{rw}%,{rh}%]");

                // Chạy AI local trên luồng phụ để tránh block UI
                var localResult = await Task.Run(() => _detector.Detect(
                    imageData, 
                    confThreshold, 
                    nmsThreshold, 
                    targetClassCode, 
                    rx, 
                    ry, 
                    rw, 
                    rh,
                    settings.AiModels?.UseLetterbox ?? true,
                    settings.AiModels?.EnableTiling ?? false,
                    settings.AiModels?.TilingOverlap ?? 0.2
                ));

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
                await dbService.SaveDetectionResultAsync(localResult.Result, dbItemToSave!, rawPath, resultPath);

                LastResult = localResult.Result;
                StatusMessage = $"Detected: {localResult.Result.Count} objects | {localResult.Result.ProcessingTimeMs:0.##} ms";
                _logService.LogInfo(StatusMessage);

                _navigateToResult?.Invoke(localResult.Result, localResult.UiAnnotatedImageBytes ?? Array.Empty<byte>());
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

        private bool _isProcessingStreamFrame = false;
        private readonly object _streamLock = new object();

        private void OnFrameCaptured(object? sender, BitmapSource frame)
        {
            _liveFrame = frame;
            // ====== TEST IMAGE MODE CODE ======
            if (HasTestImage) return;
            // ==================================
            
            if (string.Equals(ModelProcess, "Stream", StringComparison.OrdinalIgnoreCase) && IsStreamingDetectionActive)
            {
                bool startProcessing = false;
                lock (_streamLock)
                {
                    if (!_isProcessingStreamFrame)
                    {
                        _isProcessingStreamFrame = true;
                        startProcessing = true;
                    }
                }

                if (startProcessing)
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            byte[]? imageData = null;
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                imageData = BitmapSourceToByteArray(frame);
                            });

                            if (imageData != null)
                            {
                                var settings = _settingsService.LoadSettings();
                                float confThreshold = (float)settings.AiModels.ConfidenceThreshold;
                                float nmsThreshold = (float)settings.AiModels.NmsThreshold;
                                bool allClass = settings.General?.AllClass == true;
                                int? targetClassCode = allClass ? null : (int?)SelectedItem?.ClassCode;
                                double rx = settings.General?.RoiX ?? 0;
                                double ry = settings.General?.RoiY ?? 0;
                                double rw = settings.General?.RoiWidth ?? 100;
                                double rh = settings.General?.RoiHeight ?? 100;

                                var localResult = _detector.Detect(
                                    imageData, 
                                    confThreshold, 
                                    nmsThreshold, 
                                    targetClassCode, 
                                    rx, 
                                    ry, 
                                    rw, 
                                    rh,
                                    settings.AiModels?.UseLetterbox ?? true,
                                    settings.AiModels?.EnableTiling ?? false,
                                    settings.AiModels?.TilingOverlap ?? 0.2
                                );
                                if (localResult.Result.IsSuccess && localResult.UiAnnotatedImageBytes != null)
                                {
                                    App.Current.Dispatcher.Invoke(() =>
                                    {
                                        var displayImage = ByteArrayToBitmapSource(localResult.UiAnnotatedImageBytes);
                                        if (displayImage != null)
                                        {
                                            DisplayFrame = displayImage;
                                        }
                                        LiveStreamCount = localResult.Result.Count;
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing stream frame: {ex.Message}");
                        }
                        finally
                        {
                            lock (_streamLock)
                            {
                                _isProcessingStreamFrame = false;
                            }
                        }
                    });
                }
            }
            else
            {
                if (!IsShowingResult)
                {
                    DisplayFrame = frame;
                }
            }
        }

        private byte[] BitmapSourceToByteArray(BitmapSource bitmapSource)
        {
            if (bitmapSource == null)
                return Array.Empty<byte>();

            try
            {
                // Clone để tránh bị overwrite bởi camera stream
                var safeBitmap = bitmapSource.Clone();
                
                if (safeBitmap.CanFreeze && !safeBitmap.IsFrozen)
                    safeBitmap.Freeze();

                // Convert format về chuẩn
                var formatted = new FormatConvertedBitmap();
                formatted.BeginInit();
                formatted.Source = safeBitmap;
                formatted.DestinationFormat = PixelFormats.Bgr24;
                formatted.EndInit();
                formatted.Freeze();

                using (var stream = new MemoryStream())
                {
                    var encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(formatted));
                    encoder.Save(stream);
                    return stream.ToArray();
                }
            }
            catch
            {
                return Array.Empty<byte>();
            }
}

        private void ToggleStreamDetection()
        {
            IsStreamingDetectionActive = !IsStreamingDetectionActive;
            if (IsStreamingDetectionActive)
            {
                StatusMessage = "Live stream detection started.";
                _logService.LogInfo("Live stream detection started.");
            }
            else
            {
                StatusMessage = "Live stream detection stopped.";
                _logService.LogInfo("Live stream detection stopped.");
                if (_liveFrame != null)
                {
                    DisplayFrame = _liveFrame;
                }
                LiveStreamCount = 0;
            }
        }



        private void ToggleSettings()
        {
            if (!IsSettingsMode)
            {
                StatusMessage = "Loading camera settings...";
                try
                {
                    LoadSettings();
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
                    settings.Camera.Fps = Fps;
                    settings.Camera.TestMode = TestMode;

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
