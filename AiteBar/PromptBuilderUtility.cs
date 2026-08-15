using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class PromptBuilderUtility : UtilityBase<PromptBuilderWindow>
{
    private static readonly PromptBuilderService _service = new();
    private AiGateway? _gateway;

    public override string Id => "PromptBuilder";
    public override string DisplayNameKey => "Tool_PromptBuilder";
    public override string IconGlyph => "\uF6A6";
    public override string IconColor => UtilityIconColors.AiTools;

    protected override PromptBuilderWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        // The window is recreated on every opening, while model discovery is cached
        // by AiGateway for 15 minutes. Keep that cache for the utility lifetime.
        _gateway ??= new AiGateway(settingsService);
        return new(_service, settingsService, owner as MainWindow, _gateway);
    }

    protected override void ShowWindow(PromptBuilderWindow window, AppSettingsService settingsService) =>
        window.ShowNearPanel(settingsService);

    protected override bool RestoreExistingWindow(PromptBuilderWindow window)
    {
        return false;
    }
}
