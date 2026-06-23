using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Client.Commands;
using Client.Services;
using Client.Models;

namespace Client.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private bool _isSidebarOpen = true;
        private ViewModelBase _currentViewModel;
        private string _currentSection;
        private readonly ISettingsService _settingsService;
        private readonly IYoloDetector _detector;
        private readonly ModelManagerService _modelManager;
        private readonly InMemoryLogService _logService;

        private string _systemStatusText;
        private Brush _systemStatusDot;
        private Brush _dbStatusDot = Brushes.Red;
        private DbItem? _selectedItem;

        public bool IsSidebarOpen
        {
            get => _isSidebarOpen;
            set => SetProperty(ref _isSidebarOpen, value);
        }

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public string CurrentSection
        {
            get => _currentSection;
            set => SetProperty(ref _currentSection, value);
        }

        public string SystemStatusText
        {
            get => _systemStatusText;
            set => SetProperty(ref _systemStatusText, value);
        }

        public Brush SystemStatusDot
        {
            get => _systemStatusDot;
            set => SetProperty(ref _systemStatusDot, value);
        }

        public Brush DbStatusDot
        {
            get => _dbStatusDot;
            set => SetProperty(ref _dbStatusDot, value);
        }

        public DbItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    if (DashboardViewModel != null)
                    {
                        DashboardViewModel.SelectedItem = value;
                    }
                }
            }
        }

        public DashboardViewModel DashboardViewModel { get; }
        public ItemsViewModel ItemsViewModel { get; }
        public ModelViewModel ModelViewModel { get; }
        public HistoryViewModel HistoryViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }
        public ResultViewModel ResultViewModel { get; }

        public ICommand ToggleSidebarCommand { get; }
        public ICommand NavigateToDashboardCommand { get; }
        public ICommand NavigateToResultCommand { get; }
        public ICommand NavigateToItemsCommand { get; }
        public ICommand NavigateToModelCommand { get; }
        public ICommand NavigateToHistoryCommand { get; }
        public ICommand NavigateToSettingsCommand { get; }

        public MainViewModel()
        {
            _settingsService = new SettingsService();
            _logService = InMemoryLogService.Instance;

            // Khởi tạo AI engine cục bộ và Model Manager
            _detector = new YoloDetector();
            _modelManager = new ModelManagerService();

            // Đăng ký sự kiện nạp lại model động khi active.json thay đổi
            _modelManager.ActiveModelChanged += (s, modelPath) =>
            {
                //dùng GetFileNameWithoutExtension để bỏ đuôi .onnx thay vì GetFileName
                _detector.LoadModel(modelPath);
                var filename = System.IO.Path.GetFileNameWithoutExtension(modelPath);
                SystemStatusText = $"{(filename == "MockMode" ? "Mock Mode" : filename)}";
                SystemStatusDot = _detector.IsModelLoaded ? Brushes.LimeGreen : Brushes.Goldenrod;
                _logService.LogInfo($"Active model reloaded: {filename}");
            };

            // Nạp model ban đầu
            var initialModel = _modelManager.GetActiveModelPath();
            _detector.LoadModel(initialModel);

            DashboardViewModel = new DashboardViewModel(_detector, _modelManager, _settingsService);
            ItemsViewModel = new ItemsViewModel(this, _settingsService);
            ResultViewModel = new ResultViewModel();
            ModelViewModel = new ModelViewModel(_detector, _modelManager, _settingsService);
            HistoryViewModel = new HistoryViewModel();
            SettingsViewModel = new SettingsViewModel(_settingsService, _modelManager);

            // Khởi chạy tiến trình khởi tạo/tạo schema database ngầm để tránh block UI thread
            Task.Run(async () =>
            {
                try
                {
                    var dbService = new DatabaseService(_settingsService);
                    dbService.InitializeDatabase(Client.Constants.DefaultSettings.DatabaseConnectionString);
                    _logService.LogInfo("Database schema initialized and verified successfully.");

                    await dbService.InitializeMachineIdAsync();
                    _logService.LogInfo($"Current Machine cached with ID: {DatabaseService.CurrentMachineId}");

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        DbStatusDot = Brushes.LimeGreen;
                    });

                    // Sau khi DB sẵn sàng, reload lại danh mục items
                    await ItemsViewModel.LoadItemsAsync();
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Database schema initialization or machine registration failed: {ex.Message}");
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        DbStatusDot = Brushes.Red;
                    });
                }
            });

            _currentViewModel = DashboardViewModel;
            _currentSection = "Dashboard";

            var initialModelName = System.IO.Path.GetFileNameWithoutExtension(initialModel);
            _systemStatusText = $"{(initialModelName == "MockMode" ? "Mock Mode" : initialModelName)}";
            _systemStatusDot = _detector.IsModelLoaded ? Brushes.LimeGreen : Brushes.Goldenrod;
            _logService.LogInfo("Application started with local AI engine");

            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarOpen = !IsSidebarOpen);
            NavigateToDashboardCommand = new RelayCommand(_ => NavigateTo("Dashboard"));
            NavigateToResultCommand = new RelayCommand(_ => NavigateTo("Result"));
            NavigateToItemsCommand = new RelayCommand(_ => NavigateTo("Items"));
            NavigateToModelCommand = new RelayCommand(_ => NavigateTo("Model"));
            NavigateToHistoryCommand = new RelayCommand(_ => NavigateTo("History"));
            NavigateToSettingsCommand = new RelayCommand(_ => NavigateTo("Settings"));

            DashboardViewModel.SetNavigateToSettings(() => NavigateTo("Settings"));
            DashboardViewModel.SetNavigateToResult((result, bytes) =>
            {
                NavigateTo("Result");
                ResultViewModel.LoadFromLocalResult(result, bytes);
            });

            ResultViewModel.SetNavigateBack(() => NavigateTo("Dashboard"));
            
            HistoryViewModel.SetNavigateToResult((log) =>
            {
                NavigateTo("Result");
                ResultViewModel.LoadFromHistoryLog(log, _settingsService);
            });
        }

        private void NavigateTo(string section)
        {
            CurrentSection = section;
            CurrentViewModel = section switch
            {
                "Dashboard" => DashboardViewModel,
                "Result" => ResultViewModel,
                "Items" => ItemsViewModel,
                "Model" => ModelViewModel,
                "History" => HistoryViewModel,
                "Settings" => SettingsViewModel,
                _ => DashboardViewModel
            };
        }
    }
}
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              