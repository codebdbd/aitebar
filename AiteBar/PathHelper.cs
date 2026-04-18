using System;
using System.IO;

namespace AiteBar
{
    internal static class PathHelper
    {
        public const string AppCompany = "Codebdbd";
        public const string AppName = "Aite Bar";

        public static string AppDataFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppCompany, AppName);

        public static string ConfigFile => Path.Combine(AppDataFolder, "custom_buttons.json");
        public static string SettingsFile => Path.Combine(AppDataFolder, "settings.json");
        public static string LogFile => Path.Combine(AppDataFolder, "error.log");
        public static string IconsFolder => Path.Combine(AppDataFolder, "Icons");

        public static void EnsureDirectories()
        {
            if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
            if (!Directory.Exists(IconsFolder)) Directory.CreateDirectory(IconsFolder);
        }
    }
}
