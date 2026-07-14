using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Channels;
using System.Windows;

namespace Client.Services
{
    public sealed class InMemoryLogService : ILogService, IDisposable
    {

        // ── In-memory (UI) collection ─────────────────────────────────────────
        public ObservableCollection<string> Entries { get; } = new();

        // ── File logging infrastructure ───────────────────────────────────────
        // %LocalAppData%\VisionCounter\Logs — always valid, never null
        private static readonly string LogsDir =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VisionCounter", "Logs");
        // ── Singleton ────────────────────────────────────────────────────────────
        public static InMemoryLogService Instance { get; } = new();

        private readonly Channel<string> _fileQueue =
            Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

        private string _currentLogPath = string.Empty;
        private DateTime _currentLogDate = DateTime.MinValue;
        private Timer? _midnightTimer;

        // ── Constructor ───────────────────────────────────────────────────────
        private InMemoryLogService()
        {
            Directory.CreateDirectory(LogsDir);
            RotateLogFile();            // set today's file
            ScheduleMidnightRotation(); // auto-rotate at midnight
            _ = DrainQueueAsync();      // background writer
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void LogInfo(string message) => Add("INFO", message);
        public void LogWarning(string message) => Add("WARN", message);
        public void LogError(string message, Exception? exception = null)
        {
            var full = exception == null ? message : $"{message} | {exception.Message}";
            Add("ERROR", full);
        }

        // ── Core ──────────────────────────────────────────────────────────────
        private void Add(string level, string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {level}: {message}";

            // 1. UI collection (always on UI thread)
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(() => AddToEntries(line));
            else
                AddToEntries(line);

            // 2. Queue for async file write (include full timestamp for file)
            var fileLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level}: {message}";
            _fileQueue.Writer.TryWrite(fileLine);
        }

        private void AddToEntries(string line)
        {
            Entries.Insert(0, line);
            while (Entries.Count > 500)
                Entries.RemoveAt(Entries.Count - 1);
        }

        // ── File rotation ─────────────────────────────────────────────────────
        private void RotateLogFile()
        {
            var today = DateTime.Today;
            if (_currentLogDate == today) return;

            _currentLogDate = today;
            _currentLogPath = Path.Combine(LogsDir, $"{today:yyyyMMdd}.log");
            PurgeOldLogs();
        }

        private void PurgeOldLogs(int keepDays = 30)
        {
            try
            {
                var cutoff = DateTime.Today.AddDays(-keepDays);
                foreach (var file in Directory.GetFiles(LogsDir, "????????.log"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (name.Length == 8 &&
                        DateTime.TryParseExact(name, "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var fileDate)
                        && fileDate < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { /* best-effort */ }
        }

        private void ScheduleMidnightRotation()
        {
            var now = DateTime.Now;
            var midnight = now.Date.AddDays(1);
            var delay = midnight - now;

            _midnightTimer = new Timer(_ =>
            {
                RotateLogFile();
                ScheduleMidnightRotation(); // reschedule for the next day
            }, null, delay, Timeout.InfiniteTimeSpan);
        }

        // ── Async background file writer ──────────────────────────────────────
        private async Task DrainQueueAsync()
        {
            await foreach (var line in _fileQueue.Reader.ReadAllAsync())
            {
                try
                {
                    // Re-check rotation (handles midnight while queue was busy)
                    if (DateTime.Today != _currentLogDate)
                        RotateLogFile();

                    await File.AppendAllTextAsync(_currentLogPath, line + Environment.NewLine);
                }
                catch { /* don't crash the app on log failures */ }
            }
        }

        // ── Disposal ──────────────────────────────────────────────────────────
        public void Dispose()
        {
            _midnightTimer?.Dispose();
            _fileQueue.Writer.TryComplete();
        }
    }
}
