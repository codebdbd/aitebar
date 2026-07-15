using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class CommandButtonStyleTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void SharedCommandStyles_UseTimerReferenceGeometryAndSemanticColors()
    {
        XDocument resources = LoadXaml("UtilityWindowResources.xaml");
        XElement baseStyle = FindStyle(resources, "CommandButtonBaseStyle");

        Assert.Equal("{StaticResource PrimaryButtonStyle}", baseStyle.Attribute("BasedOn")?.Value);
        AssertSetter(baseStyle, "Height", "44");
        AssertSetter(baseStyle, "MinWidth", "150");
        AssertSetter(baseStyle, "Padding", "18,0");
        AssertSetter(baseStyle, "FontSize", "14");

        XElement commandStyle = FindStyle(resources, "CommandButtonStyle");
        Assert.Equal("{StaticResource CommandButtonBaseStyle}", commandStyle.Attribute("BasedOn")?.Value);
        AssertSetter(commandStyle, "Background", "{StaticResource FormControlBackground}");
        AssertSetter(commandStyle, "BorderBrush", "{StaticResource FormControlBorderBrush}");

        XElement primaryStyle = FindStyle(resources, "PrimaryCommandButtonStyle");
        Assert.Equal("{StaticResource CommandButtonBaseStyle}", primaryStyle.Attribute("BasedOn")?.Value);
        AssertSetter(primaryStyle, "Foreground", "White");
        AssertSetter(primaryStyle, "Background", "{DynamicResource AccentColor}");
        Assert.DoesNotContain(
            primaryStyle.Elements(PresentationNamespace + "Setter"),
            setter => string.Equals(setter.Attribute("Property")?.Value, "BorderBrush", StringComparison.Ordinal));

        XElement disabledTrigger = Assert.Single(
            primaryStyle
                .Descendants(PresentationNamespace + "Trigger"),
            trigger =>
                string.Equals(trigger.Attribute("Property")?.Value, "IsEnabled", StringComparison.Ordinal) &&
                string.Equals(trigger.Attribute("Value")?.Value, "False", StringComparison.Ordinal));
        AssertSetter(disabledTrigger, "Background", "#242426");
        AssertSetter(disabledTrigger, "Foreground", "#77777B");
        AssertSetter(disabledTrigger, "BorderBrush", "Transparent");
        AssertSetter(disabledTrigger, "Cursor", "Arrow");
    }

    [Fact]
    public async Task DisabledPrimary_IsVisuallyDistinctFromEnabledSecondary_AndHasNoAccentBorder()
    {
        await RunStaAsync(() =>
        {
            var secondary = new Button
            {
                Style = Assert.IsType<Style>(Application.Current.FindResource("CommandButtonStyle"))
            };
            var disabledPrimary = new Button
            {
                Style = Assert.IsType<Style>(Application.Current.FindResource("PrimaryCommandButtonStyle")),
                IsEnabled = false
            };

            secondary.ApplyTemplate();
            disabledPrimary.ApplyTemplate();

            Color secondaryBackground = Assert.IsType<SolidColorBrush>(secondary.Background).Color;
            Color disabledBackground = Assert.IsType<SolidColorBrush>(disabledPrimary.Background).Color;
            Color secondaryForeground = Assert.IsType<SolidColorBrush>(secondary.Foreground).Color;
            Color disabledForeground = Assert.IsType<SolidColorBrush>(disabledPrimary.Foreground).Color;
            Color disabledBorder = Assert.IsType<SolidColorBrush>(disabledPrimary.BorderBrush).Color;

            Assert.Equal(Color.FromRgb(0x38, 0x38, 0x38), secondaryBackground);
            Assert.Equal(Color.FromRgb(0x24, 0x24, 0x26), disabledBackground);
            Assert.NotEqual(secondaryBackground, disabledBackground);
            Assert.NotEqual(secondaryForeground, disabledForeground);
            Assert.Equal(Colors.Transparent, disabledBorder);
        });
    }

    [Theory]
    [InlineData("TimerStopwatchWindow.xaml", "name", "BtnStartPause", "PrimaryCommandButtonStyle")]
    [InlineData("TimerStopwatchWindow.xaml", "name", "BtnReset", "CommandButtonStyle")]
    [InlineData("FileSorterWindow.xaml", "name", "BtnSort", "PrimaryCommandButtonStyle")]
    [InlineData("IconConverterWindow.xaml", "click", "BtnChoose_Click", "CommandButtonStyle")]
    [InlineData("IconConverterWindow.xaml", "name", "BtnSave", "PrimaryCommandButtonStyle")]
    [InlineData("QRCodeGeneratorWindow.xaml", "name", "BtnSavePng", "CommandButtonStyle")]
    [InlineData("QRCodeGeneratorWindow.xaml", "name", "BtnSaveSvg", "CommandButtonStyle")]
    [InlineData("QRCodeGeneratorWindow.xaml", "name", "BtnCopyPng", "CommandButtonStyle")]
    [InlineData("QRCodeGeneratorWindow.xaml", "name", "BtnCopySvg", "CommandButtonStyle")]
    [InlineData("SettingsWindow.xaml", "name", "BtnCancel", "CommandButtonStyle")]
    [InlineData("SettingsWindow.xaml", "name", "BtnSave", "PrimaryCommandButtonStyle")]
    [InlineData("AppSettingsWindow.xaml", "click", "BtnCancel_Click", "CommandButtonStyle")]
    [InlineData("AppSettingsWindow.xaml", "click", "BtnSave_Click", "PrimaryCommandButtonStyle")]
    public void AgreedButton_UsesSharedSemanticStyle(
        string fileName,
        string selectorKind,
        string selectorValue,
        string expectedStyle)
    {
        XDocument window = LoadXaml(fileName);
        XElement button = Assert.Single(
            window.Descendants(PresentationNamespace + "Button"),
            element => selectorKind == "name"
                ? string.Equals(element.Attribute(XamlNamespace + "Name")?.Value, selectorValue, StringComparison.Ordinal)
                : string.Equals(element.Attribute("Click")?.Value, selectorValue, StringComparison.Ordinal));

        Assert.Equal($"{{StaticResource {expectedStyle}}}", button.Attribute("Style")?.Value);
    }

    [Theory]
    [InlineData("TimerStopwatchWindow.xaml")]
    [InlineData("IconConverterWindow.xaml")]
    public void MigratedWindow_DoesNotShadowSharedCommandStyles(string fileName)
    {
        XDocument window = LoadXaml(fileName);
        string[] localStyleKeys = window
            .Descendants(PresentationNamespace + "Style")
            .Select(style => style.Attribute(XamlNamespace + "Key")?.Value)
            .Where(key => key != null)
            .Cast<string>()
            .ToArray();

        Assert.DoesNotContain("CommandButtonStyle", localStyleKeys);
        Assert.DoesNotContain("PrimaryCommandButtonStyle", localStyleKeys);
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
