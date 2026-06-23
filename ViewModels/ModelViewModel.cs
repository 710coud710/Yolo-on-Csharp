using System.Collections.ObjectModel;
using System.Windows.Input;
using Client.Commands;
using Client.Models;
using Client.Services;

namespace Client.ViewModels
{
    public class ModelViewModel : ViewModelBase
    {
        private readonly IYoloDetector _detector;
        private readonly ModelManagerService _modelManager;
        private readonly ISettingsService _settingsService;
        private ObservableCollection<SelectableMaterialClass> _classes;
        private string _statusMessage;
        private bool _isLoading;
        private int _selectedCount;
        private string _searchQuery;

        public ObservableCollection<SelectableMaterialClass> Classes
        {
            get => _classes;
            set => SetProperty(ref _classes, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public int SelectedCount
        {
            get => _selectedCount;
            set => SetProperty(ref _selectedCount, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set => SetProperty(ref _searchQuery, value);
        }

        public ICommand LoadClassesCommand { get; }
        public ICommand RefreshClassesCommand { get; }
        public ICommand SaveSelectedClassesCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public ModelViewModel(IYoloDetector detector, ModelManagerService modelManager, ISettingsService settingsService)
        {
            _detector = detector;
            _modelManager = modelManager;
            _settingsService = settingsService;
            _classes = new ObservableCollection<SelectableMaterialClass>();
            _statusMessage = "Ready";
            _searchQuery = string.Empty;

            LoadClassesCommand = new RelayCommand(async _ => await LoadClasses());
            RefreshClassesCommand = new RelayCommand(async _ => await LoadClasses());
            SaveSelectedClassesCommand = new RelayCommand(_ => SaveSelectedClasses());
            SearchCommand = new RelayCommand(async _ => await LoadClasses());
            ClearSearchCommand = new RelayCommand(async _ =>
            {
                SearchQuery = string.Empty;
                await LoadClasses();
            });

            Task.Run(async () => await LoadClasses());
        }

        private async Task LoadClasses()
        {
            if (IsLoading) return;

            IsLoading = true;
            StatusMessage = "Loading models from database...";

            try
            {
                var dbService = new DatabaseService(_settingsService);
                var dbModels = await dbService.GetModelsFromDbAsync();
                var activeModelPath = _modelManager.GetActiveModelPath();

                // Chạy trên luồng phụ để tránh block UI
                await Task.Run(() =>
                {
                    var items = new List<SelectableMaterialClass>();

                    foreach (var model in dbModels)
                    {
                        bool isActive = string.Equals(model.Label, activeModelPath, StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(System.IO.Path.Combine(_modelManager.ModelsDir, model.Label), activeModelPath, StringComparison.OrdinalIgnoreCase);

                        items.Add(new SelectableMaterialClass(model, isActive));
                    }

                    // Áp dụng bộ lọc tìm kiếm
                    if (!string.IsNullOrWhiteSpace(SearchQuery))
                    {
                        var query = SearchQuery.Trim().ToLowerInvariant();
                        items = items.Where(x => 
                            x.Label.ToLowerInvariant().Contains(query) || 
                            x.MaterialCode.ToLowerInvariant().Contains(query) || 
                            x.Description.ToLowerInvariant().Contains(query)
                        ).ToList();
                    }

                    // Cập nhật kết quả lên UI thread
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Classes.Clear();
                        foreach (var selectableClass in items)
                        {
                            selectableClass.PropertyChanged += (s, e) =>
                            {
                                if (e.PropertyName == nameof(SelectableMaterialClass.IsSelected) && selectableClass.IsSelected)
                                {
                                    // Đảm bảo chỉ chọn duy nhất 1 model làm active
                                    foreach (var other in Classes)
                                    {
                                        if (other != selectableClass)
                                        {
                                            other.IsSelected = false;
                                        }
                                    }
                                    UpdateSelectedCount();
                                }
                            };
                            Classes.Add(selectableClass);
                        }
                        UpdateSelectedCount();
                    });
                });

                StatusMessage = $"Loaded {dbModels.Count} models from database.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load models: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void SaveSelectedClasses()
        {
            try
            {
                StatusMessage = "Saving selected active model...";

                var selectedModel = Classes.FirstOrDefault(c => c.IsSelected);
                if (selectedModel == null)
                {
                    StatusMessage = "Error: Please select a model first.";
                    return;
                }

                // Cập nhật active.json thông qua ModelManagerService
                _modelManager.SetActiveModel(selectedModel.MaterialCode, selectedModel.Label);

                // Cập nhật trạng thái active trong database
                Task.Run(async () =>
                {
                    var dbService = new DatabaseService(_settingsService);
                    await dbService.SetActiveModelInDbAsync(selectedModel.Id);
                });

                StatusMessage = $"Activated model: {selectedModel.Label} successfully!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save model configuration: {ex.Message}";
            }
        }

        private void UpdateSelectedCount()
        {
            SelectedCount = Classes.Count(c => c.IsSelected);
        }
    }
}
