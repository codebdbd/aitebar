using System.IO;
using System.Xml.Linq;

namespace AiteBar.Tests;

public sealed class ComboBoxPopupChromeTests
{
    [Theory]
    [InlineData(300, 100, true)]
    [InlineData(100, 136, false)]
    public void IsPopupAbove_UsesActualPopupPosition(
        double comboTop,
        double popupTop,
        bool expected)
    {
        Assert.Equal(expected, ComboBoxPopupChrome.IsPopupAbove(comboTop, popupTop));
    }

    [Fact]
    public void BaseStyle_SwapsJoinedCornersWhenPopupOpensAbove()
    {
        XDocument resources = XDocument.Load(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "FormControlsResources.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        XElement style = Assert.Single(resources.Descendants(presentation + "Style"),
            element => element.Attribute(xaml + "Key")?.Value == "BaseComboBoxStyle");
        XElement aboveTrigger = Assert.Single(style.Descendants(presentation + "MultiTrigger"),
            trigger => trigger.Descendants(presentation + "Condition").Any(condition =>
                condition.Attribute("Property")?.Value == "local:ComboBoxPopupChrome.OpensAbove"));

        Assert.Contains(aboveTrigger.Descendants(presentation + "Setter"), setter =>
            setter.Attribute("TargetName")?.Value == "Outline" &&
            setter.Attribute("Property")?.Value == "CornerRadius" &&
            setter.Attribute("Value")?.Value == "0,0,4,4");
        Assert.Contains(aboveTrigger.Descendants(presentation + "Setter"), setter =>
            setter.Attribute("TargetName")?.Value == "DropDownBorder" &&
            setter.Attribute("Property")?.Value == "CornerRadius" &&
            setter.Attribute("Value")?.Value == "4,4,0,0");
        Assert.Contains(aboveTrigger.Descendants(presentation + "Setter"), setter =>
            setter.Attribute("TargetName")?.Value == "DropDownBorder" &&
            setter.Attribute("Property")?.Value == "BorderThickness" &&
            setter.Attribute("Value")?.Value == "1,1,1,0");
    }

    [Fact]
    public void BaseStyle_UsesPixelScrollingWithoutTrailingPartialRowGap()
    {
        XDocument resources = XDocument.Load(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "FormControlsResources.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        XElement style = Assert.Single(resources.Descendants(presentation + "Style"),
            element => element.Attribute(xaml + "Key")?.Value == "BaseComboBoxStyle");
        XElement popupScrollViewer = Assert.Single(style.Descendants(presentation + "ScrollViewer"),
            element => element.Attribute("CanContentScroll") != null);

        Assert.Equal("False", popupScrollViewer.Attribute("CanContentScroll")?.Value);
    }

    [Fact]
    public void BaseStyle_ConstrainsPopupWidthToTheOwningComboBox()
    {
        XDocument resources = XDocument.Load(Path.Combine(
            FindRepoRoot(),
            "AiteBar",
            "FormControlsResources.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        XElement style = Assert.Single(resources.Descendants(presentation + "Style"),
            element => element.Attribute(xaml + "Key")?.Value == "BaseComboBoxStyle");
        XElement dropDown = Assert.Single(style.Descendants(presentation + "Border"),
            element => element.Attribute(xaml + "Name")?.Value == "DropDownBorder");

        Assert.Equal("{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}", dropDown.Attribute("Width")?.Value);
        Assert.Null(dropDown.Attribute("MinWidth"));
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

        throw new DirectoryNotFoundException(
            "Repository root with AiteBar.sln was not found.");
    }
}
