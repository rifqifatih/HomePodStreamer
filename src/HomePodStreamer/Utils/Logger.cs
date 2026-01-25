using System;
using System.IO;

namespace HomePodStreamer.Utils
{
    public static class Logger
    {
        private static readonly string LogFilePath;
        private static readonly object LockObject = new();

        static Logger()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logDir = Path.Combine(appDataPath, "HomePodStreamer", "Logs");
            Directory.CreateDirectory(logDir);
            LogFilePath = Path.Combine(logDir, $"log_{DateTime.Now:yyyyMMdd}.txt");
        }

        public static void Info(string message)
        {
            Log("INFO", message);
        }

        public static void Warning(string message)
        {
            Log("WARN", message);
        }

        public static void Error(string message, Exception? exception = null)
        {
            var fullMessage = exception != null ? $"{message} - {exception}" : message;
            Log("ERROR", fullMessage);
        }

        private static void Log(string level, string message)
        {
            try
            {
                lock (LockObject)
                {
                    var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                    System.Diagnostics.Debug.WriteLine(logEntry);
                }
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
}
