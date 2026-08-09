namespace ProxyPlayerServer
{
    internal static class DebugLog
    {
        private static readonly string LogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "ProxyPlayerServer.log"
        );
        private static readonly Lock LogLock = new();
        public static void Write(string message)
        {
            lock (LogLock)
            {
                try
                {
                    File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}");
                }
                catch { }
            }
        }
        public static void Write(string message, Exception ex) => Write($"{message}{Environment.NewLine}{ex}");
    }
}
