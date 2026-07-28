using System.IO;
using System.Xml.Linq;

namespace AiteBar.Tests;

public sealed class QuickNoteFormattingControlsTests
{
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void HeadingCombo_ContainsBodyAndAllSixHeadingLevels()
    {
        XElement combo = FindCombo("CmbHeading");

        Assert.Equal(
            ["0", "1", "2", "3", "4", "5", "6"],
            combo.Elements(PresentationNamespace + "ComboBoxItem")
                .Select(item => item.Attribute("Tag")?.Value ?? string.Empty)
                .ToArray());
    }

    [Fact]
    public void ListCombo_ContainsBulletAndNumberedChoices()
    {
        XElement combo = FindCombo("CmbList");

        Assert.Equal(
            ["bullet", "numbered"],
            combo.Elements(PresentationNamespace + "ComboBoxItem")
                .Select(item => item.Attribute("Tag")?.Value ?? string.Empty)
                .ToArray());
    }

    [Fact]
    public void FormattingCombos_ResetAfterEachCommandSoTheSameChoiceCanBeUsedAgain()
    {
        Assert.Equal("-1", FindCombo("CmbHeading").Attribute("SelectedIndex")?.Value);
        Assert.Equal("-1", FindCombo("CmbList").Attribute("SelectedIndex")?.Value);

        string code = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "AiteBar",
            "QuickNoteWindow.xaml.cs"));
        Assert.Equal(2, code.Split("ResetFormatCombo(comboBox, -1);").Length - 1);
    }

    [Fact]
    public void Toolbar_KeepsCompactPrimaryCommandsAndOverflowMenu()
    {
        XDocument document = LoadDocument();
        XElement toolbar = document
            .Descendants(PresentationNamespace + "StackPanel")
            .Single(element => element.Elements(PresentationNamespace + "ComboBox")
                .Any(combo => (string?)combo.Attribute(XamlNamespace + "Name") == "CmbHeading"));

        Assert.Equal(5, toolbar.Elements(PresentationNamespace + "Button").Count());
        XElement overflow = toolbar.Elements(PresentationNamespace + "Button")
            .Single(button => button.Element(PresentationNamespace + "Button.ContextMenu") != null);
        string[] handlers = overflow
            .Descendants(PresentationNamespace + "MenuItem")
            .Select(item => (string?)item.Attribute("Click"))
            .Where(click => click != null)
            .Cast<string>()
            .ToArray();

        Assert.Equal(
            ["BtnUndo_Click", "BtnRedo_Click", "BtnUnderline_Click", "BtnCode_Click", "BtnClearFormatting_Click"],
            handlers);
    }

    private static XElement FindCombo(string name)
    {
        XDocument document = LoadDocument();
        return document
            .Descendants(PresentationNamespace + "ComboBox")
            .Single(element => (string?)element.Attribute(XamlNamespace + "Name") == name);
    }

    private static XDocument LoadDocument()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AiteBar", "QuickNoteWindow.xaml");
        return XDocument.Load(path);
    }
}
