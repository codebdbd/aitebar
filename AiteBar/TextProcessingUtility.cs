using System;
using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class TextProcessingUtility : UtilityBase<TextProcessingWindow>
{
    private static readonly TextProcessingService _service = new();

    public override string Id => "TextProcessing";
    public override string DisplayNameKey => "Tool_TextProcessing";
    public override string IconGlyph => "\uF7DA";
    public override string IconColor => "#007ACC";

    protected override TextProcessingWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        var gateway = new AiGateway(settingsService);
        var viewModel = new TextProcessingViewModel(_service, gateway, settingsService);
        return new TextProcessingWindow(viewModel, settingsService) { Owner = owner };
    }

    protected override void ShowWindow(TextProcessingWindow window, AppSettingsService settingsService)
    {
        window.ShowNearPanel(settingsService);
    }

    protected override bool RestoreExistingWindow(TextProcessingWindow window)
    {
        window.RestoreFromAiteBar();
        return true;
    }
}
