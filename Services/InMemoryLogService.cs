using System.Collections.ObjectModel;
using System.Windows;

namespace Client.Services
{
    public sealed class InMemoryLogService : ILogService
    {
        public static InMemoryLogService Instance { get; } = new();

        public ObservableCollection<string> Entries { get; } = new();

        public void LogInfo(string message) => Add("INFO", message);

        public void LogWarning(string message) => Add("WARN", message);

        public void LogError(string message, Exception? exception = null)
        {
            var full = exception == null ? message : $"{message} | {exception.Message}";
            Add("ERROR", full);
        }

        private void Add(string level, string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {level}: {message}";

            // Ensure collection is updated on UI thread
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => Entries.Insert(0, line));
            }
            else
            {
                Entries.Insert(0, line);
            }

            // Prevent unbounded growth (keep last 500 lines)
            while (Entries.Count > 500)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }
        }
    }
}

