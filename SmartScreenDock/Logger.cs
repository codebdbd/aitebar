using System;
using System.IO;

namespace SmartScreenDock
{
    internal static class Logger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Codebdbd", "Aite Deck", "error.log"); // константы задаются в MainWindow; Logger намеренно самодостаточен
        private const long MaxLogSizeBytes = 1 * 1024 * 1024;
        private static readonly object _lockObj = new();

        public static void Log(Exception ex)
        {
            try
            {
                lock (_lockObj)
                {
                    string? dir = Path.GetDirectoryName(LogPath);
                    if (dir != null && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogSizeBytes)
                    {
                        string bakPath = LogPath + ".bak";
                        if (File.Exists(bakPath)) File.Delete(bakPath);
                        File.Move(LogPath, bakPath);
                    }

                    File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
                }
            }
            catch { }
        }
    }
}
