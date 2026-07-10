using System.IO;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Client.Commands;
using Client.Models;
using Client.Services;

namespace Client.ViewModels
{
    public class ResultViewModel : ViewModelBase
    {
        private readonly InMemoryLogService _logService;
        private Action? _navigateBack;
        private BitmapSource? _outputImage;
        private int _imageId;
        private string _machineName;
        private int _totalDetections;
        private double _processingTimeMs;
        private DateTime _createdAt;
        private string _outputImageUrl;
        private bool _isLoading;
        private string _statusMessage;
        private string _statusResult = "PENDING";
        private long _detectionId;
        private string _itemCode = string.Empty;
        private bool _isConfirmationEnabled;

        // Tracks the current relative result-image path and base save dir for file rename on confirmation
        private string? _resultImageRelativePath;
        private string? _saveDirectory;

        public string ItemCode
        {
            get => _itemCode;
            private set => SetProperty(ref _itemCode, value);
        }

        public bool IsConfirmationEnabled
        {
            get => _isConfirmationEnabled;
            private set => SetProperty(ref _isConfirmationEnabled, value);
        }

        public string StatusResult
        {
            get => _statusResult;
            set => SetProperty(ref _statusResult, value);
        }

        public BitmapSource? OutputImage
        {
            get => _outputImage;
            private set => SetProperty(ref _outputImage, value);
        }

        public int ImageId
        {
            get => _imageId;
            private set => SetProperty(ref _imageId, value);
        }

        public string MachineName
        {
            get => _machineName;
            private set => SetProperty(ref _machineName, value);
        }

        public int TotalDetections
        {
            get => _totalDetections;
            private set => SetProperty(ref _totalDetections, value);
        }

        public double ProcessingTimeMs
        {
            get => _processingTimeMs;
            private set => SetProperty(ref _processingTimeMs, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            private set => SetProperty(ref _createdAt, value);
        }

        public string OutputImageUrl
        {
            get => _outputImageUrl;
            private set => SetProperty(ref _outputImageUrl, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public ICommand BackCommand { get; }
        public ICommand PassCommand { get; }
        public ICommand FailCommand { get; }



        public ResultViewModel()
        {
            _logService = InMemoryLogService.Instance;
            _machineName = string.Empty;
            _outputImageUrl = string.Empty;
            _statusMessage = "Ready";

            BackCommand = new RelayCommand(_ => HandleBack());
            PassCommand = new RelayCommand(async _ => await UpdateStatusAsync("Pass"));
            FailCommand = new RelayCommand(async _ => await UpdateStatusAsync("Fail"));
        }

        public void LoadFromLocalResult(DetectionResult result, byte[]? annotatedImageBytes, long detectionId, string itemCode,
            string? resultImageRelativePath = null, string? saveDirectory = null)
        {
            _detectionId = detectionId;
            StatusResult = "PENDING";
            ItemCode = itemCode;
            IsConfirmationEnabled = true;
            ImageId = (int)detectionId;
            MachineName = Environment.MachineName;
            TotalDetections = result.Count;
            ProcessingTimeMs = result.ProcessingTimeMs;
            CreatedAt = result.Timestamp;
            OutputImageUrl = result.ImagePath ?? string.Empty;

            // Store for file rename on confirmation
            _resultImageRelativePath = resultImageRelativePath ?? result.ImagePath;
            _saveDirectory = saveDirectory;

            if (annotatedImageBytes != null && annotatedImageBytes.Length > 0)
            {
                try
                {
                    OutputImage = ToBitmapSource(annotatedImageBytes);
                }
                catch (Exception ex)
                {
                    OutputImage = null;
                    _logService.LogError("Error converting local image bytes to BitmapSource", ex);
                }
            }
            else
            {
                OutputImage = null;
            }

            StatusMessage = "Local result loaded";
        }

        public void LoadFromHistoryLog(HistoryLog log, ISettingsService settingsService)
        {
            _detectionId = log.Id;
            StatusResult = log.Status;
            ItemCode = log.ItemCode;
            IsConfirmationEnabled = false;
            ImageId = log.Id;
            MachineName = log.MachineName;
            TotalDetections = log.TotalObjects;
            ProcessingTimeMs = 0;
            CreatedAt = log.CreatedAt;
            OutputImageUrl = log.ResultImagePath ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(log.ResultImagePath))
            {
                try
                {
                    var settings = settingsService.LoadSettings();
                    var saveDir = settings.Image.SaveDirectory;
                    if (string.IsNullOrWhiteSpace(saveDir))
                    {
                        saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CapturedImages");
                    }
                    if (!Path.IsPathRooted(saveDir))
                    {
                        saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, saveDir);
                    }

                    var absolutePath = Path.Combine(saveDir, log.ResultImagePath.TrimStart('\\', '/'));
                    if (File.Exists(absolutePath))
                    {
                        var bytes = File.ReadAllBytes(absolutePath);
                        OutputImage = ToBitmapSource(bytes);
                    }
                    else
                    {
                        OutputImage = null;
                        _logService.LogWarning($"Result image file not found at: {absolutePath}");
                    }
                }
                catch (Exception ex)
                {
                    OutputImage = null;
                    _logService.LogError("Error loading history result image from disk", ex);
                }
            }
            else
            {
                OutputImage = null;
            }

            StatusMessage = "Historical result loaded";
        }

        private static BitmapSource ToBitmapSource(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        public void SetNavigateBack(Action navigateBack)
        {
            _navigateBack = navigateBack;
        }

        private void HandleBack()
        {
            if (IsConfirmationEnabled && StatusResult == "PENDING")
            {
                bool confirmBack = false;
                App.Current.Dispatcher.Invoke(() =>
                {
                    var message = "Bạn chưa xác nhận kết quả Pass/Fail. Bạn có chắc chắn muốn quay lại không?";
                    var dialog = new Client.Views.ItemSelectConfirmDialog(message, "Xác nhận quay lại", "Alert", "#FF9500");
                    if (App.Current.MainWindow != null)
                    {
                        dialog.Owner = App.Current.MainWindow;
                    }
                    confirmBack = dialog.ShowDialog() == true;
                });

                if (!confirmBack)
                {
                    return;
                }
            }

            _navigateBack?.Invoke();
        }

        private async Task UpdateStatusAsync(string newStatus)
        {
            if (_detectionId <= 0) return;

            bool confirmSave = false;
            App.Current.Dispatcher.Invoke(() =>
            {
                var message = $"Bạn có chắc chắn muốn xác nhận kết quả là {newStatus.ToUpper()} không?";
                var dialog = new Client.Views.ItemSelectConfirmDialog(message, "Xác nhận kết quả");
                if (App.Current.MainWindow != null)
                {
                    dialog.Owner = App.Current.MainWindow;
                }
                confirmSave = dialog.ShowDialog() == true;
            });

            if (!confirmSave) return;

            StatusResult = newStatus.ToUpper();
            StatusMessage = $"Updating status to {newStatus}...";
            try
            {
                // Determine the suffix for this status
                string suffix = newStatus.ToLowerInvariant() switch
                {
                    "pass"    => "_P",
                    "fail"    => "_F",
                    _         => "_N"   // Pending or any other
                };

                // Attempt to rename the result image file (fire-and-forget on failure — don't block DB update)
                string? newRelativePath = null;
                if (!string.IsNullOrWhiteSpace(_resultImageRelativePath))
                {
                    try
                    {
                        var settingsSvc = new SettingsService();
                        var settings   = settingsSvc.LoadSettings();
                        var saveDir    = _saveDirectory ?? settings.Image.SaveDirectory;
                        if (string.IsNullOrWhiteSpace(saveDir))
                            saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CapturedImages");
                        if (!Path.IsPathRooted(saveDir))
                            saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, saveDir);

                        var absOldPath = Path.Combine(saveDir, _resultImageRelativePath.TrimStart('\\', '/'));
                        if (File.Exists(absOldPath))
                        {
                            var dir      = Path.GetDirectoryName(absOldPath)!;
                            var nameNoExt = Path.GetFileNameWithoutExtension(absOldPath);
                            var ext      = Path.GetExtension(absOldPath);

                            // Remove any previous suffix before appending the new one
                            foreach (var s in new[] { "_P", "_F", "_N" })
                            {
                                if (nameNoExt.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                                {
                                    nameNoExt = nameNoExt[..^s.Length];
                                    break;
                                }
                            }

                            var newFilename = $"{nameNoExt}{suffix}{ext}";
                            var absNewPath  = Path.Combine(dir, newFilename);

                            // Build new relative path matching original pattern
                            var relDir      = Path.GetDirectoryName(_resultImageRelativePath.TrimStart('\\', '/'))!;
                            newRelativePath = "\\" + Path.Combine(relDir, newFilename).Replace('/', '\\');

                            await Task.Run(() => File.Move(absOldPath, absNewPath, overwrite: true));
                            _resultImageRelativePath = newRelativePath;
                            _logService.LogInfo($"Renamed result image: {absNewPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"Failed to rename result image (continuing with DB update): {ex.Message}");
                        newRelativePath = null; // Keep original path in DB if rename fails
                    }
                }

                var dbService = new DatabaseService(new SettingsService());
                bool success;
                if (newRelativePath != null)
                {
                    success = await dbService.UpdateDetectionStatusAndImagePathAsync(_detectionId, newStatus, newRelativePath);
                }
                else
                {
                    success = await dbService.UpdateDetectionStatusAsync(_detectionId, newStatus);
                }

                if (success)
                {
                    StatusMessage = $"Status updated to {newStatus} successfully.";
                    _navigateBack?.Invoke();
                }
                else
                {
                    StatusMessage = "Failed to update status in database.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating status: {ex.Message}";
            }
        }
    }
}
