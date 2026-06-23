using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Client.Commands;
using Client.Models;
using Client.Services;

namespace Client.ViewModels
{
    public class HistoryViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private Action<HistoryLog>? _navigateToResult;

        private ObservableCollection<HistoryLog> _history;
        private HistoryLog? _selectedLog;
        private string _statusMessage;
        private bool _isLoading;
        private string _searchText;

        private int _pageNumber = 1;
        private int _totalCount = 0;
        private int _pageSize = 100;

        public ObservableCollection<HistoryLog> History
        {
            get => _history;
            set => SetProperty(ref _history, value);
        }

        public HistoryLog? SelectedLog
        {
            get => _selectedLog;
            set => SetProperty(ref _selectedLog, value);
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

        public ICommand ClearHistoryCommand { get; }
        public ICommand ExportHistoryCommand { get; }
        public ICommand RefreshHistoryCommand { get; }
        public ICommand SearchHistoryCommand { get; }
        public ICommand ViewResultCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }

        public HistoryViewModel()
        {
            _settingsService = new SettingsService();
            _history = new ObservableCollection<HistoryLog>();
            _statusMessage = "Ready";
            _isLoading = false;
            _searchText = string.Empty;

            ClearHistoryCommand = new RelayCommand(_ => ClearHistory());
            ExportHistoryCommand = new RelayCommand(_ => ExportHistory());
            RefreshHistoryCommand = new RelayCommand(async _ => await RefreshHistory());
            SearchHistoryCommand = new RelayCommand(async _ => await SearchHistory());
            ViewResultCommand = new RelayCommand(log => ViewResult(log as HistoryLog));

            NextPageCommand = new RelayCommand(async _ =>
            {
                if (CanGoNext)
                {
                    PageNumber++;
                    await LoadHistory();
                }
            });

            PrevPageCommand = new RelayCommand(async _ =>
            {
                if (CanGoPrev)
                {
                    PageNumber--;
                    await LoadHistory();
                }
            });

            Task.Run(async () => await LoadHistory());
        }

        private void ClearHistory()
        {
            History.Clear();
            StatusMessage = "History cleared (local mock only)";
        }

        private void ExportHistory()
        {
            StatusMessage = "Export history logic is not implemented yet (requires SQL Server database).";
        }

        public async Task LoadHistory()
        {
            if (IsLoading) return;

            IsLoading = true;
            StatusMessage = "Loading history logs from database...";

            try
            {
                var dbService = new DatabaseService(_settingsService);
                var (logs, total) = await dbService.GetHistoryLogsPagedAsync(SearchText, PageNumber, PageSize);

                App.Current.Dispatcher.Invoke(() =>
                {
                    History.Clear();
                    foreach (var log in logs)
                    {
                        History.Add(log);
                    }
                    TotalCount = total;
                });

                StatusMessage = $"Loaded {History.Count} of {total} history entries";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load history: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshHistory()
        {
            StatusMessage = "Refreshing history...";
            PageNumber = 1;
            await LoadHistory();
        }

        private async Task SearchHistory()
        {
            StatusMessage = "Searching history...";
            PageNumber = 1;
            await LoadHistory();
        }

        public void SetNavigateToResult(Action<HistoryLog> navigateToResult)
        {
            _navigateToResult = navigateToResult;
        }

        private void ViewResult(HistoryLog? log)
        {
            if (log != null)
            {
                _navigateToResult?.Invoke(log);
            }
        }
    }
}
