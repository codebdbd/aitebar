using AiteBar;

namespace AiteBar.Tests;

public sealed class TextProcessingViewModelTests
{
    private TextProcessingViewModel CreateViewModel()
    {
        var service = new TextProcessingService();
        var gateway = new AiGateway(new AppSettingsService());
        var settings = new AppSettingsService();
        return new TextProcessingViewModel(service, gateway, settings);
    }

    [Fact]
    public void DefaultState_ProofreadMode()
    {
        var vm = CreateViewModel();
        Assert.Equal(TextProcessingMode.Proofread, vm.CurrentMode);
    }

    [Fact]
    public void DefaultState_EmptyInput()
    {
        var vm = CreateViewModel();
        Assert.Equal(string.Empty, vm.InputText);
        Assert.Equal(0, vm.CharacterCount);
        Assert.Equal(0, vm.WordCount);
    }

    [Fact]
    public void DefaultState_NotProcessing()
    {
        var vm = CreateViewModel();
        Assert.False(vm.IsProcessing);
        Assert.True(vm.IsEditorEnabled);
        Assert.True(vm.IsModeSwitcherEnabled);
        Assert.True(vm.IsModelSelectorEnabled);
    }

    [Fact]
    public void DefaultState_AutoModel()
    {
        var vm = CreateViewModel();
        Assert.True(vm.IsAutoModel);
    }

    [Fact]
    public void SwitchMode_ChangesCurrentMode()
    {
        var vm = CreateViewModel();
        vm.SwitchMode(TextProcessingMode.Typography);
        Assert.Equal(TextProcessingMode.Typography, vm.CurrentMode);
    }

    [Fact]
    public void SwitchMode_DuringProcessing_Ignored()
    {
        var vm = CreateViewModel();
        vm.IsProcessing = true;
        vm.SwitchMode(TextProcessingMode.Cleanup);
        Assert.Equal(TextProcessingMode.Proofread, vm.CurrentMode);
    }

    [Fact]
    public void InputText_UpdatesCounts()
    {
        var vm = CreateViewModel();
        vm.InputText = "Hello world test";

        Assert.Equal(16, vm.CharacterCount);
        Assert.Equal(3, vm.WordCount);
    }

    [Fact]
    public void InputText_EmptyText_ZeroCounts()
    {
        var vm = CreateViewModel();
        vm.InputText = "";

        Assert.Equal(0, vm.CharacterCount);
        Assert.Equal(0, vm.WordCount);
    }

    [Fact]
    public void InputText_WhitespaceOnly_ZeroWordCount()
    {
        var vm = CreateViewModel();
        vm.InputText = "   ";

        Assert.Equal(3, vm.CharacterCount);
        Assert.Equal(0, vm.WordCount);
    }

    [Fact]
    public void Clear_ResetsAllState()
    {
        var vm = CreateViewModel();
        vm.InputText = "Some text";
        vm.OriginalText = "original";
        vm.ProcessedText = "processed";
        vm.HasSuccessfulResult = true;

        vm.Clear();

        Assert.Equal(string.Empty, vm.InputText);
        Assert.Equal(string.Empty, vm.OriginalText);
        Assert.Equal(string.Empty, vm.ProcessedText);
        Assert.False(vm.HasSuccessfulResult);
        Assert.False(vm.IsShowingOriginal);
        Assert.Equal(0, vm.CharacterCount);
        Assert.Equal(0, vm.WordCount);
    }

    [Fact]
    public void ToggleVersion_WithNoResult_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.InputText = "Hello";
        vm.ToggleVersion();
        Assert.Equal("Hello", vm.InputText);
    }

    [Fact]
    public void ToggleVersion_WithResult_SwitchesBetweenOriginalAndProcessed()
    {
        var vm = CreateViewModel();
        vm.OriginalText = "original";
        vm.ProcessedText = "processed";
        vm.HasSuccessfulResult = true;
        vm.InputText = "processed";

        vm.ToggleVersion();
        Assert.Equal("original", vm.InputText);
        Assert.True(vm.IsShowingOriginal);

        vm.ToggleVersion();
        Assert.Equal("processed", vm.InputText);
        Assert.False(vm.IsShowingOriginal);
    }

    [Fact]
    public void ModeDescription_ChangesWithMode()
    {
        var vm = CreateViewModel();
        string proofreadDesc = vm.ModeDescription;

        vm.SwitchMode(TextProcessingMode.Typography);
        string typographyDesc = vm.ModeDescription;

        Assert.NotEqual(proofreadDesc, typographyDesc);
    }

    [Fact]
    public void MainButtonText_ShowsCancelDuringProcessing()
    {
        var vm = CreateViewModel();
        string idleText = vm.MainButtonText;
        Assert.False(string.IsNullOrEmpty(idleText));
    }

    [Fact]
    public void IsClearEnabled_WithText_True()
    {
        var vm = CreateViewModel();
        vm.InputText = "text";
        Assert.True(vm.IsClearEnabled);
    }

    [Fact]
    public void IsClearEnabled_Empty_False()
    {
        var vm = CreateViewModel();
        vm.InputText = "";
        Assert.False(vm.IsClearEnabled);
    }

    [Fact]
    public void IsRepeatEnabled_NoResult_False()
    {
        var vm = CreateViewModel();
        Assert.False(vm.IsRepeatEnabled);
    }

    [Fact]
    public void IsToggleVersionVisible_NoResult_False()
    {
        var vm = CreateViewModel();
        Assert.False(vm.IsToggleVersionVisible);
    }

    [Fact]
    public void CharacterCountText_FormatsCorrectly()
    {
        var vm = CreateViewModel();
        vm.InputText = "Hello";
        string text = vm.CharacterCountText;
        Assert.Contains("5", text);
    }

    [Fact]
    public void WordCountText_FormatsCorrectly()
    {
        var vm = CreateViewModel();
        vm.InputText = "Hello world";
        string text = vm.WordCountText;
        Assert.Contains("2", text);
    }

    [Fact]
    public void SelectedModelDisplay_ShowsAuto_WhenAutoModel()
    {
        var vm = CreateViewModel();
        Assert.True(vm.IsAutoModel);
        Assert.Equal(LocalizationService.Get("TextProcessing_ModelAuto"), vm.SelectedModelDisplay);
    }

    [Fact]
    public void PropertyChanged_FiredOnInputTextChange()
    {
        var vm = CreateViewModel();
        bool fired = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TextProcessingViewModel.InputText))
                fired = true;
        };

        vm.InputText = "new text";
        Assert.True(fired);
    }

    [Fact]
    public void PropertyChanged_FiredOnModeChange()
    {
        var vm = CreateViewModel();
        bool fired = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TextProcessingViewModel.CurrentMode))
                fired = true;
        };

        vm.SwitchMode(TextProcessingMode.Cleanup);
        Assert.True(fired);
    }

    [Fact]
    public void PropertyChanged_FiredOnCharacterCountChange()
    {
        var vm = CreateViewModel();
        bool fired = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TextProcessingViewModel.CharacterCount))
                fired = true;
        };

        vm.InputText = "test";
        Assert.True(fired);
    }

    [Fact]
    public void Models_Collection_InitiallyEmpty()
    {
        var vm = CreateViewModel();
        Assert.Empty(vm.Models);
    }

    [Fact]
    public void HasUnsavedContent_EmptyText_False()
    {
        var vm = CreateViewModel();
        Assert.False(vm.HasUnsavedContent());
    }

    [Fact]
    public void HasUnsavedContent_NonEmptyText_True()
    {
        var vm = CreateViewModel();
        vm.InputText = "Hello";
        Assert.True(vm.HasUnsavedContent());
    }

    [Fact]
    public void IsOverLimit_FalseForShortText()
    {
        var vm = CreateViewModel();
        vm.InputText = "Short text";
        Assert.False(vm.IsOverLimit);
    }
}
