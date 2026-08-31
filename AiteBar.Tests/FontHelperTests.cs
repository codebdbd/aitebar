using AiteBar;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AiteBar.Tests;

public sealed class FontHelperTests
{
    [Fact]
    public void MaterialKey_IsExpectedValue()
    {
        Assert.Equal("Material Icons", FontHelper.MaterialKey);
    }

    [Fact]
    public void MaterialFont_RemainsBundledForLegacyUserIcons()
    {
        string project = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "AiteBar.csproj"));

        Assert.Contains("Resources\\MaterialIcons-Regular.ttf", project);
        Assert.Contains("MaterialKey => _materialFont", File.ReadAllText(
            Path.Combine(FindRepoRoot(), "AiteBar", "FontHelper.cs")));
    }

    [Fact]
    public void ApplicationUi_UsesFluentInsteadOfLegacySystemIconFonts()
    {
        string appDirectory = Path.Combine(FindRepoRoot(), "AiteBar");
        string[] violations = Directory
            .EnumerateFiles(appDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith($"{Path.DirectorySeparatorChar}FontHelper.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                string source = File.ReadAllText(path);
                return source.Contains("#Material Icons", StringComparison.Ordinal) ||
                       source.Contains("FontHelper.MaterialKey", StringComparison.Ordinal) ||
                       source.Contains("Segoe MDL2 Assets", StringComparison.Ordinal) ||
                       source.Contains("Text=\"🔍\"", StringComparison.Ordinal) ||
                       source.Contains("Text=\"✓\"", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(appDirectory, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void FluentKey_IsExpectedValue()
    {
        Assert.Equal("Fluent System Icons", FontHelper.FluentKey);
    }

    [Fact]
    public void BrandsKey_IsExpectedValue()
    {
        Assert.Equal("Font Awesome Brands", FontHelper.BrandsKey);
    }

    [Fact]
    public void FluentCodepointsResource_IsExpectedValue()
    {
        Assert.Equal("pack://application:,,,/Resources/FluentSystemIcons.json", FontHelper.FluentCodepointsResource);
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "AiteBar.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root with AiteBar.sln was not found.");
    }
}
