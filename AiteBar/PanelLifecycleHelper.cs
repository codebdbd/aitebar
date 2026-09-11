namespace AiteBar;

/// <summary>
/// Источник отображения панели: наведение курсора на край экрана (ховер) или явный вызов (хоткей, трей, индикатор).
/// </summary>
public enum PanelShowSource
{
    /// <summary>
    /// Вызов наведением курсора на край экрана (peek/hover). Автоскрытие по уходу мыши активно.
    /// </summary>
    PointerHover,

    /// <summary>
    /// Явный вызов пользователем (хоткей, трей, индикатор). Автоскрытие по уходу мыши отключено.
    /// </summary>
    Explicit
}

/// <summary>
/// Вспомогательные чистые методы для управления жизненным циклом и автоскрытием панели.
/// </summary>
public static class PanelLifecycleHelper
{
    /// <summary>
    /// Определяет, происходит ли движение панели вдоль горизонтальной оси (X) для заданного края прикрепления.
    /// </summary>
    public static bool IsHorizontalMotion(DockEdge edge) =>
        edge is DockEdge.Left or DockEdge.Right;

    /// <summary>
    /// Проверяет, следует ли запускать таймер задержки автоскрытия при уходе курсора с панели.
    /// </summary>
    public static bool ShouldStartPointerLeaveTimer(
        bool isShown,
        bool isAnimating,
        PanelShowSource showSource,
        bool isPanelInteractionActive,
        bool isReordering)
    {
        return isShown
            && !isAnimating
            && showSource == PanelShowSource.PointerHover
            && !isPanelInteractionActive
            && !isReordering;
    }

    /// <summary>
    /// Проверяет, следует ли выполнять скрытие панели по истечении таймера задержки ухода курсора.
    /// </summary>
    public static bool ShouldPerformPointerLeaveHide(
        bool isShown,
        bool isAnimating,
        PanelShowSource showSource,
        bool isPanelInteractionActive,
        bool isReordering,
        bool isCursorOverPanel)
    {
        return isShown
            && !isAnimating
            && showSource == PanelShowSource.PointerHover
            && !isPanelInteractionActive
            && !isReordering
            && !isCursorOverPanel;
    }
}
