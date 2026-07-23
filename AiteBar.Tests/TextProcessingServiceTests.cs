using AiteBar;

namespace AiteBar.Tests;

public sealed class TextProcessingServiceTests
{
    private readonly TextProcessingService _service = new();

    [Theory]
    [InlineData(TextProcessingMode.Proofread)]
    [InlineData(TextProcessingMode.Typography)]
    [InlineData(TextProcessingMode.Cleanup)]
    public void GetSystemPrompt_ReturnsNonEmptyPrompt(TextProcessingMode mode)
    {
        string prompt = _service.GetSystemPrompt(mode);
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    [Fact]
    public void GetSystemPrompt_ReturnsDifferentPromptsForDifferentModes()
    {
        string proofread = _service.GetSystemPrompt(TextProcessingMode.Proofread);
        string typography = _service.GetSystemPrompt(TextProcessingMode.Typography);
        string cleanup = _service.GetSystemPrompt(TextProcessingMode.Cleanup);

        Assert.NotEqual(proofread, typography);
        Assert.NotEqual(proofread, cleanup);
        Assert.NotEqual(typography, cleanup);
    }

    [Fact]
    public void BuildRequest_CreatesValidRequest()
    {
        var request = _service.BuildRequest(TextProcessingMode.Proofread, "Hello world");

        Assert.NotNull(request.Messages);
        Assert.Equal(2, request.Messages.Count);
        Assert.Equal("system", request.Messages[0].Role);
        Assert.Equal("user", request.Messages[1].Role);
        Assert.Contains("Hello world", request.Messages[1].Content);
        Assert.True(request.MaxOutputTokens >= 1024);
        Assert.True(request.RequiredContextTokens > request.MaxOutputTokens);
        Assert.Equal(0.3, request.Temperature);
    }

    [Fact]
    public void BuildRequest_EmptyText_CreatesValidRequest()
    {
        var request = _service.BuildRequest(TextProcessingMode.Cleanup, "");

        Assert.NotNull(request.Messages);
        Assert.Equal(2, request.Messages.Count);
    }

    [Fact]
    public void EstimateTokens_ReturnsPositiveForNonEmptyText()
    {
        int tokens = TextProcessingService.EstimateTokens("Hello world, this is a test.");
        Assert.True(tokens > 0);
    }

    [Fact]
    public void EstimateTokens_ReturnsZeroForEmptyText()
    {
        Assert.Equal(0, TextProcessingService.EstimateTokens(""));
        Assert.Equal(0, TextProcessingService.EstimateTokens((string?)null!));
    }

    [Fact]
    public void EstimateTokens_LongerTextReturnsMoreTokens()
    {
        int shortTokens = TextProcessingService.EstimateTokens("Hi");
        int longTokens = TextProcessingService.EstimateTokens("This is a much longer sentence with many more words in it.");
        Assert.True(longTokens > shortTokens);
    }

    [Fact]
    public void CleanResponse_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _service.CleanResponse(""));
        Assert.Equal(string.Empty, _service.CleanResponse("   "));
        Assert.Equal(string.Empty, _service.CleanResponse((string?)null!));
    }

    [Fact]
    public void CleanResponse_PlainText_ReturnsUnchanged()
    {
        string input = "This is plain text without any wrapping.";
        Assert.Equal(input, _service.CleanResponse(input));
    }

    [Fact]
    public void CleanResponse_StripsCodeFence()
    {
        string input = "```\nCorrected text here\n```";
        string result = _service.CleanResponse(input);
        Assert.Equal("Corrected text here", result);
    }

    [Fact]
    public void CleanResponse_StripsCodeFenceWithLanguageTag()
    {
        string input = "```\nSome result\n```";
        string result = _service.CleanResponse(input);
        Assert.Equal("Some result", result);
    }

    [Theory]
    [InlineData("Исправленный текст: Hello", "Hello")]
    [InlineData("Оформленный текст: World", "World")]
    [InlineData("Очищенный текст: Test", "Test")]
    [InlineData("Result: output", "output")]
    [InlineData("Corrected text: fix", "fix")]
    [InlineData("Formatted text: fmt", "fmt")]
    [InlineData("Cleaned text: clean", "clean")]
    public void CleanResponse_StripsServicePrefixes(string input, string expected)
    {
        string result = _service.CleanResponse(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("\"quoted text\"", "\"quoted text\"")]
    [InlineData("'single quoted'", "'single quoted'")]
    public void CleanResponse_SingleLineInQuotes_KeepsQuotes(string input, string expected)
    {
        string result = _service.CleanResponse(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CleanResponse_MultilineInQuotes_StripsQuotes()
    {
        string input = "\"line1\nline2\"";
        string result = _service.CleanResponse(input);
        Assert.Equal("line1\nline2", result);
    }

    [Fact]
    public void CleanResponse_WhitespaceAroundResult_IsTrimmed()
    {
        string input = "  \n  Cleaned text  \n  ";
        string result = _service.CleanResponse(input);
        Assert.Equal("Cleaned text", result);
    }

    [Fact]
    public void MaxInputLength_Is50000()
    {
        Assert.Equal(50_000, TextProcessingService.MaxInputLength);
    }

    [Fact]
    public void BuildRequest_ProofreadMode_UsesCorrectPrompt()
    {
        var request = _service.BuildRequest(TextProcessingMode.Proofread, "text");
        Assert.Contains("орфографи", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequest_TypographyMode_UsesCorrectPrompt()
    {
        var request = _service.BuildRequest(TextProcessingMode.Typography, "text");
        Assert.Contains("типографическ", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequest_CleanupMode_UsesCorrectPrompt()
    {
        var request = _service.BuildRequest(TextProcessingMode.Cleanup, "text");
        Assert.Contains("очистк", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequest_WithMaxOutputTokens_RespectsMinClamp()
    {
        // MaxOutputTokens is clamped to [1024, 32768], so 500 becomes 1024
        var request = _service.BuildRequest(TextProcessingMode.Proofread, "text", maxOutputTokens: 500);
        Assert.Equal(1024, request.MaxOutputTokens);
    }

    [Fact]
    public void BuildRequest_MaxOutputTokens_CannotExceed32768()
    {
        // For short text, outputBudget is small, so Clamp brings it to 1024 minimum
        var request = _service.BuildRequest(TextProcessingMode.Proofread, "text", maxOutputTokens: 50000);
        Assert.True(request.MaxOutputTokens <= 32768);
        Assert.True(request.MaxOutputTokens >= 1024);
    }

    [Fact]
    public void BuildRequest_LongText_IncreasesOutputBudget()
    {
        string shortText = "hi";
        string longText = new string('x', 10000);
        var shortReq = _service.BuildRequest(TextProcessingMode.Proofread, shortText);
        var longReq = _service.BuildRequest(TextProcessingMode.Proofread, longText);
        Assert.True(longReq.MaxOutputTokens >= shortReq.MaxOutputTokens);
        Assert.True(longReq.RequiredContextTokens > shortReq.RequiredContextTokens);
    }

    [Fact]
    public void CleanResponse_CodeFenceWithLanguageTag_StripsTag()
    {
        string input = "```python\ncode here\n```";
        string result = _service.CleanResponse(input);
        Assert.Equal("code here", result);
    }

    [Fact]
    public void CleanResponse_SingleLineNoNewline_KeepsQuotes()
    {
        string input = "\"just one line\"";
        string result = _service.CleanResponse(input);
        Assert.Equal("\"just one line\"", result);
    }

    [Fact]
    public void CleanResponse_GermanPrefixes_Stripped()
    {
        Assert.Equal("text", _service.CleanResponse("Korrigierter Text: text"));
        Assert.Equal("text", _service.CleanResponse("Formatierter Text: text"));
        Assert.Equal("text", _service.CleanResponse("Bereinigter Text: text"));
        Assert.Equal("text", _service.CleanResponse("Ergebnis: text"));
    }

    [Fact]
    public void CleanResponse_UkrainianPrefixes_Stripped()
    {
        Assert.Equal("text", _service.CleanResponse("Виправлений текст: text"));
        Assert.Equal("text", _service.CleanResponse("Оформлений текст: text"));
        Assert.Equal("text", _service.CleanResponse("Очищений текст: text"));
    }

    [Fact]
    public void CleanResponse_OnlyWhitespace_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _service.CleanResponse("\n\n\n"));
        Assert.Equal(string.Empty, _service.CleanResponse("\t\t"));
    }

    [Fact]
    public void EstimateTokens_TextLengthAffectsTokenCount()
    {
        string shortText = "hi";
        string longText = "This is a much longer sentence with many more words in it.";
        int shortTokens = TextProcessingService.EstimateTokens(shortText);
        int longTokens = TextProcessingService.EstimateTokens(longText);
        Assert.True(longTokens > shortTokens);
    }

    [Fact]
    public void GetSystemPrompt_ProofreadPrompt_ContainsProhibitions()
    {
        string prompt = _service.GetSystemPrompt(TextProcessingMode.Proofread);
        Assert.Contains("перефразировать", prompt);
        Assert.Contains("Верни только", prompt);
    }

    [Fact]
    public void GetSystemPrompt_TypographyPrompt_ContainsProhibitions()
    {
        string prompt = _service.GetSystemPrompt(TextProcessingMode.Typography);
        Assert.Contains("менять слова", prompt);
        Assert.Contains("Верни только", prompt);
    }

    [Fact]
    public void GetSystemPrompt_CleanupPrompt_ContainsProhibitions()
    {
        string prompt = _service.GetSystemPrompt(TextProcessingMode.Cleanup);
        Assert.Contains("исправлять орфографию", prompt);
        Assert.Contains("Верни только", prompt);
    }
}
