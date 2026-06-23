namespace Client.Services
{
    public interface ILogService
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? exception = null);
    }
}
