using System.IO;
using System.Xml.Linq;

namespace AiteBar.Tests;

public sealed class QuickNoteFormattingControlsTests
{
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void Toolbar_ContainsDirectFormattingAndImageButtonsInOrder()
    {
        XElement toolbar = FindFormattingToolbar();

        string[] handlers = toolbar.Elements(PresentationNamespace + "Button")
            .Select(button => (string?)button.Attribute("Click"))
            .Where(click => click != null)
            .Cast<string>()
            .ToArray();

        Assert.Equal(
            [
                "BtnDecreaseFontSize_Click",
                "BtnIncreaseFontSize_Click",
                "BtnBullet_Click",
                "BtnNumbered_Click",
                "BtnBold_Click",
                "BtnItalic_Click",
                "BtnStrikethrough_Click",
                "BtnInsertLink_Click",
                "BtnInsertImage_Click",
                "BtnUnderline_Click",
                "BtnCode_Click",
                "BtnClearFormatting_Click"
            ],
            handlers);
    }

    [Fact]
    public void Toolbar_DoesNotUseDropdownsOrOverflowMenu()
    {
        XDocument document = LoadDocument();

        Assert.DoesNotContain(document.Descendants(PresentationNamespace + "ComboBox"), static _ => true);
        Assert.DoesNotContain(document.Descendants(PresentationNamespace + "Style"),
            static style => (string?)style.Attribute(XamlNamespace + "Key") == "FormatComboStyle");
        Assert.DoesNotContain(document.Descendants(PresentationNamespace + "Button"),
            static button => (string?)button.Attribute("Content") == "⋯");
    }

    [Fact]
    public void Toolbar_ListButtonsUseStableMaterialListGlyphs()
    {
        XElement toolbar = FindFormattingToolbar();
        Dictionary<string, XElement> buttonsByHandler = toolbar.Elements(PresentationNamespace + "Button")
            .Where(button => button.Attribute("Click") != null)
            .ToDictionary(button => button.Attribute("Click")!.Value);

        Assert.Equal("pack://application:,,,/Resources/#Material Icons", buttonsByHandler["BtnBullet_Click"].Attribute("FontFamily")?.Value);
        Assert.Equal("\uE241", buttonsByHandler["BtnBullet_Click"].Attribute("Content")?.Value);
        Assert.Null(buttonsByHandler["BtnNumbered_Click"].Attribute("FontFamily"));
        Assert.Equal("\uF7FA", buttonsByHandler["BtnNumbered_Click"].Attribute("Content")?.Value);
    }

    [Fact]
    public void Window_UsesSquareEdgeToEdgeNoteBorder()
    {
        XDocument document = LoadDocument();
        XElement chrome = document.Descendants(PresentationNamespace + "WindowChrome").Single();
        XElement outerBorder = document.Descendants(PresentationNamespace + "Grid")
            .First()
            .Elements(PresentationNamespace + "Border")
            .Single();

        Assert.Equal("0", chrome.Attribute("CornerRadius")?.Value);
        Assert.Equal("0", outerBorder.Attribute("Margin")?.Value);
        Assert.Equal("0", outerBorder.Attribute("CornerRadius")?.Value);
        Assert.Equal("1", outerBorder.Attribute("BorderThickness")?.Value);
    }

    [Fact]
    public void ToolbarAndWindowButtons_HaveAutomationNames()
    {
        XDocument document = LoadDocument();
        XElement[] buttons = document.Descendants(PresentationNamespace + "Button").ToArray();
        XElement[] formattingButtons = FindFormattingToolbar().Elements(PresentationNamespace + "Button").ToArray();

        Assert.All(formattingButtons, button => Assert.NotNull(button.Attribute("AutomationProperties.Name")));
        Assert.NotNull(document.Descendants(PresentationNamespace + "ToggleButton")
            .Single(button => (string?)button.Attribute(XamlNamespace + "Name") == "BtnPin")
            .Attribute("AutomationProperties.Name"));
        Assert.Contains(buttons, button =>
            (string?)button.Attribute("Click") == "BtnClose_Click" && button.Attribute("AutomationProperties.Name") != null);
    }

    [Fact]
    public void EditorContextMenu_ContainsNoteCommandsAfterStandardTextCommands()
    {
        XElement contextMenu = FindEditorContextMenu();
        string[] directHandlers = contextMenu.Elements(PresentationNamespace + "MenuItem")
            .Select(item => (string?)item.Attribute("Click"))
            .Where(click => click != null)
            .Cast<string>()
            .ToArray();

            Assert.Equal(
            [
                "BtnTheme_Click",
                "BtnOpenFile_Click",
                "BtnClear_Click"
            ],
            directHandlers);
    }

    [Fact]
    public void EditorContextMenu_DoesNotContainHiddenFormattingOrConflictCopyCommands()
    {
        XElement contextMenu = FindEditorContextMenu();

        Assert.DoesNotContain(contextMenu.Descendants(PresentationNamespace + "MenuItem"),
            static item => (string?)item.Attribute("Header") == "{local:Loc ResourceKey=QuickNote_MoreFormatting}");
        Assert.DoesNotContain(contextMenu.Descendants(PresentationNamespace + "MenuItem"),
            static item => (string?)item.Attribute(XamlNamespace + "Name") == "MenuOpenConflictCopy");
    }

    [Fact]
    public void EditorLayout_UsesCompactHorizontalInsets()
    {
        XDocument document = LoadDocument();
        XElement editorContainer = document
            .Descendants(PresentationNamespace + "Border")
            .Single(border => (string?)border.Attribute("Grid.Row") == "1");

        Assert.Equal("8,2,4,0", (string?)editorContainer.Attribute("Margin"));
    }

    [Fact]
    public void WindowChrome_UsesAccentHeaderAndBottomFormattingToolbar()
    {
        XDocument document = LoadDocument();
        XElement header = document.Descendants(PresentationNamespace + "Border")
            .Single(border => (string?)border.Attribute(XamlNamespace + "Name") == "HeaderBar");
        XElement toolbar = FindFormattingToolbar();
        XElement footer = document.Descendants(PresentationNamespace + "Border")
            .Single(border => (string?)border.Attribute(XamlNamespace + "Name") == "FooterBar");

        Assert.Equal("0", (string?)header.Attribute("Grid.Row"));
        Assert.Equal("2", (string?)toolbar.Parent?.Attribute("Grid.Row"));
        Assert.Equal("3", (string?)footer.Attribute("Grid.Row"));
        Assert.Equal("Left", (string?)document.Descendants(PresentationNamespace + "ToggleButton")
            .Single(button => (string?)button.Attribute(XamlNamespace + "Name") == "BtnPin")
            .Attribute("HorizontalAlignment"));
        Assert.DoesNotContain(document.Descendants(PresentationNamespace + "Rectangle"),
            rectangle => ((string?)rectangle.Attribute(XamlNamespace + "Name"))?.StartsWith("FormatSeparator", StringComparison.Ordinal) == true);
    }

    private static XElement FindFormattingToolbar()
    {
        XDocument document = LoadDocument();
        return document
            .Descendants(PresentationNamespace + "StackPanel")
            .Single(element => element.Elements(PresentationNamespace + "Button")
                .Any(button => (string?)button.Attribute("Click") == "BtnDecreaseFontSize_Click"));
    }

    private static XElement FindEditorContextMenu()
    {
        XDocument document = LoadDocument();
        XElement editor = document
            .Descendants(PresentationNamespace + "RichTextBox")
            .Single(element => (string?)element.Attribute(XamlNamespace + "Name") == "TxtNote");

        return editor
            .Element(PresentationNamespace + "RichTextBox.ContextMenu")!
            .Element(PresentationNamespace + "ContextMenu")!;
    }

    private static XDocument LoadDocument()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AiteBar", "QuickNoteWindow.xaml");
        return XDocument.Load(path);
    }
}
