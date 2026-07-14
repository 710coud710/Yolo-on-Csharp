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
    public class ItemsViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly ISettingsService _settingsService;
        private ObservableCollection<DbItem> _items;
        private DbItem? _selectedItem;
        private DbItem? _selectedRow;
        private string _statusMessage;
        private bool _isLoading;

        private string _searchText = string.Empty;
        private int _pageNumber = 1;
        private int _totalCount = 0;
        private int _pageSize = 100;

        private bool _showHiddenItems;
        private bool _isManageMenuOpen;

        // Các thuộc tính phục vụ Form Thêm mới Item
        private bool _isAddFormOpen;
        private ObservableCollection<DbClass> _availableClasses = new();
        private DbClass? _selectedClass;
        private string _newItemCode = string.Empty;
        private string _newItemName = string.Empty;
        private ObservableCollection<string> _availableItemTypes = new();
        private string _newItemType = string.Empty;

        public ObservableCollection<DbItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public DbItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    _mainViewModel.SelectedItem = value;
                }
            }
        }

        public DbItem? SelectedRow
        {
            get => _selectedRow;
            set => SetProperty(ref _selectedRow, value);
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

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public int PageNumber
        {
            get => _pageNumber;
            set
            {
                if (SetProperty(ref _pageNumber, value))
                {
                    OnPropertyChanged(nameof(CanGoPrev));
                    OnPropertyChanged(nameof(CanGoNext));
                }
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            set
            {
                if (SetProperty(ref _totalCount, value))
                {
                    OnPropertyChanged(nameof(TotalPages));
                    OnPropertyChanged(nameof(CanGoPrev));
                    OnPropertyChanged(nameof(CanGoNext));
                }
            }
        }

        public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

        public int PageSize => _pageSize;

        public bool CanGoPrev => PageNumber > 1;
        public bool CanGoNext => PageNumber < TotalPages;

        public bool ShowHiddenItems
        {
            get => _showHiddenItems;
            set
            {
                if (SetProperty(ref _showHiddenItems, value))
                {
                    PageNumber = 1;
                    Task.Run(async () => await LoadItemsAsync());
                }
            }
        }

        public bool IsManageMenuOpen
        {
            get => _isManageMenuOpen;
            set => SetProperty(ref _isManageMenuOpen, value);
        }

        // Các thuộc tính Binding cho Form Thêm mới
        public bool IsAddFormOpen
        {
            get => _isAddFormOpen;
            set => SetProperty(ref _isAddFormOpen, value);
        }

        public ObservableCollection<DbClass> AvailableClasses
        {
            get => _availableClasses;
            set => SetProperty(ref _availableClasses, value);
        }

        public DbClass? SelectedClass
        {
            get => _selectedClass;
            set => SetProperty(ref _selectedClass, value);
        }

        public string NewItemCode
        {
            get => _newItemCode;
            set => SetProperty(ref _newItemCode, value);
        }

        public string NewItemName
        {
            get => _newItemName;
            set => SetProperty(ref _newItemName, value);
        }

        public ObservableCollection<string> AvailableItemTypes
        {
            get => _availableItemTypes;
            set => SetProperty(ref _availableItemTypes, value);
        }

        public string NewItemType
        {
            get => _newItemType;
            set => SetProperty(ref _newItemType, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }

        // Các Commands mới
        public ICommand OpenAddFormCommand { get; }
        public ICommand CloseAddFormCommand { get; }
        public ICommand SubmitAddItemCommand { get; }
        public ICommand ConfirmSelectItemCommand { get; }
        public ICommand ConfirmToggleItemActiveCommand { get; }
        public ICommand ToggleManageMenuCommand { get; }

        public ItemsViewModel(MainViewModel mainViewModel, ISettingsService settingsService)
        {
            _mainViewModel = mainViewModel;
            _settingsService = settingsService;
            _items = new ObservableCollection<DbItem>();
            _statusMessage = "Ready";

            ToggleManageMenuCommand = new RelayCommand(_ =>
            {
                IsManageMenuOpen = !IsManageMenuOpen;
            });

            RefreshCommand = new RelayCommand(async _ =>
            {
                PageNumber = 1;
                await LoadItemsAsync();
            });

            SearchCommand = new RelayCommand(async _ =>
            {
                PageNumber = 1;
                await LoadItemsAsync();
            });

            ClearSearchCommand = new RelayCommand(async _ =>
            {
                SearchText = string.Empty;
                PageNumber = 1;
                await LoadItemsAsync();
            });

            NextPageCommand = new RelayCommand(async _ =>
            {
                if (CanGoNext)
                {
                    PageNumber++;
                    await LoadItemsAsync();
                }
            });

            PrevPageCommand = new RelayCommand(async _ =>
            {
                if (CanGoPrev)
                {
                    PageNumber--;
                    await LoadItemsAsync();
                }
            });

            // Lệnh Chọn Item có hộp thoại xác nhận
            ConfirmSelectItemCommand = new RelayCommand(param =>
            {
                if (param is DbItem item)
                {
                    ConfirmSelectItem(item);
                }
            });

            // Lệnh Bật/Tắt trạng thái hoạt động của Item có hộp thoại xác nhận và cập nhật DB
            ConfirmToggleItemActiveCommand = new RelayCommand(async param =>
            {
                if (param is DbItem item)
                {
                    bool currentActive = item.IsActive;

                    // Không thể disable item đang được chọn
                    if (currentActive && _mainViewModel.SelectedItem?.Id == item.Id)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            var dialog = new Client.Views.ItemSelectConfirmDialog(
                                "Cannot disable the currently selected item. Please select another item first.",
                                "Warning",
                                "Alert",
                                "#FF9500");
                            dialog.SetAlertMode("OK");
                            if (App.Current.MainWindow != null)
                            {
                                dialog.Owner = App.Current.MainWindow;
                            }
                            dialog.ShowDialog();
                        });
                        return;
                    }

                    string actionText = currentActive ? "disable" : "enable";

                    bool confirmed = false;
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        var dialog = new Client.Views.PasswordConfirmDialog(_settingsService);
                        if (App.Current.MainWindow != null)
                        {
                            dialog.Owner = App.Current.MainWindow;
                        }
                        confirmed = dialog.ShowDialog() == true;
                    });

                    if (confirmed)
                    {
                        IsLoading = true;
                        StatusMessage = $"{(currentActive ? "Disabling" : "Enabling")} item '{item.ItemCode}'...";
                        try
                        {
                            var dbService = new DatabaseService(_settingsService);
                            bool success = await dbService.UpdateItemActiveStatusAsync(item.Id, !currentActive);
                            if (success)
                            {
                                StatusMessage = $"{(currentActive ? "Disabled" : "Enabled")} item '{item.ItemCode}' successfully.";
                                
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    if (currentActive && !ShowHiddenItems)
                                    {
                                        // Xóa khỏi danh sách Items hiển thị để biến mất ngay lập tức ko cần load lại
                                        int indexInList = Items.IndexOf(item);
                                        if (indexInList >= 0)
                                        {
                                            Items.RemoveAt(indexInList);
                                            
                                            // Cập nhật lại số thứ tự (Index) của các item còn lại phía sau
                                            int startIdx = (PageNumber - 1) * PageSize + 1;
                                            for (int i = indexInList; i < Items.Count; i++)
                                            {
                                                Items[i].Index = startIdx + i;
                                            }
                                            
                                            TotalCount--;
                                        }
                                    }
                                    else
                                    {
                                        // Nếu hiển thị cả hidden hoặc là Enable lên, chỉ cần cập nhật trạng thái IsActive tại chỗ ko cần load lại
                                        item.IsActive = !currentActive;
                                    }
                                });
                            }
                            else
                            {
                                StatusMessage = $"Failed to {actionText} item in database.";
                            }
                        }
                        catch (Exception ex)
                        {
                            StatusMessage = $"Error: {ex.Message}";
                        }
                        finally
                        {
                            IsLoading = false;
                        }
                    }
                }
            });

            // Lệnh mở Form Thêm mới: Tải dữ liệu classes và types gợi ý trước khi hiển thị form
            OpenAddFormCommand = new RelayCommand(async _ =>
            {
                IsLoading = true;
                StatusMessage = "Loading classes and types for add form...";
                try
                {
                    var dbService = new DatabaseService(_settingsService);
                    var classesList = await dbService.GetClassesAsync();
                    var typesList = await dbService.GetExistingItemTypesAsync();

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        AvailableClasses.Clear();
                        foreach (var c in classesList) AvailableClasses.Add(c);

                        AvailableItemTypes.Clear();
                        foreach (var t in typesList) AvailableItemTypes.Add(t);

                        SelectedClass = AvailableClasses.FirstOrDefault();
                        NewItemCode = string.Empty;
                        NewItemName = string.Empty;
                        NewItemType = string.Empty;

                        IsAddFormOpen = true;
                    });
                    StatusMessage = "Ready to add new item.";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to load form metadata: {ex.Message}";
                }
                finally
                {
                    IsLoading = false;
                }
            });

            // Lệnh đóng Form Thêm mới
            CloseAddFormCommand = new RelayCommand(_ =>
            {
                IsAddFormOpen = false;
                StatusMessage = "Add item cancelled.";
            });

            // Lệnh xác nhận Thêm mới Item
            SubmitAddItemCommand = new RelayCommand(async _ =>
            {
                if (SelectedClass == null)
                {
                    System.Windows.MessageBox.Show("Please select a material class.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(NewItemCode))
                {
                    System.Windows.MessageBox.Show("Please enter an item code.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(NewItemName))
                {
                    System.Windows.MessageBox.Show("Please enter an item name.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(NewItemType))
                {
                    System.Windows.MessageBox.Show("Please select or enter an item type.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                bool confirmed = false;
                App.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new Client.Views.PasswordConfirmDialog(_settingsService);
                    if (App.Current.MainWindow != null)
                    {
                        dialog.Owner = App.Current.MainWindow;
                    }
                    confirmed = dialog.ShowDialog() == true;
                });

                if (!confirmed)
                {
                    return;
                }

                IsLoading = true;
                StatusMessage = $"Adding new item: {NewItemCode}...";
                try
                {
                    var dbService = new DatabaseService(_settingsService);
                    bool success = await dbService.AddItemAsync(SelectedClass.Id, NewItemCode, NewItemName, NewItemType);
                    if (success)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            IsAddFormOpen = false;
                        });
                        StatusMessage = $"Item '{NewItemCode}' added successfully.";
                        await LoadItemsAsync();
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Failed to add item to database. (The code may already exist)", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    IsLoading = false;
                }
            });

            Task.Run(async () => await LoadItemsAsync());
        }

        public async Task LoadItemsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            StatusMessage = "Loading items from database...";

            try
            {
                var dbService = new DatabaseService(_settingsService);

                // Khôi phục từ settings khi khởi động ứng dụng
                if (_mainViewModel.SelectedItem == null)
                {
                    try
                    {
                        var settings = _settingsService.LoadSettings();
                        if (settings.LastSelectedItemId.HasValue)
                        {
                            var savedItem = await dbService.GetItemByIdAsync(settings.LastSelectedItemId.Value);
                            if (savedItem != null && savedItem.IsActive)
                            {
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    _mainViewModel.SelectedItem = savedItem;
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading saved item from settings: {ex.Message}");
                    }
                }

                var (itemsList, total) = await dbService.GetActiveItemsPagedAsync(SearchText, PageNumber, PageSize, ShowHiddenItems);

                App.Current.Dispatcher.Invoke(() =>
                {
                    Items.Clear();
                    int startIdx = (PageNumber - 1) * PageSize + 1;
                    for (int i = 0; i < itemsList.Count; i++)
                    {
                        var item = itemsList[i];
                        item.Index = startIdx + i;
                        Items.Add(item);
                    }

                    TotalCount = total;
                    
                    // Khôi phục lựa chọn từ MainViewModel nếu nó tồn tại trong danh sách và còn hoạt động
                    if (_mainViewModel.SelectedItem != null && _mainViewModel.SelectedItem.IsActive)
                    {
                        var found = Items.FirstOrDefault(i => i.Id == _mainViewModel.SelectedItem.Id);
                        if (found != null)
                        {
                            _selectedItem = found;
                            _selectedRow = found;
                        }
                        else
                        {
                            _selectedItem = _mainViewModel.SelectedItem;
                            _selectedRow = null;
                        }
                        OnPropertyChanged(nameof(SelectedItem));
                        OnPropertyChanged(nameof(SelectedRow));
                    }
                    
                    // Nếu chưa chọn item nào hoặc item đang chọn bị vô hiệu hóa, tự động chọn item hoạt động đầu tiên
                    if ((_selectedItem == null || !_selectedItem.IsActive) && Items.Any(i => i.IsActive))
                    {
                        var firstActive = Items.FirstOrDefault(i => i.IsActive);
                        if (firstActive != null)
                        {
                            _selectedItem = firstActive;
                            _selectedRow = firstActive;
                            _mainViewModel.SelectedItem = firstActive;
                            OnPropertyChanged(nameof(SelectedItem));
                            OnPropertyChanged(nameof(SelectedRow));
                        }
                    }
                });
                StatusMessage = $"Loaded {Items.Count} of {total} items.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load items: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void ConfirmSelectItem(DbItem item)
        {
            if (!item.IsActive)
            {
                StatusMessage = "Cannot select an inactive item.";
                return;
            }

            bool confirmed = false;
            App.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new Client.Views.ItemSelectConfirmDialog(item.DisplayName);
                if (App.Current.MainWindow != null)
                {
                    dialog.Owner = App.Current.MainWindow;
                }
                confirmed = dialog.ShowDialog() == true;
            });

            if (confirmed)
            {
                SelectedItem = item;
                SelectedRow = item;
                StatusMessage = $"Selected item: {item.DisplayName}";
            }
        }
    }
}
