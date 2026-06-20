using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AiteBar
{
    internal static class Logger
    {
        private static string LogPath => PathHelper.LogFile;
        private const long MaxLogSizeBytes = 1 * 1024 * 1024;
        private const int MaxBackupLogFiles = 3;
        private static readonly ConcurrentQueue<string> _logQueue = new();
        private static readonly object _flushLock = new();
        private static bool _isFlushing = false;
        // For testing purposes
        private static TaskCompletionSource<bool>? _flushCompleteTcs;

        public static void Log(Exception ex)
        {
            try
            {
                _logQueue.Enqueue(BuildLogEntry(ex));
                FlushQueue();
            }
            catch (Exception logEx)
            {
                Debug.WriteLine(logEx);
            }
        }

        public static Task LogAsync(Exception ex) =>
            Task.Run(() => Log(ex));

        // For testing
        internal static Task WaitForFlushAsync()
        {
            lock (_flushLock)
            {
                if (!_isFlushing)
                    return Task.CompletedTask;

                _flushCompleteTcs ??= new TaskCompletionSource<bool>();
                return _flushCompleteTcs.Task;
            }
        }

        private static void FlushQueue()
        {
            lock (_flushLock)
            {
                if (_isFlushing)
                    return;
                _isFlushing = true;
            }

            Task.Run(async () =>
            {
                try
                {
                    bool hasMore;
                    do
                    {
                        hasMore = false;
                        while (_logQueue.TryDequeue(out string? logEntry))
                        {
                            await WriteLogEntryAsync(logEntry);
                            hasMore = true;
                        }
                    } while (hasMore);
                }
                finally
                {
                    lock (_flushLock)
                    {
                        _isFlushing = false;
                        if (_flushCompleteTcs != null)
                        {
                            _flushCompleteTcs.SetResult(true);
                            _flushCompleteTcs = null;
                        }
                        // Double-check if new items were added after we finished
                        if (!_logQueue.IsEmpty)
                            FlushQueue();
                    }
                }
            });
        }

        private static async Task WriteLogEntryAsync(string logEntry)
        {
            try
            {
                EnsureLogFileReady();
                await File.AppendAllTextAsync(LogPath, logEntry);
            }
            catch (Exception logEx)
            {
                Debug.WriteLine(logEx);
            }
        }

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
            try
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
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                // Fallback: truncate the file
                File.WriteAllText(LogPath, string.Empty);
            }
        }
    }
}
