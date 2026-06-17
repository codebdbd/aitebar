namespace AiteBar;

/// <summary>
/// Централизованные константы для длительностей анимаций (мс).
/// </summary>
public static class Constants
{
    /// <summary>Fade-in/out для кнопок при drag-and-drop (мс).</summary>
    public const int AnimationFadeMs = 140;

    /// <summary>Slide-анимация при перестановке кнопок (мс).</summary>
    public const int AnimationSlideMs = 150;

    /// <summary>Показ панели (мс).</summary>
    public const int PanelShowAnimationMs = 175;

    /// <summary>Скрытие панели (мс).</summary>
    public const int PanelHideAnimationMs = 140;

    /// <summary>Анимация окна QuickNote (мс).</summary>
    public const int QuickNoteSlideMs = 200;

    /// <summary>Padding between panel edge and screen edge (device-independent pixels).</summary>
    public const double PanelScreenPadding = 20;

    /// <summary>Size of the drag handle (device-independent pixels).</summary>
    public const double DragHandleSpan = 18;

    /// <summary>Wheel delta threshold for context switching.</summary>
    public const int WheelDeltaPerContextSwitch = 120;

    /// <summary>Cooldown period for context switching via mouse wheel (milliseconds).</summary>
    public const int ContextWheelSwitchCooldownMs = 220;

    /// <summary>Outer size of a button (device-independent pixels).</summary>
    public const double ButtonOuterSize = 44;

    /// <summary>Size of a separator between button groups (device-independent pixels).</summary>
    public const double SeparatorSize = 9;

    /// <summary>Chrome size (padding/border) around the panel (device-independent pixels).</summary>
    public const double PanelChrome = 8;
}
