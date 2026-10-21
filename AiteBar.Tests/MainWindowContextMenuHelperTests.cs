using System.Collections.Generic;
using Xunit;

namespace AiteBar.Tests;

public class MainWindowContextMenuHelperTests
{
    [Fact]
    public void BuildDuplicateElementName_FirstCopy_AppendsCopySuffix()
    {
        string suffix = LocalizationService.Get("Element_CopySuffix");
        string result = MainWindow.BuildDuplicateElementName("Test Button", new List<string> { "Test Button" });
        Assert.Equal($"Test Button ({suffix})", result);
    }

    [Fact]
    public void BuildDuplicateElementName_SecondCopy_AppendsNumberedCopySuffix()
    {
        string suffix = LocalizationService.Get("Element_CopySuffix");
        string suffix2 = LocalizationService.Format("Element_CopySuffixFormat", 2);
        var existing = new List<string> { "Test Button", $"Test Button ({suffix})" };
        string result = MainWindow.BuildDuplicateElementName("Test Button", existing);
        Assert.Equal($"Test Button ({suffix2})", result);
    }

    [Fact]
    public void BuildDuplicateElementName_EmptySource_UsesDefaultButtonName()
    {
        string defaultName = LocalizationService.Get("Element_NewButton");
        string suffix = LocalizationService.Get("Element_CopySuffix");
        string result = MainWindow.BuildDuplicateElementName("", new List<string>());
        Assert.Equal($"{defaultName} ({suffix})", result);
    }

    [Theory]
    [InlineData("Program", true)]
    [InlineData("ScriptFile", true)]
    [InlineData("Web", false)]
    [InlineData("Folder", false)]
    [InlineData("Command", false)]
    [InlineData("InvalidType", false)]
    public void CanRunAsAdmin_EvaluatesActionTypeCorrectly(string actionType, bool expected)
    {
        var element = new CustomElement { ActionType = actionType };
        bool canRun = MainWindow.CanRunAsAdmin(element);
        Assert.Equal(expected, canRun);
    }

    [Theory]
    [InlineData("Program", true)]
    [InlineData("File", true)]
    [InlineData("Folder", true)]
    [InlineData("ScriptFile", true)]
    [InlineData("Web", false)]
    [InlineData("Command", false)]
    [InlineData("InvalidType", false)]
    public void CanOpenElementLocation_EvaluatesActionTypeCorrectly(string actionType, bool expected)
    {
        var element = new CustomElement { ActionType = actionType };
        bool canOpen = MainWindow.CanOpenElementLocation(element);
        Assert.Equal(expected, canOpen);
    }

    [Fact]
    public void TryGetCopyValue_ValidTypes_ReturnsTrueAndCorrectValue()
    {
        var webElement = new CustomElement { ActionType = "Web", ActionValue = "https://example.com" };
        Assert.True(MainWindow.TryGetCopyValue(webElement, out string caption, out string value));
        Assert.Equal("https://example.com", value);
        Assert.False(string.IsNullOrWhiteSpace(caption));

        var cmdElement = new CustomElement { ActionType = "Command", ActionValue = "echo 1" };
        Assert.True(MainWindow.TryGetCopyValue(cmdElement, out caption, out value));
        Assert.Equal("echo 1", value);

        var pathElement = new CustomElement { ActionType = "Folder", ActionValue = @"C:\Tools" };
        Assert.True(MainWindow.TryGetCopyValue(pathElement, out caption, out value));
        Assert.Equal(@"C:\Tools", value);
    }

    [Fact]
    public void TryGetCopyValue_EmptyValueOrInvalidType_ReturnsFalse()
    {
        var emptyElement = new CustomElement { ActionType = "Web", ActionValue = "  " };
        Assert.False(MainWindow.TryGetCopyValue(emptyElement, out _, out _));

        var invalidElement = new CustomElement { ActionType = "Unknown", ActionValue = "test" };
        Assert.False(MainWindow.TryGetCopyValue(invalidElement, out _, out _));
    }
}
