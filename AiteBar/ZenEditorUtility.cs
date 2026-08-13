using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class ZenEditorUtility : UtilityBase<ZenEditorWindow>
{
    private static readonly ZenEditorStore Store = new();

    public override string Id => "ZenEditor";
    public override string DisplayNameKey => "Tool_ZenEditor";
    public override string IconGlyph => "\uF1EC";
    public override string IconColor => UtilityIconColors.TextWorkspace;

    protected override ZenEditorWindow CreateWindow(AppSettingsService settingsService, Window? owner) =>
        new(Store, owner as MainWindow);

    protected override void ShowWindow(ZenEditorWindow window, AppSettingsService settingsService) =>
        window.ShowFullScreen();

    protected override bool RestoreExistingWindow(ZenEditorWindow window)
    {
        window.RestoreFromAiteBar();
        return true;
    }
}
