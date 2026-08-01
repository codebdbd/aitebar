namespace AiteBar;

public sealed record ModelItem(
    string? ProviderId,
    string? ModelId,
    string Display,
    int? ContextLength)
{
    public string FullDisplay { get; init; } = Display;
}
