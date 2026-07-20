using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class FormControlHeightTests
{
    private const string HeightResourceReference = "{StaticResource FormControlHeight}";
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void SharedFormStyles_UseSingle36PixelHeightToken()
    {
        XDocument resources = LoadXaml("FormControlsResources.xaml");
        XElement height = Assert.Single(
            resources.Root!.Elements(),
            element => string.Equals(element.Attribute(XamlNamespace + "Key")?.Value, "FormControlHeight", StringComparison.Ordinal));

        Assert.Equal("36", height.Value);
        AssertSetter(FindStyle(resources, "BaseTextBoxStyle"), "Height", HeightResourceReference);
        AssertSetter(FindStyle(resources, "BaseComboBoxStyle"), "Height", HeightResourceReference);

        XElement selectionButtonStyle = FindStyle(resources, "FormSelectionButtonStyle");
        Assert.Equal("{StaticResource SecondaryButtonStyle}", selectionButtonStyle.Attribute("BasedOn")?.Value);
        AssertSetter(selectionButtonStyle, "Height", HeightResourceReference);

        AssertSetter(FindStyle(resources, "IconButtonStyle"), "Height", "32");
        AssertSetter(FindStyle(resources, "ToolbarButtonStyle"), "Height", "30");
    }

    [Fact]
    public void TextInputs_UseDedicatedDarkBackgroundAndCompactPadding()
    {
        XDocument resources = LoadXaml("FormControlsResources.xaml");
        XElement inputBackground = Assert.Single(
            resources.Root!.Elements(PresentationNamespace + "SolidColorBrush"),
            element => string.Equals(element.Attribute(XamlNamespace + "Key")?.Value, "FormInputBackground", StringComparison.Ordinal));
        XElement inputHoverBackground = Assert.Single(
            resources.Root.Elements(PresentationNamespace + "SolidColorBrush"),
            element => string.Equals(element.Attribute(XamlNamespace + "Key")?.Value, "FormInputBackgroundHover", StringComparison.Ordinal));
        XElement placeholderMargin = Assert.Single(
            resources.Root.Elements(PresentationNamespace + "Thickness"),
            element => string.Equals(element.Attribute(XamlNamespace + "Key")?.Value, "FormInputPlaceholderMargin", StringComparison.Ordinal));

        Assert.Equal("#2D2D2D", inputBackground.Attribute("Color")?.Value);
        Assert.Equal("#2D2D2D", inputHoverBackground.Attribute("Color")?.Value);
        Assert.Equal("8,0,0,0", placeholderMargin.Value);

        XElement textBoxStyle = FindStyle(resources, "BaseTextBoxStyle");
        AssertSetter(textBoxStyle, "Background", "{StaticResource FormInputBackground}");
        AssertSetter(textBoxStyle, "Padding", "4,0");

        XElement hoverTrigger = Assert.Single(
            textBoxStyle.Descendants(PresentationNamespace + "Trigger"),
            trigger =>
                string.Equals(trigger.Attribute("Property")?.Value, "IsMouseOver", StringComparison.Ordinal) &&
                string.Equals(trigger.Attribute("Value")?.Value, "True", StringComparison.Ordinal));
        XElement hoverBackgroundSetter = Assert.Single(
            hoverTrigger.Elements(PresentationNamespace + "Setter"),
            setter =>
                string.Equals(setter.Attribute("TargetName")?.Value, "Chrome", StringComparison.Ordinal) &&
                string.Equals(setter.Attribute("Property")?.Value, "Background", StringComparison.Ordinal));
        Assert.Equal("{StaticResource FormInputBackgroundHover}", hoverBackgroundSetter.Attribute("Value")?.Value);

        XElement iconPickerSearchStyle = FindStyle(LoadXaml("IconPickerWindow.xaml"), "SearchTextBoxStyle");
        Assert.DoesNotContain(
            iconPickerSearchStyle.Elements(PresentationNamespace + "Setter"),
            setter => string.Equals(setter.Attribute("Property")?.Value, "Background", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("IconConverterWindow.xaml", "PanelBorderStyle")]
    [InlineData("QRCodeGeneratorWindow.xaml", "PanelBorderStyle")]
    [InlineData("QRCodeGeneratorWindow.xaml", "InspectorSectionStyle")]
    public void ConverterAndQrPanels_UseRequested192A41Background(string fileName, string styleKey)
    {
        AssertSetter(FindStyle(LoadXaml(fileName), styleKey), "Background", "#192A41");
    }

    [Theory]
    [InlineData("QRCodeGeneratorWindow.xaml", "TxtInputPlaceholder")]
    [InlineData("ClipboardManagerWindow.xaml", "TxtSearchPlaceholder")]
    [InlineData("RotationProfileSelectionWindow.xaml", "TxtSearchPlaceholder")]
    [InlineData("SettingsWindow.xaml", "TxtNamePlaceholder")]
    [InlineData("SettingsWindow.xaml", "TxtActionPlaceholder")]
    [InlineData("SettingsWindow.xaml", "TxtHexPlaceholder")]
    public void TextInputPlaceholder_UsesCaretAlignedSharedMargin(string fileName, string elementName)
    {
        XDocument window = LoadXaml(fileName);
        XElement placeholder = Assert.Single(
            window.Descendants(PresentationNamespace + "TextBlock"),
            element => string.Equals(element.Attribute(XamlNamespace + "Name")?.Value, elementName, StringComparison.Ordinal));

        Assert.Equal("{StaticResource FormInputPlaceholderMargin}", placeholder.Attribute("Margin")?.Value);
    }

    [Fact]
    public void DynamicContextRows_UseSharedFormControlHeightInsteadOfClippingAt34()
    {
        string code = File.ReadAllText(Path.Combine(FindRepoRoot(), "AiteBar", "AppSettingsWindow.xaml.cs"));

        Assert.Contains("double formControlHeight = (double)FindResource(\"FormControlHeight\");", code);
        Assert.Contains("new Grid { Height = formControlHeight", code);
        Assert.DoesNotContain("new Grid { Height = 34", code);
    }

    [Fact]
    public void TimerPresetArea_FitsTwo36PixelButtonsAndTheirMargins()
    {
        XDocument timer = LoadXaml("TimerStopwatchWindow.xaml");
        XElement outerPresetRow = Assert.Single(
            timer.Descendants(PresentationNamespace + "RowDefinition"),
            row => string.Equals(row.Attribute(XamlNamespace + "Name")?.Value, "PresetRows", StringComparison.Ordinal));
        XElement timerView = Assert.Single(
            timer.Descendants(PresentationNamespace + "Grid"),
            grid => string.Equals(grid.Attribute(XamlNamespace + "Name")?.Value, "TimerView", StringComparison.Ordinal));
        XElement[] timerRows = Assert.Single(timerView.Elements(PresentationNamespace + "Grid.RowDefinitions"))
            .Elements(PresentationNamespace + "RowDefinition")
            .ToArray();
        XElement presetGrid = Assert.Single(
            timerView.Descendants(PresentationNamespace + "UniformGrid"),
            grid => string.Equals(grid.Attribute(XamlNamespace + "Name")?.Value, "PresetButtonsGrid", StringComparison.Ordinal));

        Assert.Equal("88", outerPresetRow.Attribute("Height")?.Value);
        Assert.Equal("88", timerRows[1].Attribute("Height")?.Value);
        Assert.Equal("2", presetGrid.Attribute("Rows")?.Value);
        AssertSetter(FindStyle(timer, "PresetButtonStyle"), "Height", HeightResourceReference);
        AssertSetter(FindStyle(timer, "PresetButtonStyle"), "Margin", "4");
    }

    [Theory]
    [InlineData("SettingsResources.xaml", "SegmentedRadioButtonStyle")]
    [InlineData("AppSettingsWindow.xaml", "HotkeyModifierButtonStyle")]
    [InlineData("QRCodeGeneratorWindow.xaml", "CompactComboStyle")]
    [InlineData("QuickNoteWindow.xaml", "FormatComboStyle")]
    [InlineData("IconPickerWindow.xaml", "SearchTextBoxStyle")]
    [InlineData("IconConverterWindow.xaml", "OptionRadioButtonStyle")]
    [InlineData("TimerStopwatchWindow.xaml", "ModeButtonStyle")]
    [InlineData("TimerStopwatchWindow.xaml", "PresetButtonStyle")]
    public void SpecializedFormStyle_UsesSharedHeight(string fileName, string styleKey)
    {
        AssertSetter(FindStyle(LoadXaml(fileName), styleKey), "Height", HeightResourceReference);
    }

    [Theory]
    [InlineData("BtnBrowse")]
    [InlineData("BtnRotationProfiles")]
    [InlineData("BtnOpenCatalog")]
    [InlineData("BtnSelectCustomIcon")]
    public void SettingsSelectionButton_UsesSharedFormSelectionStyle(string buttonName)
    {
        XDocument window = LoadXaml("SettingsWindow.xaml");
        XElement button = Assert.Single(
            window.Descendants(PresentationNamespace + "Button"),
            element => string.Equals(element.Attribute(XamlNamespace + "Name")?.Value, buttonName, StringComparison.Ordinal));

        Assert.Equal("{StaticResource FormSelectionButtonStyle}", button.Attribute("Style")?.Value);
    }

    [Fact]
    public void QrMultilineInputs_RetainPurposeful60PixelHeight()
    {
        XDocument window = LoadXaml("QRCodeGeneratorWindow.xaml");

        foreach (string inputName in new[] { "TxtEmailBody", "TxtSmsMessage" })
        {
            XElement input = Assert.Single(
                window.Descendants(PresentationNamespace + "TextBox"),
                element => string.Equals(element.Attribute(XamlNamespace + "Name")?.Value, inputName, StringComparison.Ordinal));

            Assert.Equal("60", input.Attribute("Height")?.Value);
            Assert.Equal("True", input.Attribute("AcceptsReturn")?.Value);
        }
    }

    [Fact]
    public async Task SharedFormStyles_ResolveTo36PixelsAtRuntime()
    {
        await RunStaAsync(() =>
        {
            Assert.Equal(36d, Assert.IsType<double>(Application.Current.FindResource("FormControlHeight")));

            var textBox = new TextBox
            {
                Style = Assert.IsType<Style>(Application.Current.FindResource("BaseTextBoxStyle"))
            };
            var comboBox = new ComboBox
            {
                Style = Assert.IsType<Style>(Application.Current.FindResource("BaseComboBoxStyle"))
            };
            var selectionButton = new Button
            {
                Style = Assert.IsType<Style>(Application.Current.FindResource("FormSelectionButtonStyle"))
            };

            Assert.Equal(36d, textBox.Height);
            Assert.Equal(36d, comboBox.Height);
            Assert.Equal(36d, selectionButton.Height);
            Assert.Equal(new Thickness(4, 0, 4, 0), textBox.Padding);
            Assert.Equal(
                new Thickness(8, 0, 0, 0),
                Assert.IsType<Thickness>(Application.Current.FindResource("FormInputPlaceholderMargin")));
            Assert.Equal(
                Color.FromRgb(0x2D, 0x2D, 0x2D),
                Assert.IsType<SolidColorBrush>(textBox.Background).Color);
        });
    }

    private static XElement FindStyle(XDocument document, string key) =>
        Assert.Single(
            document.Descendants(PresentationNamespace + "Style"),
            style => string.Equals(style.Attribute(XamlNamespace + "Key")?.Value, key, StringComparison.Ordinal));

    private static void AssertSetter(XElement style, string property, string expectedValue)
    {
        XElement setter = Assert.Single(
            style.Elements(PresentationNamespace + "Setter"),
            element => string.Equals(element.Attribute("Property")?.Value, property, StringComparison.Ordinal));
        Assert.Equal(expectedValue, setter.Attribute("Value")?.Value);
    }

    private static XDocument LoadXaml(string fileName) =>
        XDocument.Load(Path.Combine(FindRepoRoot(), "AiteBar", fileName));

    private static Task RunStaAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
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
