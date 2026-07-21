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

    [Theory]
    [InlineData("Привет мир", true)]
    [InlineData("", true)]
    public void FitsInContext_ShortTextFits(string text, bool expected)
    {
        bool result = _service.FitsInContext("system prompt", text, 100_000);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FitsInContext_NoContextLimit_AlwaysFits()
    {
        bool result = _service.FitsInContext("system", new string('x', 100_000), null);
        Assert.True(result);
    }

    [Fact]
    public void FitsInContext_TextExceedsLimit_ReturnsFalse()
    {
        bool result = _service.FitsInContext("system prompt", new string('x', 100_000), 1000);
        Assert.False(result);
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
}
