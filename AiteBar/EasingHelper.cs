using System.Windows.Media.Animation;

namespace AiteBar;

/// <summary>
/// Общие easing-функции для анимаций.
/// </summary>
public static class EasingHelper
{
    /// <summary>
    /// Стандартная easing-функция (CubicEase, EaseOut) для большинства анимаций.
    /// </summary>
    public static IEasingFunction DefaultEasing { get; } =
        new CubicEase { EasingMode = EasingMode.EaseOut };

    /// <summary>
    /// Easing для скрытия панели (CubicEase, EaseIn) — ускорение к концу.
    /// </summary>
    public static IEasingFunction HideEasing { get; } =
        new CubicEase { EasingMode = EasingMode.EaseIn };

    /// <summary>
    /// Возвращает подходящую easing-функцию для показа или скрытия.
    /// </summary>
    public static IEasingFunction ForToggle(bool hide) =>
        hide ? HideEasing : DefaultEasing;
}
