using System.IO;

namespace AiteBar.Tests;

public sealed class TextProcessingProtectionTests
{
    private readonly TextProcessingService _service = new();

    [Fact]
    public void ProtectAndRestoreTechnicalFragments_RoundTripsSupportedFragments()
    {
        const string source =
            "Открой https://example.com/a?q=1, напиши user@example.com и запусти `dotnet test`.\n" +
            "Файл C:\\Temp\\report.txt, версия v1.2.3 и ${HOME}. <strong>тег</strong>.\n" +
            "UUID 43b85022-3b6e-4681-92be-a5c003a30b77.\n```csharp\nvar x = 1;\n```";

        ProtectedText protectedText = _service.ProtectTechnicalFragments(source);

        Assert.DoesNotContain("https://example.com", protectedText.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("user@example.com", protectedText.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet test", protectedText.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("v1.2.3", protectedText.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("43b85022-3b6e-4681-92be-a5c003a30b77", protectedText.Text, StringComparison.Ordinal);
        Assert.Equal(source, TextProcessingService.RestoreTechnicalFragments(protectedText.Text, protectedText));
    }

    [Fact]
    public void RestoreTechnicalFragments_RestoresMarkersInsideChangedNaturalText()
    {
        ProtectedText protectedText = _service.ProtectTechnicalFragments("ошбка https://example.com");
        string changed = protectedText.Text.Replace("ошбка", "ошибка", StringComparison.Ordinal);

        Assert.Equal(
            "ошибка https://example.com",
            TextProcessingService.RestoreTechnicalFragments(changed, protectedText));
    }

    [Fact]
    public void ProtectTechnicalFragments_DoesNotChangePlainText()
    {
        ProtectedText protectedText = _service.ProtectTechnicalFragments("Обычный текст без технических фрагментов.");

        Assert.Empty(protectedText.Fragments);
        Assert.Equal("Обычный текст без технических фрагментов.", protectedText.Text);
    }

    [Fact]
    public void RestoreTechnicalFragments_RejectsMissingMarkerInFinalResponse()
    {
        ProtectedText protectedText = _service.ProtectTechnicalFragments("Открой https://example.com");

        Assert.Throws<InvalidDataException>(() =>
            TextProcessingService.RestoreTechnicalFragments(
                "Открой",
                protectedText,
                requireAllMarkers: true));
    }
}
