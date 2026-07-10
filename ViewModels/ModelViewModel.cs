using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly ICameraService _cameraService;
        private ObservableCollection<SelectableModelClass> _classes;
        private string _statusMessage;
        private bool _isLoading;
        private int _selectedCount;
        private string _searchQuery;

        public ObservableCollection<SelectableModelClass> Classes
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
        public ICommand SelectModelCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public ModelViewModel(IYoloDetector detector, ModelManagerService modelManager, ISettingsService settingsService, ICameraService cameraService)
        {
            _detector = detector;
            _modelManager = modelManager;
            _settingsService = settingsService;
            _cameraService = cameraService;
            _classes = new ObservableCollection<SelectableModelClass>();
            _statusMessage = "Ready";
            _searchQuery = string.Empty;

            LoadClassesCommand = new RelayCommand(async _ => await LoadClasses());
            RefreshClassesCommand = new RelayCommand(async _ => await LoadClasses());
            SelectModelCommand = new RelayCommand(obj =>
            {
                if (obj is SelectableModelClass modelClass)
                {
                    _ = SelectModelAsync(modelClass);
                }
            });
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
                    var items = new List<SelectableModelClass>();

                    foreach (var model in dbModels)
                    {
                        bool isActive = string.Equals(model.ModelPath, activeModelPath, StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(System.IO.Path.Combine(_modelManager.ModelsDir, model.ModelPath), activeModelPath, StringComparison.OrdinalIgnoreCase);

                        bool isLocal = string.Equals(model.ModelPath, "MockMode", StringComparison.OrdinalIgnoreCase);
                        if (!isLocal)
                        {
                            string localPath = System.IO.Path.IsPathRooted(model.ModelPath) 
                                ? model.ModelPath 
                                : System.IO.Path.Combine(_modelManager.ModelsDir, model.ModelPath);
                            isLocal = System.IO.File.Exists(localPath);
                        }

                        items.Add(new SelectableModelClass(model, isActive) { IsLocal = isLocal });
                    }

                    // Áp dụng bộ lọc tìm kiếm
                    if (!string.IsNullOrWhiteSpace(SearchQuery))
                    {
                        var query = SearchQuery.Trim().ToLowerInvariant();
                        items = items.Where(x => 
                            x.ModelPath.ToLowerInvariant().Contains(query) || 
                            x.ModelCode.ToLowerInvariant().Contains(query) || 
                            x.Description.ToLowerInvariant().Contains(query)
                        ).ToList();
                    }

                    // Cập nhật kết quả lên UI thread
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Classes.Clear();
                        foreach (var selectableClass in items)
                        {
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

        private async Task SelectModelAsync(SelectableModelClass modelClass)
        {
            if (modelClass == null) return;

            // Prompt user about restart requirement
            bool restartNow = false;
            App.Current.Dispatcher.Invoke(() =>
            {
                var message = $"Changing the active model to '{modelClass.ModelPath}' requires a restart to take effect. Would you like to restart the application now?";
                var dialog = new Client.Views.ItemSelectConfirmDialog(message, "Restart Required");
                if (App.Current.MainWindow != null)
                {
                    dialog.Owner = App.Current.MainWindow;
                }
                restartNow = dialog.ShowDialog() == true;
            });

            try
            {
                StatusMessage = $"Activating model {modelClass.ModelPath}...";

                // 1. Mark this model as active (IsSelected = true) and others as false on the UI
                foreach (var item in Classes)
                {
                    item.IsSelected = (item == modelClass);
                }
                UpdateSelectedCount();

                // 2. Set active model config via manager
                _modelManager.SetActiveModel(modelClass.ModelCode, modelClass.ModelPath);

                // 3. Save to database in background
                await Task.Run(async () =>
                {
                    try
                    {
                        var dbService = new DatabaseService(_settingsService);
                        await dbService.SetActiveModelInDbAsync(modelClass.Id);
                    }
                    catch (Exception ex)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            StatusMessage = $"Warning: Saved locally, but failed to update active model in database: {ex.Message}";
                        });
                    }
                });

                if (restartNow)
                {
                    StatusMessage = "Restarting application...";
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var processPath = Environment.ProcessPath;
                            if (!string.IsNullOrEmpty(processPath))
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = processPath,
                                    UseShellExecute = true
                                });
                            }
                            System.Windows.Application.Current.Shutdown();
                        }
                        catch (Exception ex)
                        {
                            StatusMessage = $"Failed to restart automatically: {ex.Message}. Please restart manually.";
                        }
                    });
                }
                else
                {
                    StatusMessage = $"Activated model: {modelClass.ModelPath} successfully! Please restart the application later to apply changes.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to activate model: {ex.Message}";
            }
        }

        private void UpdateSelectedCount()
        {
            SelectedCount = Classes.Count(c => c.IsSelected);
        }
    }
}
