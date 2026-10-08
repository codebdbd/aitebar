namespace AiteBar;

/// <summary>
/// Представляет установленное приложение Windows (классическое Desktop или Store/UWP).
/// </summary>
public sealed record InstalledAppInfo(
    string Name,
    string Path,
    string AppType,
    string Description = "",
    string? IconSourcePath = null,
    int IconIndex = 0,
    string Arguments = "")
{
    public const string TypeDesktop = "desktop";
    public const string TypeUwp = "uwp";

    public bool IsUwp => string.Equals(AppType, TypeUwp, StringComparison.OrdinalIgnoreCase);
}
