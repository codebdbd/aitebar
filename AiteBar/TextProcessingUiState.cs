namespace AiteBar;

internal readonly record struct TextProcessingUiStateInput(
    string Text,
    bool IsProcessing,
    bool IsLoadingModels,
    bool HasEligibleModel,
    bool HasClipboardText,
    bool HasSuccessfulResult);

internal readonly record struct TextProcessingUiState(
    int CharacterCount,
    int WordCount,
    bool IsOverLimit,
    bool CanProcess,
    bool CanCancel,
    bool CanEdit,
    bool CanPaste,
    bool CanCopy,
    bool CanClear,
    bool CanRepeat,
    bool CanSwitchVersion,
    bool CanSelectMode,
    bool CanSelectModel)
{
    public static TextProcessingUiState Create(TextProcessingUiStateInput input)
    {
        string text = input.Text ?? string.Empty;
        int characterCount = text.Length;
        int wordCount = string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        bool hasText = !string.IsNullOrWhiteSpace(text);
        bool isOverLimit = characterCount > TextProcessingService.MaxInputLength;
        bool isIdle = !input.IsProcessing;
        bool modelsReady = !input.IsLoadingModels && input.HasEligibleModel;

        return new TextProcessingUiState(
            characterCount,
            wordCount,
            isOverLimit,
            isIdle && hasText && !isOverLimit && modelsReady,
            input.IsProcessing,
            isIdle,
            isIdle && input.HasClipboardText,
            text.Length > 0,
            isIdle && text.Length > 0,
            isIdle && input.HasSuccessfulResult,
            isIdle && input.HasSuccessfulResult,
            isIdle,
            isIdle && !input.IsLoadingModels && input.HasEligibleModel);
    }
}
