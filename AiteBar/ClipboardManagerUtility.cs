using System;
using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    [Utility]
    public sealed class ClipboardManagerUtility : UtilityBase<ClipboardManagerWindow>
    {
        public override string Id => "ClipboardManager";
        public override string DisplayNameKey => "Tool_ClipboardManager";
        public override string IconGlyph => "\uE34E";
        public override string IconColor => "#F59E0B";

        protected override ClipboardManagerWindow CreateWindow(AppSettingsService settingsService, Window? owner)
        {
            return new ClipboardManagerWindow(ClipboardHistoryService.Instance) { Owner = owner };
        }

        protected override void ShowWindow(ClipboardManagerWindow window, AppSettingsService settingsService)
        {
            window.ShowNearPanel(settingsService);
        }
    }
}
