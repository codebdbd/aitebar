using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;

namespace AiteBar
{
    public class BrowserProfileInfo
    {
        public string DisplayName { get; set; } = "";
        public string ProfilePath { get; set; } = "";
        public string ProfileName => Path.GetFileName(ProfilePath);
    }

    [SupportedOSPlatform("windows")]
    internal static class BrowserHelper
    {
        private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static string GetExecutablePath(BrowserType type)
        {
            return type switch
            {
                BrowserType.Chrome => GetPathFromRegistry(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe") 
                                     ?? SearchCommonPaths("chrome.exe", [
                                         @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                                         @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                                         Path.Combine(LocalAppData, @"Google\Chrome\Application\chrome.exe")
                                     ]),
                BrowserType.Edge => GetPathFromRegistry(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe")
                                   ?? SearchCommonPaths("msedge.exe", [
                                       @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                                       @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
                                   ]),
                BrowserType.Brave => GetPathFromRegistry(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\brave.exe")
                                    ?? SearchCommonPaths("brave.exe", [
                                        Path.Combine(LocalAppData, @"BraveSoftware\Brave-Browser\Application\brave.exe"),
                                        @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe"
                                    ]),
                BrowserType.Yandex => GetPathFromRegistry(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\browser.exe")
                                     ?? SearchCommonPaths("browser.exe", [
                                         Path.Combine(LocalAppData, @"Yandex\YandexBrowser\Application\browser.exe")
                                     ]),
                BrowserType.Opera => GetPathFromRegistry(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\opera.exe")
                                    ?? SearchCommonPaths("opera.exe", [
                                        Path.Combine(LocalAppData, @"Programs\Opera\opera.exe"),
                                        @"C:\Program Files\Opera\opera.exe"
                                    ]),
                BrowserType.OperaGX => SearchCommonPaths("opera.exe", [
                                        Path.Combine(LocalAppData, @"Programs\Opera GX\opera.exe"),
                                        @"C:\Program Files\Opera GX\opera.exe"
                                    ]),
                BrowserType.Vivaldi => GetPathFromRegistry(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\vivaldi.exe")
                                      ?? SearchCommonPaths("vivaldi.exe", [
                                          Path.Combine(LocalAppData, @"Vivaldi\Application\vivaldi.exe"),
                                          @"C:\Program Files\Vivaldi\Application\vivaldi.exe"
                                      ]),
                BrowserType.Firefox => GetPathFromRegistry(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\firefox.exe")
                                      ?? SearchCommonPaths("firefox.exe", [
                                          @"C:\Program Files\Mozilla Firefox\firefox.exe",
                                          @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe"
                                      ]),
                _ => "chrome.exe"
            };
        }

        private static string? GetPathFromRegistry(string keyPath)
        {
            try
            {
                return Microsoft.Win32.Registry.GetValue($@"HKEY_LOCAL_MACHINE\{keyPath}", "", null) as string
                       ?? Microsoft.Win32.Registry.GetValue($@"HKEY_CURRENT_USER\{keyPath}", "", null) as string;
            }
            catch { return null; }
        }

        private static string SearchCommonPaths(string defaultName, string[] candidates)
        {
            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }
            return defaultName;
        }

        public static string GetUserDataPath(BrowserType type)
        {
            return type switch
            {
                BrowserType.Chrome => Path.Combine(LocalAppData, @"Google\Chrome\User Data"),
                BrowserType.Edge => Path.Combine(LocalAppData, @"Microsoft\Edge\User Data"),
                BrowserType.Brave => Path.Combine(LocalAppData, @"BraveSoftware\Brave-Browser\User Data"),
                BrowserType.Yandex => Path.Combine(LocalAppData, @"Yandex\YandexBrowser\User Data"),
                BrowserType.Opera => Path.Combine(AppData, @"Opera Software\Opera Stable"),
                BrowserType.OperaGX => Path.Combine(AppData, @"Opera Software\Opera GX Stable"),
                BrowserType.Vivaldi => Path.Combine(LocalAppData, @"Vivaldi\User Data"),
                BrowserType.Firefox => Path.Combine(AppData, @"Mozilla\Firefox"),
                _ => Path.Combine(LocalAppData, @"Google\Chrome\User Data")
            };
        }

        public static BrowserType GetSystemDefaultBrowser()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
                var progId = key?.GetValue("ProgId")?.ToString();
                if (progId == null) return BrowserType.Chrome;

                if (progId.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) return BrowserType.Chrome;
                if (progId.Contains("MSEdge", StringComparison.OrdinalIgnoreCase)) return BrowserType.Edge;
                if (progId.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) return BrowserType.Firefox;
                if (progId.Contains("Brave", StringComparison.OrdinalIgnoreCase)) return BrowserType.Brave;
                if (progId.Contains("Yandex", StringComparison.OrdinalIgnoreCase)) return BrowserType.Yandex;
                if (progId.Contains("Opera", StringComparison.OrdinalIgnoreCase)) return BrowserType.Opera;
                if (progId.Contains("Vivaldi", StringComparison.OrdinalIgnoreCase)) return BrowserType.Vivaldi;
            }
            catch { }
            return BrowserType.Chrome;
        }

        public static List<BrowserProfileInfo> GetProfiles(BrowserType type)
        {
            string basePath = GetUserDataPath(type);
            if (!Directory.Exists(basePath)) return [];

            List<BrowserProfileInfo> result = [];

            if (type == BrowserType.Firefox)
            {
                return GetFirefoxProfiles(basePath);
            }
            
            // Standard Chromium profiles
            var profileDirs = Directory.GetDirectories(basePath, "Profile *").ToList();
            if (Directory.Exists(Path.Combine(basePath, "Default")))
                profileDirs.Insert(0, Path.Combine(basePath, "Default"));

            // If no profiles found, maybe it's Opera style where User Data is the profile itself
            if (profileDirs.Count == 0 && File.Exists(Path.Combine(basePath, "Preferences")))
            {
                profileDirs.Add(basePath);
            }

            foreach (var dir in profileDirs)
            {
                if (!Directory.Exists(dir)) continue;
                string prefFile = Path.Combine(dir, "Preferences");
                string displayName = Path.GetFileName(dir);
                if (displayName == "User Data" || displayName == "Opera Stable" || displayName == "Opera GX Stable") 
                    displayName = "Default";

                if (File.Exists(prefFile))
                {
                    try
                    {
                        using var stream = File.OpenRead(prefFile);
                        using var doc = JsonDocument.Parse(stream);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("account_info", out var accounts) &&
                            accounts.ValueKind == JsonValueKind.Array &&
                            accounts.GetArrayLength() > 0)
                        {
                            var first = accounts[0];
                            if (first.TryGetProperty("email", out var emailProp) &&
                                !string.IsNullOrWhiteSpace(emailProp.GetString()))
                            {
                                displayName = emailProp.GetString()!;
                            }
                        }
                        else if (root.TryGetProperty("profile", out var profile) &&
                                 profile.TryGetProperty("name", out var nameProp))
                        {
                            displayName = nameProp.GetString() ?? displayName;
                        }
                    }
                    catch (Exception ex) { Logger.Log(ex); }
                }

                result.Add(new BrowserProfileInfo { DisplayName = displayName, ProfilePath = dir });
            }
            return [.. result.OrderBy(p => p.DisplayName)];
        }

        private static List<BrowserProfileInfo> GetFirefoxProfiles(string basePath)
        {
            List<BrowserProfileInfo> result = [];
            string profilesIni = Path.Combine(basePath, "profiles.ini");
            if (!File.Exists(profilesIni)) return result;

            try
            {
                string[] lines = File.ReadAllLines(profilesIni);
                string? currentName = null;
                string? currentPath = null;
                bool isRelative = true;

                foreach (var line in lines)
                {
                    if (line.StartsWith("[Profile", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(currentName) && !string.IsNullOrEmpty(currentPath))
                        {
                            string fullPath = isRelative ? Path.Combine(basePath, currentPath) : currentPath;
                            result.Add(new BrowserProfileInfo { DisplayName = currentName, ProfilePath = fullPath });
                        }
                        currentName = null;
                        currentPath = null;
                        isRelative = true;
                    }
                    else if (line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase))
                    {
                        currentName = line[5..].Trim();
                    }
                    else if (line.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
                    {
                        currentPath = line[5..].Trim().Replace('/', '\\');
                    }
                    else if (line.StartsWith("IsRelative=", StringComparison.OrdinalIgnoreCase))
                    {
                        isRelative = line[11..].Trim() == "1";
                    }
                }

                // Last profile
                if (!string.IsNullOrEmpty(currentName) && !string.IsNullOrEmpty(currentPath))
                {
                    string fullPath = isRelative ? Path.Combine(basePath, currentPath) : currentPath;
                    result.Add(new BrowserProfileInfo { DisplayName = currentName, ProfilePath = fullPath });
                }
            }
            catch (Exception ex) { Logger.Log(ex); }

            return [.. result.OrderBy(p => p.DisplayName)];
        }

        public static string AdvanceProfile(BrowserType type, string lastUsedProfile)
        {
            var profiles = GetProfiles(type);
            if (profiles.Count == 0) return "";

            int idx = profiles.FindIndex(p => p.ProfileName == lastUsedProfile || p.ProfilePath == lastUsedProfile);
            if (idx < 0) return profiles[0].ProfileName;
            return profiles[(idx + 1) % profiles.Count].ProfileName;
        }
    }
}
