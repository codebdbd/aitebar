using System.IO;

namespace AiteBar;

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
        if (Enum.TryParse<ActionType>(actionType, out ActionType parsed))
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

    public static bool TryNormalizeWebUrl(string value, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string candidate = value.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "https://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri))
            return false;

        if (uri is null)
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        if (string.IsNullOrWhiteSpace(uri.Host) || !uri.Host.Contains('.', StringComparison.Ordinal))
            return false;

        normalized = uri.ToString();
        return true;
    }
}
