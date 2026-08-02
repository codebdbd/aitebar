namespace AiteBar;

public sealed record ModelItem(
    string? ProviderId,
    string? ModelId,
    string Display,
    int? ContextLength,
    TextProcessingModelTier Tier = TextProcessingModelTier.ManualOnly)
{
    public string FullDisplay { get; init; } = Display;
}
