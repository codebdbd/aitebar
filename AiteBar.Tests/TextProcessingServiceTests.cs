using AiteBar;

namespace AiteBar.Tests;

public sealed class TextProcessingServiceTests
{
    private readonly TextProcessingService _service = new();

    [Theory]
    [InlineData("Силиконовые соски болтались возле пупа Анастасии и теребонькали.", "Silicon nipples chattered near Anastasiya's belly button.", true)]
    [InlineData("Silicon nipples chattered near Anastasiya's belly button.", "Силиконовые соски болтались возле пупа Анастасии.", true)]
    [InlineData("Он сказал что вернется но не уточнил когда.", "Он сказал, что вернётся, но не уточнил, когда.", false)]
    [InlineData("Ми перевірили документ але помилку не знайшли.", "Ми перевірили документ, але помилку не знайшли.", false)]
    [InlineData("Он сказал \"я вернусь\" - и ушел...", "Он сказал «я вернусь» — и ушел…", false)]
    public void ViolatesContentPreservation_DetectsTranslationButAllowsModeEdits(
        string input,
        string output,
        bool expected)
    {
        Assert.Equal(expected, TextProcessingService.ViolatesContentPreservation(input, output));
    }

    [Fact]
    public void ViolatesContentPreservation_IgnoresSharedProtectedTechnicalFragments()
    {
        const string url = "https://example.com/very/long/technical/path/that/must/remain/unchanged";
        string input = $"Проверь этот текст возле адреса {url}.";
        string translated = $"Check this text near the address {url}.";

        Assert.True(TextProcessingService.ViolatesContentPreservation(input, translated, [url]));
    }

    [Fact]
    public void LiteraryEditing_AllowsControlledRewriteButStillRejectsTranslation()
    {
        const string input = "Старый дом стоял возле тихой реки.";
        const string edited = "Дом возвышался у спокойной водной глади.";
        const string translated = "The old house stood beside a quiet river.";
        double literaryOverlap = TextProcessingService.GetMinimumWordOverlap(
            TextProcessingMode.LiteraryEdit);

        Assert.True(TextProcessingService.ViolatesContentPreservation(input, edited));
        Assert.False(TextProcessingService.ViolatesContentPreservation(
            input,
            edited,
            minimumWordOverlap: literaryOverlap));
        Assert.True(TextProcessingService.ViolatesContentPreservation(
            input,
            translated,
            minimumWordOverlap: literaryOverlap));
    }

    [Fact]
    public void TextProcessingMode_AppendsStyleModesWithoutRenumberingExistingModes()
    {
        Assert.Equal(0, (int)TextProcessingMode.Proofread);
        Assert.Equal(1, (int)TextProcessingMode.Typography);
        Assert.Equal(2, (int)TextProcessingMode.Cleanup);
        Assert.Equal(3, (int)TextProcessingMode.LiteraryEdit);
        Assert.Equal(4, (int)TextProcessingMode.NaturalStyle);
    }

    [Fact]
    public void BuildRequest_ProofreadUsesOneSentenceWithoutAppendedContracts()
    {
        AiChatRequest request = _service.BuildRequest(TextProcessingMode.Proofread, "Текст");
        string systemPrompt = request.Messages[0].Content;

        Assert.Equal(_service.GetSystemPrompt(TextProcessingMode.Proofread), systemPrompt);
        Assert.StartsWith("Correct only spelling, grammar, and punctuation errors.", systemPrompt);
        Assert.Contains("Return only the corrected text", systemPrompt);
    }

    [Theory]
    [InlineData(TextProcessingMode.Proofread)]
    [InlineData(TextProcessingMode.Typography)]
    [InlineData(TextProcessingMode.Cleanup)]
    [InlineData(TextProcessingMode.LiteraryEdit)]
    [InlineData(TextProcessingMode.NaturalStyle)]
    public void GetSystemPrompt_ReturnsNonEmptyPrompt(TextProcessingMode mode)
    {
        string prompt = _service.GetSystemPrompt(mode);
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    [Fact]
    public void GetSystemPrompt_ReturnsDifferentPromptsForDifferentModes()
    {
        string[] prompts = Enum.GetValues<TextProcessingMode>()
            .Select(_service.GetSystemPrompt)
            .ToArray();

        Assert.Equal(prompts.Length, prompts.Distinct(StringComparer.Ordinal).Count());
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
        Assert.True(request.RequireFreeModel);
        Assert.True(request.RequireWritingModel);
        Assert.True(request.MaxOutputTokens >= 1024);
        Assert.True(request.RequiredContextTokens > request.MaxOutputTokens);
        Assert.Equal(0.0, request.Temperature);
    }

    [Theory]
    [InlineData(TextProcessingMode.Proofread, 0.0)]
    [InlineData(TextProcessingMode.Typography, 0.25)]
    [InlineData(TextProcessingMode.Cleanup, 0.1)]
    [InlineData(TextProcessingMode.LiteraryEdit, 0.4)]
    [InlineData(TextProcessingMode.NaturalStyle, 0.4)]
    public void BuildRequest_UsesModeSpecificTemperature(TextProcessingMode mode, double expected)
    {
        AiChatRequest request = _service.BuildRequest(mode, "Text");

        Assert.Equal(expected, request.Temperature);
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
    public void CleanResponse_PreservesCodeFence()
    {
        string input = "```\nCorrected text here\n```";
        string result = _service.CleanResponse(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void CleanResponse_PreservesCodeFenceWithoutLanguageTag()
    {
        string input = "```\nSome result\n```";
        string result = _service.CleanResponse(input);
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData("Исправленный текст: Hello")]
    [InlineData("Оформленный текст: World")]
    [InlineData("Очищенный текст: Test")]
    [InlineData("Result: output")]
    [InlineData("Corrected text: fix")]
    [InlineData("Formatted text: fmt")]
    [InlineData("Cleaned text: clean")]
    public void CleanResponse_PreservesServiceLikePrefixes(string input)
    {
        string result = _service.CleanResponse(input);
        Assert.Equal(input, result);
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
    public void CleanResponse_MultilineInQuotes_KeepsQuotes()
    {
        string input = "\"line1\nline2\"";
        string result = _service.CleanResponse(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void CleanResponse_WhitespaceAroundResult_IsTrimmed()
    {
        string input = "  \n  Cleaned text  \n  ";
        string result = _service.CleanResponse(input);
        Assert.Equal("Cleaned text", result);
    }

    [Theory]
    [InlineData("<think>internal reasoning</think>\nИсправленный текст.", "Исправленный текст.")]
    [InlineData("<thinking>internal reasoning</thinking>\nИсправленный текст.", "Исправленный текст.")]
    [InlineData("<analysis>internal reasoning</analysis>\nИсправленный текст.", "Исправленный текст.")]
    [InlineData("<reasoning>internal reasoning</reasoning>\nИсправленный текст.", "Исправленный текст.")]
    public void CleanResponse_RemovesClosedReasoningBlocks(string input, string expected)
    {
        Assert.Equal(expected, _service.CleanResponse(input));
    }

    [Fact]
    public void CleanResponse_UnclosedThinkBlock_RecoversExplicitFinalAnswer()
    {
        const string original = "Рыбак достает из реки толстую русалку";
        const string response =
            "Рыбак<think>\n" +
            "1. Analyze the input.\n" +
            "Final string: Рыбак достает из реки толстую русалку.\n" +
            "Output: matches response.\n";

        Assert.Equal(
            "Рыбак достает из реки толстую русалку.",
            _service.CleanResponse(response, original));
    }

    [Fact]
    public void CleanResponse_UnclosedReasoningWithoutRecoverableAnswer_ReturnsOriginalText()
    {
        const string original = "Исходный текст";
        Assert.Equal(
            original,
            _service.CleanResponse("Исх<think>служебное рассуждение", original));
    }

    [Fact]
    public void CleanResponse_UnclosedReasoningRejectsDistantExplicitAnswer()
    {
        const string original = "Проверь исходный текст без замены смысла.";
        const string response =
            "<think>служебное рассуждение\n" +
            "Final output: Совершенно другой ответ о посторонней теме.\n";

        Assert.Equal(original, _service.CleanResponse(response, original));
    }

    [Fact]
    public void HideReasoningFromStreamingPreview_HidesUnclosedReasoningTail()
    {
        Assert.Equal(
            "Готовый текст",
            TextProcessingService.HideReasoningFromStreamingPreview(
                "Готовый текст<think>служебное рассуждение"));
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
        Assert.StartsWith("Correct only spelling, grammar, and punctuation", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequest_TypographyMode_UsesCorrectPrompt()
    {
        var request = _service.BuildRequest(TextProcessingMode.Typography, "text");
        Assert.StartsWith("Apply typography only", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequest_CleanupMode_UsesCorrectPrompt()
    {
        var request = _service.BuildRequest(TextProcessingMode.Cleanup, "text");
        Assert.StartsWith("Remove only clear copy/paste", request.Messages[0].Content);
    }

    [Fact]
    public void BuildRequest_LiteraryEditMode_UsesCorrectSelfContainedPrompt()
    {
        var request = _service.BuildRequest(TextProcessingMode.LiteraryEdit, "text");
        string prompt = request.Messages[0].Content;

        Assert.Equal(_service.GetSystemPrompt(TextProcessingMode.LiteraryEdit), prompt);
        Assert.StartsWith("Edit the text for clarity, fluency, rhythm, and literary quality", prompt);
        Assert.Contains("Preserve the original language of every input segment", prompt);
        Assert.Contains("Tokens matching __AITEBAR_PROTECTED_...__", prompt);
    }

    [Fact]
    public void BuildRequest_EachModeUsesItsOwnSelfContainedPrompt()
    {
        string proofreadPrompt = _service.BuildRequest(TextProcessingMode.Proofread, "text").Messages[0].Content;
        string literaryPrompt = _service.BuildRequest(TextProcessingMode.LiteraryEdit, "text").Messages[0].Content;
        string naturalPrompt = _service.BuildRequest(TextProcessingMode.NaturalStyle, "text").Messages[0].Content;
        string typographyPrompt = _service.BuildRequest(TextProcessingMode.Typography, "text").Messages[0].Content;

        Assert.Equal(_service.GetSystemPrompt(TextProcessingMode.Proofread), proofreadPrompt);
        Assert.Equal(_service.GetSystemPrompt(TextProcessingMode.LiteraryEdit), literaryPrompt);
        Assert.Equal(_service.GetSystemPrompt(TextProcessingMode.NaturalStyle), naturalPrompt);
        Assert.Equal(_service.GetSystemPrompt(TextProcessingMode.Typography), typographyPrompt);
        Assert.DoesNotContain("if the requested transformation would require rewriting", literaryPrompt);
        Assert.DoesNotContain("if the requested transformation would require rewriting", naturalPrompt);
        Assert.Contains("You may rewrite awkward sentences", literaryPrompt);
        Assert.Contains("Rewrite the text so it sounds natural", naturalPrompt);
        Assert.Contains("Do not correct spelling or grammar, rewrite wording", typographyPrompt);
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
    public void CleanResponse_CodeFenceWithLanguageTag_PreservesTag()
    {
        string input = "```python\ncode here\n```";
        string result = _service.CleanResponse(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void CleanResponse_SingleLineNoNewline_KeepsQuotes()
    {
        string input = "\"just one line\"";
        string result = _service.CleanResponse(input);
        Assert.Equal("\"just one line\"", result);
    }

    [Fact]
    public void CleanResponse_GermanPrefixes_Preserved()
    {
        Assert.Equal("Korrigierter Text: text", _service.CleanResponse("Korrigierter Text: text"));
        Assert.Equal("Formatierter Text: text", _service.CleanResponse("Formatierter Text: text"));
        Assert.Equal("Bereinigter Text: text", _service.CleanResponse("Bereinigter Text: text"));
        Assert.Equal("Ergebnis: text", _service.CleanResponse("Ergebnis: text"));
    }

    [Fact]
    public void CleanResponse_UkrainianPrefixes_Preserved()
    {
        Assert.Equal("Виправлений текст: text", _service.CleanResponse("Виправлений текст: text"));
        Assert.Equal("Оформлений текст: text", _service.CleanResponse("Оформлений текст: text"));
        Assert.Equal("Очищений текст: text", _service.CleanResponse("Очищений текст: text"));
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
    public void GetSystemPrompt_ProofreadPrompt_IsOneDirectSentence()
    {
        string prompt = _service.GetSystemPrompt(TextProcessingMode.Proofread);
        Assert.Contains("Correct only", prompt);
        Assert.Contains("Return only the corrected text", prompt);
    }

    [Fact]
    public void GetSystemPrompt_TypographyPrompt_HasProfessionalScopeAndOutputContract()
    {
        string prompt = _service.GetSystemPrompt(TextProcessingMode.Typography);
        Assert.Contains("Apply typography only", prompt);
        Assert.Contains("Do not correct spelling or grammar", prompt);
        Assert.Contains("change paragraph boundaries", prompt);
        Assert.Contains("Return only the typographically formatted text", prompt);
    }

    [Fact]
    public void GetSystemPrompt_CleanupPrompt_HasProfessionalScopeAndOutputContract()
    {
        string prompt = _service.GetSystemPrompt(TextProcessingMode.Cleanup);
        Assert.Contains("Remove only clear copy/paste", prompt);
        Assert.Contains("more than twice", prompt);
        Assert.Contains("If a fragment is not clearly an artifact", prompt);
        Assert.Contains("Return only the cleaned text", prompt);
    }

    [Fact]
    public void GetSystemPrompt_LiteraryEditPrompt_AllowsStyleWorkWithoutInventingContent()
    {
        string prompt = _service.GetSystemPrompt(TextProcessingMode.LiteraryEdit);

        Assert.Contains("clarity, fluency, rhythm, and literary quality", prompt);
        Assert.Contains("rewrite awkward sentences", prompt);
        Assert.Contains("do not invent information", prompt);
        Assert.Contains("Return only the edited text", prompt);
    }

    [Fact]
    public void GetSystemPrompt_NaturalStylePrompt_RemovesPatternsWithoutChangingMeaning()
    {
        string prompt = _service.GetSystemPrompt(TextProcessingMode.NaturalStyle);

        Assert.Contains("natural, lively, and human", prompt);
        Assert.Contains("restrained typography", prompt);
        Assert.Contains("Vary sentence length and rhythm", prompt);
        Assert.Contains("formulaic phrases", prompt);
        Assert.Contains("unnaturally uniform paragraph structure", prompt);
        Assert.Contains("preserving its original language, meaning, facts", prompt);
        Assert.Contains("Do not add new ideas", prompt);
        Assert.Contains("Return only the revised text", prompt);
        Assert.Equal(0.15, TextProcessingService.GetMinimumWordOverlap(TextProcessingMode.NaturalStyle));
    }

    [Theory]
    [InlineData(TextProcessingMode.Proofread)]
    [InlineData(TextProcessingMode.Typography)]
    [InlineData(TextProcessingMode.Cleanup)]
    [InlineData(TextProcessingMode.LiteraryEdit)]
    [InlineData(TextProcessingMode.NaturalStyle)]
    public void BuildRequest_SystemInstructionsContainNoCyrillic(TextProcessingMode mode)
    {
        string prompt = _service.BuildRequest(mode, "Пользовательский текст").Messages[0].Content;

        Assert.DoesNotContain(prompt, character => character is >= '\u0400' and <= '\u052F');
    }
}
