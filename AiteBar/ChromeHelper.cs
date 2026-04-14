using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;

namespace AiteBar
{
    public class ChromeProfileInfo
    {
        public string DisplayName { get; set; } = "";
        public string ProfilePath { get; set; } = "";
        public string ProfileName => Path.GetFileName(ProfilePath);
    }

    [SupportedOSPlatform("windows")]
    internal static class ChromeHelper
    {
        public static string GetChromePath()
        {
            var regVal = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe", "", null) as string;
            if (!string.IsNullOrEmpty(regVal) && File.Exists(regVal)) return regVal;
            
            string[] paths = [
                @"C:\Program Files\Google\Chrome\Application\chrome.exe", 
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe", 
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe") 
            ];
            
            foreach (var p in paths) if (File.Exists(p)) return p;
            return "chrome.exe";
        }

        public static string GetUserDataPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data");
        }

        public static List<ChromeProfileInfo> GetProfiles()
        {
            string basePath = GetUserDataPath();
            if (!Directory.Exists(basePath)) return [];
            
            List<ChromeProfileInfo> result = [];
            List<string> profileDirs = [.. Directory.GetDirectories(basePath, "Profile *")];
            profileDirs.Insert(0, Path.Combine(basePath, "Default"));

            foreach (var dir in profileDirs)
            {
                if (!Directory.Exists(dir)) continue;
                string prefFile = Path.Combine(dir, "Preferences");
                string displayName = Path.GetFileName(dir);

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

                result.Add(new ChromeProfileInfo { DisplayName = displayName, ProfilePath = dir });
            }
            return [.. result.OrderBy(p => p.DisplayName)];
        }

        public static string AdvanceProfile(string lastUsedProfile)
        {
            var profiles = GetProfiles();
            if (profiles.Count == 0) return "";
            
            // lastUsedProfile could be the directory name (e.g., "Default" or "Profile 1")
            int idx = profiles.FindIndex(p => p.ProfileName == lastUsedProfile || p.ProfilePath == lastUsedProfile);
            if (idx < 0) return profiles[0].ProfileName;
            return profiles[(idx + 1) % profiles.Count].ProfileName;
        }
    }
}
