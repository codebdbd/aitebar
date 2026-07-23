using AiteBar;

namespace AiteBar.Tests;

public sealed class TextProcessingUiStateTests
{
    [Fact]
    public void EmptyText_DisablesTextActionsAndProcessing()
    {
        TextProcessingUiState state = Create(text: string.Empty);

        Assert.Equal(0, state.CharacterCount);
        Assert.Equal(0, state.WordCount);
        Assert.False(state.CanProcess);
        Assert.False(state.CanCopy);
        Assert.False(state.CanClear);
    }

    [Fact]
    public void ValidTextAndModel_EnableProcessing()
    {
        TextProcessingUiState state = Create(text: "one two", hasEligibleModel: true);

        Assert.Equal(7, state.CharacterCount);
        Assert.Equal(2, state.WordCount);
        Assert.True(state.CanProcess);
        Assert.True(state.CanCopy);
        Assert.True(state.CanClear);
    }

    [Fact]
    public void WhitespaceOnly_DisablesProcessingButCanBeCleared()
    {
        TextProcessingUiState state = Create(text: "   ", hasEligibleModel: true);

        Assert.False(state.CanProcess);
        Assert.True(state.CanCopy);
        Assert.True(state.CanClear);
    }

    [Fact]
    public void OversizedText_IsPreservedAndDisablesProcessing()
    {
        string text = new('x', TextProcessingService.MaxInputLength + 1);
        TextProcessingUiState state = Create(text: text, hasEligibleModel: true);

        Assert.Equal(text.Length, state.CharacterCount);
        Assert.True(state.IsOverLimit);
        Assert.False(state.CanProcess);
        Assert.True(state.CanCopy);
        Assert.True(state.CanClear);
    }

    [Fact]
    public void MissingOrLoadingModels_DisablesProcessing()
    {
        Assert.False(Create(text: "text", hasEligibleModel: false).CanProcess);
        Assert.False(Create(text: "text", hasEligibleModel: true, isLoadingModels: true).CanProcess);
    }

    [Fact]
    public void Processing_LeavesOnlyCancelAndCopyAvailable()
    {
        TextProcessingUiState state = Create(
            text: "text",
            hasEligibleModel: true,
            isProcessing: true,
            hasClipboardText: true,
            hasSuccessfulResult: true);

        Assert.True(state.CanCancel);
        Assert.True(state.CanCopy);
        Assert.False(state.CanProcess);
        Assert.False(state.CanEdit);
        Assert.False(state.CanPaste);
        Assert.False(state.CanClear);
        Assert.False(state.CanRepeat);
        Assert.False(state.CanSwitchVersion);
        Assert.False(state.CanSelectMode);
        Assert.False(state.CanSelectModel);
    }

    [Fact]
    public void SuccessfulResult_EnablesRepeatAndVersionSwitchingWhenIdle()
    {
        TextProcessingUiState state = Create(
            text: "processed",
            hasEligibleModel: true,
            hasSuccessfulResult: true);

        Assert.True(state.CanRepeat);
        Assert.True(state.CanSwitchVersion);
    }

    [Fact]
    public void Paste_DependsOnClipboardTextAndIdleState()
    {
        Assert.False(Create(hasClipboardText: false).CanPaste);
        Assert.True(Create(hasClipboardText: true).CanPaste);
        Assert.False(Create(hasClipboardText: true, isProcessing: true).CanPaste);
    }

    private static TextProcessingUiState Create(
        string text = "",
        bool isProcessing = false,
        bool isLoadingModels = false,
        bool hasEligibleModel = false,
        bool hasClipboardText = false,
        bool hasSuccessfulResult = false) =>
        TextProcessingUiState.Create(new TextProcessingUiStateInput(
            text,
            isProcessing,
            isLoadingModels,
            hasEligibleModel,
            hasClipboardText,
            hasSuccessfulResult));
}
