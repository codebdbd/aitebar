using AiteBar;

namespace AiteBar.Tests;

public sealed class PromptBuilderGoldenScenarioTests
{
    private readonly PromptBuilderService _service = new();

    [Fact]
    public void GrokImagine_ProductEdit_GoldenContractPreservesBriefAndLeavesFormatToInterface()
    {
        const string brief = """
            Replace the plain backdrop behind a matte-black wireless speaker with a sunlit concrete studio wall. Preserve the speaker's proportions, logo-free grille, material, and three-quarter product angle.
            """;

        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Images,
            brief,
            photoSection: PhotoSection.Product,
            photoStyle: PhotoStyle.TechProductPhoto,
            visualTarget: VisualTargetModel.GrokImagine);

        Assert.Equal(brief, request.Messages[1].Content);
        Assert.Equal(AiCapabilities.Text, request.RequiredCapabilities);
        Assert.True(request.RequireWritingModel);
        Assert.Equal(0.25, request.Temperature);

        string system = request.Messages[0].Content;
        Assert.Contains("Grok Imagine", system);
        Assert.Contains("Tech product photography", system);
        Assert.Contains("direct, fluent English description", system);
        Assert.Contains("preserve the unmentioned identity, proportions, pose, composition, and scene continuity", system);
        Assert.Contains("the interface supplies those settings", system);
        Assert.DoesNotContain("4:5", system, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("16:9", system, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1:1", system, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--ar", system, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Annie Leibovitz", system, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VideoAdvertising_GoldenContractPreservesChronologyAndNoFormatParameters()
    {
        const string brief = """
            A reusable steel water bottle stands on a rain-wet city bench at dawn. A hand picks it up, the water droplets slide down its surface, and the final shot holds on the bottle against the first sunlight.
            """;

        AiChatRequest request = _service.BuildRequest(
            PromptBuilderCategory.Video,
            brief,
            videoDirection: VideoDirection.Advertising);

        Assert.Equal(brief, request.Messages[1].Content);
        Assert.Equal(AiCapabilities.Text, request.RequiredCapabilities);
        Assert.True(request.RequireWritingModel);
        Assert.Equal(0.25, request.Temperature);

        string system = request.Messages[0].Content;
        Assert.Contains("Premium advertising spot", system);
        Assert.Contains("action and movement over time", system);
        Assert.Contains("Describe events in chronological order", system);
        Assert.Contains("what must change and what must remain unchanged", system);
        Assert.Contains("Avoid excessive motion", system);
        Assert.DoesNotContain("4:5", system, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("16:9", system, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1:1", system, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--ar", system, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not add generic quality filler or a generic negative prompt", system);
        Assert.DoesNotContain("Negative prompt:", system, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SunoMusicStyle_GoldenContractPreservesStyleFieldBoundaries()
    {
        const string brief = """
            Warm late-night electronic soul for a small rooftop party: 108 BPM, muted funk guitar, rounded analog bass, brushed percussion, intimate female vocal, and a hopeful chorus lift.
            """;

        AiChatRequest request = _service.BuildRequest(PromptBuilderCategory.Music, brief);

        Assert.Equal(brief, request.Messages[1].Content);
        Assert.Equal(AiCapabilities.Text, request.RequiredCapabilities);
        Assert.True(request.RequireWritingModel);
        Assert.Equal(0.30, request.Temperature);

        string system = request.Messages[0].Content;
        Assert.Contains("Suno Styles field", system);
        Assert.Contains("one compact but information-dense natural-language paragraph", system);
        Assert.Contains("BPM or tempo range when useful", system);
        Assert.Contains("Do not add lyrics or write what the singer literally says", system);
        Assert.Contains("Do not add artist names", system);
        Assert.Contains("Do not use visual terminology", system);
        Assert.DoesNotContain("aspect ratio", system, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("camera movement", system, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("song title:", system, StringComparison.OrdinalIgnoreCase);
    }
}
