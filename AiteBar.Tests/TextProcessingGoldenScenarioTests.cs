using AiteBar;

namespace AiteBar.Tests;

public sealed class TextProcessingGoldenScenarioTests
{
    private readonly TextProcessingService _service = new();

    [Fact]
    public void ProofreadMarkdownWithCode_GoldenContractPreservesTechnicalContent()
    {
        const string text = "Исправь текст в `README.md`, но не меняй команду `dotnet test`. Подробнее: https://example.com/docs.";

        AiChatRequest request = _service.BuildRequest(TextProcessingMode.Proofread, text);

        Assert.Equal(text, request.Messages[1].Content);
        Assert.Equal(0.0, request.Temperature);
        Assert.True(request.RequireWritingModel);
        string system = request.Messages[0].Content;
        Assert.Contains("Correct only spelling, grammar, and punctuation errors", system);
        Assert.Contains("Markdown syntax", system);
        Assert.Contains("Tokens matching __AITEBAR_PROTECTED_...__", system);
        Assert.Contains("Return only the corrected text", system);
    }

    [Fact]
    public void TypographyMultilingualHtml_GoldenContractForbidsTranslationAndRewriting()
    {
        const string text = "<p>Привіт, world! Цена 1 000 грн.</p>";

        AiChatRequest request = _service.BuildRequest(TextProcessingMode.Typography, text);

        Assert.Equal(text, request.Messages[1].Content);
        Assert.Equal(0.25, request.Temperature);
        string system = request.Messages[0].Content;
        Assert.Contains("using the conventions of each language present", system);
        Assert.Contains("never translate any part of the text", system);
        Assert.Contains("Do not correct spelling or grammar, rewrite wording", system);
        Assert.Contains("HTML/XML tags", system);
        Assert.Contains("Return only the typographically formatted text", system);
    }

    [Fact]
    public void NaturalStyleReasoningOutput_GoldenContractRestoresSafeFinalText()
    {
        const string source = "Команда завершила проверку и подготовила отчет.";
        const string rawResponse = "<think>служебное рассуждение</think>Команда завершила проверку и подготовила отчёт.";

        AiChatRequest request = _service.BuildRequest(TextProcessingMode.NaturalStyle, source);
        string cleaned = _service.CleanResponse(rawResponse, source);

        Assert.Equal(source, request.Messages[1].Content);
        Assert.Equal(0.4, request.Temperature);
        Assert.Equal("Команда завершила проверку и подготовила отчёт.", cleaned);
        Assert.Contains("Do not add new ideas, claims, examples", request.Messages[0].Content);
        Assert.Contains("Return only the revised text", request.Messages[0].Content);
    }
}
