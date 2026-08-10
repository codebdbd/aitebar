using AiteBar;

namespace AiteBar.Tests;

public sealed class PromptBuilderWindowTests
{
    [Fact]
    public void EffectiveProcessInput_UsesOriginalBrief_WhenVisibleTextIsUneditedResult()
    {
        string original = "brief";
        string processed = new('x', TextProcessingService.MaxInputLength + 100);

        string effective = PromptBuilderWindow.GetEffectiveProcessInputText(
            processed,
            hasSuccessfulResult: true,
            isShowingOriginal: false,
            original,
            processed);

        Assert.Equal(original, effective);
    }

    [Fact]
    public void EffectiveProcessInput_UsesVisibleText_WhenResultWasEdited()
    {
        string original = "brief";
        string processed = "result";
        string edited = "result with edits";

        string effective = PromptBuilderWindow.GetEffectiveProcessInputText(
            edited,
            hasSuccessfulResult: true,
            isShowingOriginal: false,
            original,
            processed);

        Assert.Equal(edited, effective);
    }

    [Fact]
    public void EffectiveProcessInput_UsesVisibleText_WhenOriginalVersionIsShown()
    {
        string original = "brief";
        string processed = "result";

        string effective = PromptBuilderWindow.GetEffectiveProcessInputText(
            original,
            hasSuccessfulResult: true,
            isShowingOriginal: true,
            original,
            processed);

        Assert.Equal(original, effective);
    }
}
