using System;
using System.IO;

namespace AiteBar
{
    internal static class ActionTargetHelper
    {
        private static readonly string[] ProgramExtensions = [".exe", ".lnk", ".appref-ms"];
        private static readonly string[] ScriptExtensions = [".bat", ".cmd", ".ps1", ".py"];

        public static bool IsProgramPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path))
                return false;

            string extension = Path.GetExtension(path).ToLowerInvariant();
            return Array.Exists(ProgramExtensions, item => item == extension);
        }

        public static bool IsScriptPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path))
                return false;

            string extension = Path.GetExtension(path).ToLowerInvariant();
            return Array.Exists(ScriptExtensions, item => item == extension);
        }

        public static bool IsRegularFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            return !IsProgramPath(path) && !IsScriptPath(path);
        }

        public static string NormalizeActionType(string actionType, string actionValue)
        {
            if (Enum.TryParse<ActionType>(actionType, out var parsed))
                return parsed.ToString();

            if (string.Equals(actionType, "Exe", StringComparison.OrdinalIgnoreCase))
                return NormalizeLegacyExecutableType(actionValue);

            return NormalizeLegacyExecutableType(actionValue);
        }

        private static string NormalizeLegacyExecutableType(string actionValue)
        {
            if (Directory.Exists(actionValue))
                return nameof(ActionType.Folder);

            if (IsScriptPath(actionValue))
                return nameof(ActionType.ScriptFile);

            if (IsProgramPath(actionValue))
                return nameof(ActionType.Program);

            if (File.Exists(actionValue))
                return nameof(ActionType.File);

            return nameof(ActionType.Program);
        }
    }
}
