namespace WpfImageProcessing.Services
{
    public interface ILogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception? ex = null);
        void Debug(string message);
    }
}
