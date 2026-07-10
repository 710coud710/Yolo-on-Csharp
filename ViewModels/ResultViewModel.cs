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
        private string _statusResult = "Pending";
        private long _detectionId;

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

            BackCommand = new RelayCommand(_ => _navigateBack?.Invoke());
            PassCommand = new RelayCommand(async _ => await UpdateStatusAsync("Pass"));
            FailCommand = new RelayCommand(async _ => await UpdateStatusAsync("Fail"));
        }

        public void LoadFromLocalResult(DetectionResult result, byte[]? annotatedImageBytes, long detectionId)
        {
            _detectionId = detectionId;
            StatusResult = "Pending";
            ImageId = (int)detectionId;
            MachineName = Environment.MachineName;
            TotalDetections = result.Count;
            ProcessingTimeMs = result.ProcessingTimeMs;
            CreatedAt = result.Timestamp;
            OutputImageUrl = result.ImagePath ?? string.Empty;
            
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

        private async Task UpdateStatusAsync(string newStatus)
        {
            if (_detectionId <= 0) return;
            StatusResult = newStatus;
            StatusMessage = $"Updating status to {newStatus}...";
            try
            {
                var dbService = new DatabaseService(new SettingsService());
                bool success = await dbService.UpdateDetectionStatusAsync(_detectionId, newStatus);
                if (success)
                {
                    StatusMessage = $"Status updated to {newStatus} successfully.";
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
