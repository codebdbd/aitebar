using System;

namespace AiteBar;

public sealed class UnifiedButton
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string IconFont { get; set; } = FontHelper.FluentKey;
    public string Color { get; set; } = "#E3E3E3";
    public string ImagePath { get; set; } = "";
    public UnifiedButtonType Type { get; set; }
    public int Order { get; set; }
    public bool IsVisible { get; set; } = true;

    // For utilities
    public string? SettingsKey { get; set; }

    // For user buttons
    public CustomElement? SourceElement { get; set; }
}

public enum UnifiedButtonType
{
    Utility,
    User
}
