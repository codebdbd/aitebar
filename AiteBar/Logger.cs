using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AiteBar
{
    internal static class Logger
    {
        private static readonly string LogPath = PathHelper.LogFile;
        private const long MaxLogSizeBytes = 1 * 1024 * 1024;
        private const int MaxBackupLogFiles = 3;
        private static readonly object _lockObj = new();

        public static void Log(Exception ex)
        {
            try
            {
                lock (_lockObj)
                {
                    EnsureLogFileReady();
                    File.AppendAllText(LogPath, BuildLogEntry(ex));
                }
            }
            catch (Exception logEx)
            {
                Debug.WriteLine(logEx);
            }
        }

        public static Task LogAsync(Exception ex) =>
            Task.Run(() => Log(ex));

        private static void EnsureLogFileReady()
        {
            string? dir = Path.GetDirectoryName(LogPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogSizeBytes)
            {
                RotateLogFile();
            }
        }

        private static string BuildLogEntry(Exception ex)
        {
            string safeExceptionText = ex.ToString()
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace("\n", " | ", StringComparison.Ordinal);

            return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {safeExceptionText}\n\n";
        }

        private static void RotateLogFile()
        {
            string directory = Path.GetDirectoryName(LogPath) ?? PathHelper.AppDataFolder;
            string fileName = Path.GetFileName(LogPath);
            string backupPath = Path.Combine(directory, $"{fileName}.{DateTime.Now:yyyyMMddHHmmss}.bak");
            File.Move(LogPath, backupPath, overwrite: false);

            foreach (var oldBackup in Directory.GetFiles(directory, $"{fileName}.*.bak")
                         .OrderByDescending(File.GetCreationTimeUtc)
                         .Skip(MaxBackupLogFiles))
            {
                File.Delete(oldBackup);
            }
        }
    }
}
