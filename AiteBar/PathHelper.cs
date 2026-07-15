using System;
using System.IO;

namespace AiteBar
{
    internal static class PathHelper
    {
        public const string AppCompany = "Codebdbd";
        public const string AppName = "Aite Bar";

        // Для тестов: возможность переопределить корневую папку данных
        private static string? _appDataFolderOverride;
        private static string? _appDataFolderFallbackOverride;
        private static readonly object _appDataLock = new();
        internal static string DefaultAppDataFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppCompany,
            AppName);

        public static string AppDataFolder
        {
            get
            {
                lock (_appDataLock)
                {
                    return _appDataFolderOverride ?? _appDataFolderFallbackOverride ?? DefaultAppDataFolder;
                }
            }
        }

        public static string ConfigFile => Path.Combine(AppDataFolder, "custom_buttons.json");
        public static string SettingsFile => Path.Combine(AppDataFolder, "settings.json");
        public static string LogFile => Path.Combine(AppDataFolder, "error.log");
        public static string IconsFolder => Path.Combine(AppDataFolder, "Icons");

        // Методы для тестов
        public static void SetAppDataFolderOverride(string path)
        {
            lock (_appDataLock)
            {
                _appDataFolderOverride = path;
            }
        }

        public static void ClearAppDataFolderOverride()
        {
            lock (_appDataLock)
            {
                _appDataFolderOverride = null;
            }
        }

        internal static void SetAppDataFolderFallbackOverride(string? path)
        {
            lock (_appDataLock)
            {
                _appDataFolderFallbackOverride = string.IsNullOrWhiteSpace(path) ? null : path;
            }
        }

        public static void EnsureDirectories()
        {
            if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
            if (!Directory.Exists(IconsFolder)) Directory.CreateDirectory(IconsFolder);
        }

        public static string? FindExecutableOnPath(string fileName)
        {
            string? pathValue = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathValue))
            {
                foreach (string dir in pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        string candidate = Path.Combine(dir.Trim(), fileName);
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                }
            }

            return null;
        }
    }
}
