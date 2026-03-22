using System;

namespace SmartScreenDock {
    public enum DockBlock
    {
        Utils = 1,
        AI = 2,
        Web = 3,
        Scripts = 4,
        Other = 5
    }

    public class CustomElement {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int BlockId { get; set; } = 4;
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "\uE710";
        public string Color { get; set; } = "#E3E3E3";
        public string ActionType { get; set; } = "Web";
        public string ActionValue { get; set; } = "";
        public string ChromeProfile { get; set; } = "";
        
        public bool IsAppMode { get; set; } = true;
        public bool IsIncognito { get; set; } = false;
        public bool UseRotation { get; set; } = false;
        public bool IsTopmost { get; set; } = false;
        public string LastUsedProfile { get; set; } = "";

        public bool Alt { get; set; }
        public bool Ctrl { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public string Key { get; set; } = "None";
    }
}
