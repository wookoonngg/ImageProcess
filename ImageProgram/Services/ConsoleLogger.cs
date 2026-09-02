using System.Diagnostics;

namespace WpfImageProcessing.Services
{
    public sealed class ConsoleLogger : ILogger
    {
        public void Info(string message) => Write("INFO", message);
        public void Warning(string message) => Write("WARN", message);
        public void Debug(string message)
        {
#if DEBUG
            Write("DEBUG", message);
#endif
        }

        public void Error(string message, Exception? ex = null)
        {
            Write("ERROR", ex == null ? message : $"{message} | {ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex);
        }

        private static void Write(string level, string message)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{level}] {message}");
        }
    }
}
