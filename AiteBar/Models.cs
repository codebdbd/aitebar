using System;

namespace AiteBar {
    public class PanelContext
    {
        public string Id { get; set; } = "context-1";
        public string Name { get; set; } = "Контекст 1";
    }

    public class HotkeyBinding
    {
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public string Key { get; set; } = "None";
    }

    public enum BrowserType
    {
        Chrome,
        Edge,
        Brave,
        Yandex,
        Opera,
        OperaGX,
        Vivaldi,
        Firefox
    }

    public enum ActionType
    {
        Web,
        Hotkey,
        Program,
        File,
        Folder,
        ScriptFile,
        Command
    }

    public class CustomElement {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "\uE710";
        public string IconFont { get; set; } = FontHelper.FluentKey;
        public string Color { get; set; } = "#E3E3E3";
        public string ActionType { get; set; } = nameof(AiteBar.ActionType.Web);
        public string ActionValue { get; set; } = "";
        public BrowserType Browser { get; set; } = BrowserType.Chrome;
        public string ChromeProfile { get; set; } = "";
        
        public bool IsAppMode { get; set; } = false;
        public bool IsIncognito { get; set; } = false;
        public bool UseRotation { get; set; } = false;
        public bool IsTopmost { get; set; } = false;
        public string LastUsedProfile { get; set; } = "";

        public bool Alt { get; set; }
        public bool Ctrl { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public string Key { get; set; } = "None";
        public string ImagePath { get; set; } = "";
        public string ContextId { get; set; } = "context-1";
    }

    public enum DockEdge { Top, Bottom, Left, Right }

    public class AppSettings {
        public bool GlobalHotkeyCtrl { get; set; } = false;
        public bool GlobalHotkeyAlt { get; set; } = false;
        public bool GlobalHotkeyShift { get; set; } = false;
        public bool GlobalHotkeyWin { get; set; } = true;
        public string GlobalHotkeyKey { get; set; } = "Z";

        public bool ShowPresetSearch { get; set; } = true;
        public bool ShowPresetScreenshot { get; set; } = true;
        public bool ShowPresetVideo { get; set; } = true;
        public bool ShowPresetCalc { get; set; } = true;

        public DockEdge Edge { get; set; } = DockEdge.Top;
        public int MonitorIndex { get; set; } = 0; // 0 = Primary, 1, 2...
        public double ActivationZoneSizePercent { get; set; } = 30; // % от ширины/высоты края
        public double PanelSizePercent { get; set; } = 80; // % от ширины/высоты экрана
        public int ActivationDelayMs { get; set; } = 250;
        public List<PanelContext> Contexts { get; set; } = [];
        public string ActiveContextId { get; set; } = "context-1";
        public HotkeyBinding NextContextHotkey { get; set; } = new();
        public HotkeyBinding PreviousContextHotkey { get; set; } = new();
        public HotkeyBinding Context1Hotkey { get; set; } = new();
        public HotkeyBinding Context2Hotkey { get; set; } = new();
        public HotkeyBinding Context3Hotkey { get; set; } = new();
        public HotkeyBinding Context4Hotkey { get; set; } = new();

        public List<CustomElement> Elements { get; set; } = new();
    }
}
