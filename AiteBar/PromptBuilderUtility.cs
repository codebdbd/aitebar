using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class PromptBuilderUtility : UtilityBase<PromptBuilderWindow>
{
    private static readonly PromptBuilderService _service = new();

    public override string Id => "PromptBuilder";
    public override string DisplayNameKey => "Tool_PromptBuilder";
    public override string IconGlyph => "\uE945";
    public override string IconColor => "#007ACC";

    protected override PromptBuilderWindow CreateWindow(AppSettingsService settingsService, Window? owner) =>
        new(_service, settingsService, owner as MainWindow);

    protected override void ShowWindow(PromptBuilderWindow window, AppSettingsService settingsService) =>
        window.ShowNearPanel(settingsService);

    protected override bool RestoreExistingWindow(PromptBuilderWindow window)
    {
        return false;
    }
}
