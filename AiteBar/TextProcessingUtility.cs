using System;
using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class TextProcessingUtility : UtilityBase<TextProcessingWindow>
{
    private static readonly TextProcessingService _service = new();
    private AiGateway? _gateway;

    public override string Id => "TextProcessing";
    public override string DisplayNameKey => "Tool_TextProcessing";
    public override string IconGlyph => "\uF7FF";
    public override string IconColor => UtilityIconColors.TextWorkspace;

    protected override TextProcessingWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        _gateway ??= new AiGateway(settingsService);
        return new TextProcessingWindow(_service, settingsService, owner as MainWindow, _gateway);
    }

    protected override void ShowWindow(TextProcessingWindow window, AppSettingsService settingsService)
    {
        window.ShowNearPanel(settingsService);
    }

    protected override bool RestoreExistingWindow(TextProcessingWindow window)
    {
        return false;
    }
}
