using AiteBar;

namespace AiteBar.Tests;

public sealed class PromptBuilderEvaluationCatalogTests
{
    [Fact]
    public void Catalog_CoversVisualTargetsAndCoreTaskDomains()
    {
        Assert.Contains(PromptBuilderEvaluationCatalog.Scenarios, scenario => scenario.VisualTarget == VisualTargetModel.GptImage);
        Assert.Contains(PromptBuilderEvaluationCatalog.Scenarios, scenario => scenario.VisualTarget == VisualTargetModel.Flux);
        Assert.Contains(PromptBuilderEvaluationCatalog.Scenarios, scenario => scenario.VisualTarget == VisualTargetModel.NanoBanana);
        Assert.Contains(PromptBuilderEvaluationCatalog.Scenarios, scenario => scenario.Category == PromptBuilderCategory.Programming);
        Assert.Contains(PromptBuilderEvaluationCatalog.Scenarios, scenario => scenario.Category == PromptBuilderCategory.Analysis);
        Assert.Contains(PromptBuilderEvaluationCatalog.Scenarios, scenario => scenario.Category == PromptBuilderCategory.Texts);
        Assert.Contains(PromptBuilderEvaluationCatalog.Scenarios, scenario => scenario.Category == PromptBuilderCategory.Video);
        Assert.Contains(PromptBuilderEvaluationCatalog.Scenarios, scenario => scenario.Category == PromptBuilderCategory.Music);
    }

    [Fact]
    public void EveryScenario_ProducesItsRequiredPromptContract()
    {
        var service = new PromptBuilderService();

        foreach (PromptBuilderEvaluationScenario scenario in PromptBuilderEvaluationCatalog.Scenarios)
        {
            AiChatRequest request = service.BuildRequest(
                scenario.Category,
                scenario.Brief,
                photoSection: scenario.PhotoSection,
                paintingStyle: scenario.PaintingStyle,
                animationStyle: scenario.AnimationStyle,
                photoStyle: scenario.PhotoStyle,
                textType: scenario.TextType,
                textTone: scenario.TextTone,
                analysisDirection: scenario.AnalysisDirection,
                videoDirection: scenario.VideoDirection,
                programmingTaskType: scenario.ProgrammingTaskType,
                programmingProjectType: scenario.ProgrammingProjectType,
                programmingStyle: scenario.ProgrammingStyle,
                visualTarget: scenario.VisualTarget);

            foreach (string fragment in scenario.RequiredSystemPromptFragments)
            {
                Assert.Contains(fragment, request.Messages[0].Content, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
