namespace AiteBar;

internal sealed record ButtonVisualState(
    string Id,
    string Icon,
    string IconFont,
    string ImagePath);

internal static class ButtonVisualStateHelper
{
    public static IReadOnlyList<ButtonVisualState> CreateSnapshot(
        IEnumerable<CustomElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        return elements
            .Select(element => new ButtonVisualState(
                element.Id ?? string.Empty,
                element.Icon ?? string.Empty,
                element.IconFont ?? string.Empty,
                element.ImagePath ?? string.Empty))
            .ToList();
    }
}
