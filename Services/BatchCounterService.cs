using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Client.Services
{
    public class BatchIncrementItem
    {
        public int Index { get; set; }
        public long DetectionId { get; set; }
        public int AddedQuantity { get; set; }
        public int RunningTotal { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string DisplayText
        {
            get => $"+{AddedQuantity}";
            set { }
        }
        public string FormattedTime
        {
            get => Timestamp.ToString("HH:mm:ss");
            set { }
        }
        public string TooltipText
        {
            get => $"#{Index}: +{AddedQuantity} ({ItemCode}) lúc {FormattedTime} -> Tổng: {RunningTotal}";
            set { }
        }
    }

    public class BatchCounterState
    {
        public int TotalCount { get; set; }
        public List<BatchIncrementItem> Increments { get; set; } = new();
        public List<long> CountedDetectionIds { get; set; } = new();
    }

    public interface IBatchCounterService
    {
        int TotalCount { get; }
        ObservableCollection<BatchIncrementItem> Increments { get; }
        event EventHandler? TotalChanged;
        bool AddPassResult(long detectionId, int quantity, string itemCode);
        void Reset();
    }

    public class BatchCounterService : IBatchCounterService
    {
        private readonly string _stateFilePath;
        private readonly object _lock = new();
        private readonly HashSet<long> _countedDetectionIds = new();

        public int TotalCount { get; private set; }
        public ObservableCollection<BatchIncrementItem> Increments { get; } = new();

        public event EventHandler? TotalChanged;

        public BatchCounterService()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VisionCounter"
            );
            Directory.CreateDirectory(dir);
            _stateFilePath = Path.Combine(dir, "batch_counter.json");

            LoadState();
        }

        public bool AddPassResult(long detectionId, int quantity, string itemCode)
        {
            if (quantity <= 0) return false;

            lock (_lock)
            {
                // De-duplication check: tránh cộng lặp nếu cùng 1 detectionId đã được PASS
                if (detectionId > 0 && _countedDetectionIds.Contains(detectionId))
                {
                    return false;
                }

                if (detectionId > 0)
                {
                    _countedDetectionIds.Add(detectionId);
                }

                TotalCount += quantity;

                var item = new BatchIncrementItem
                {
                    Index = Increments.Count + 1,
                    DetectionId = detectionId,
                    AddedQuantity = quantity,
                    RunningTotal = TotalCount,
                    ItemCode = string.IsNullOrWhiteSpace(itemCode) ? "N/A" : itemCode,
                    Timestamp = DateTime.Now
                };

                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() => Increments.Add(item));
                }
                else
                {
                    Increments.Add(item);
                }

                SaveState();
            }

            TotalChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void Reset()
        {
            lock (_lock)
            {
                TotalCount = 0;
                _countedDetectionIds.Clear();

                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() => Increments.Clear());
                }
                else
                {
                    Increments.Clear();
                }

                SaveState();
            }

            TotalChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SaveState()
        {
            try
            {
                var state = new BatchCounterState
                {
                    TotalCount = TotalCount,
                    Increments = Increments.ToList(),
                    CountedDetectionIds = _countedDetectionIds.ToList()
                };

                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BatchCounterService] Failed to save state: {ex.Message}");
            }
        }

        private void LoadState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    string json = File.ReadAllText(_stateFilePath);
                    var state = JsonSerializer.Deserialize<BatchCounterState>(json);
                    if (state != null)
                    {
                        TotalCount = state.TotalCount;
                        _countedDetectionIds.Clear();
                        if (state.CountedDetectionIds != null)
                        {
                            foreach (var id in state.CountedDetectionIds)
                            {
                                _countedDetectionIds.Add(id);
                            }
                        }

                        Increments.Clear();
                        if (state.Increments != null)
                        {
                            foreach (var inc in state.Increments)
                            {
                                Increments.Add(inc);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BatchCounterService] Failed to load state: {ex.Message}");
            }
        }
    }
}
