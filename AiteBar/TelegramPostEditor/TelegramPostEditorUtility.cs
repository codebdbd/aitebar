using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
[Utility]
public sealed class TelegramPostEditorUtility : UtilityBase<TelegramPostEditorWindow>
{
    public override string Id => "TelegramPostEditor";
    public override string DisplayNameKey => "Tool_TelegramPostEditor";
    public override string IconGlyph => "\uF69A";
    public override string IconColor => UtilityIconColors.TextWorkspace;

    protected override TelegramPostEditorWindow CreateWindow(AppSettingsService settingsService, Window? owner)
    {
        MainWindow? mainWindow = owner as MainWindow;
        AiGateway aiGateway = mainWindow?.GetAiGateway() ?? new AiGateway(settingsService);
        return new TelegramPostEditorWindow(settingsService, mainWindow, aiGateway);
    }

    protected override void ShowWindow(TelegramPostEditorWindow window, AppSettingsService settingsService)
    {
        window.ShowWorkspace(settingsService);
    }
}
