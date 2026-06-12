using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AiteBar.Tests;

public sealed class FileSorterWindowLayoutTests
{
    [Fact]
    public void FileSorterHeader_CloseButtonIsNotClippedAndHasNoFocusOutlineTrigger()
    {
        string repoRoot = FindRepoRoot();
        XDocument document = XDocument.Load(Path.Combine(repoRoot, "AiteBar", "FileSorterWindow.xaml"));

        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        XElement closeButton = Assert.Single(
            document.Descendants(presentation + "Button"),
            element => string.Equals(element.Attribute(x + "Name")?.Value, "BtnClose", StringComparison.Ordinal));

        Assert.Equal("Center", closeButton.Attribute("VerticalAlignment")?.Value);

        XElement firstRowDefinition = document
            .Descendants(presentation + "RowDefinition")
            .First();
        Assert.Equal("Auto", firstRowDefinition.Attribute("Height")?.Value);

        string xaml = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "FileSorterWindow.xaml"));
        Assert.DoesNotContain("<Trigger Property=\"IsKeyboardFocused\" Value=\"True\">", xaml, StringComparison.Ordinal);
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
