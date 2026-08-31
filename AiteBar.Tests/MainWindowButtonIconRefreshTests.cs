using AiteBar;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class MainWindowButtonIconRefreshTests
{
    [Fact]
    public async Task SaveElement_ChangedGlyphAndFont_AreAppliedToExistingPanelButton()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(async () =>
            {
                EnsureApplicationResources();
                var settingsService = new AppSettingsService(
                    Path.Combine(root, "custom_buttons.json"),
                    Path.Combine(root, "settings.json"));
                var window = new MainWindow(settingsService);
                try
                {
                    AppSettings settings = window.GetAppSettings();
                    settings.Contexts = ContextStateHelper.NormalizeContexts(settings.Contexts);
                    settings.ActiveContextId = settings.Contexts[0].Id;
                    foreach (UtilityButtonDefinition definition in UtilityButtonCatalog.All)
                    {
                        definition.SetVisible(settings, false);
                    }

                    var element = new CustomElement
                    {
                        Id = "button-1",
                        Name = "Button",
                        ContextId = settings.ActiveContextId,
                        ActionType = nameof(ActionType.Web),
                        ActionValue = "https://example.com",
                        Icon = "\uF45B",
                        IconFont = FontHelper.FluentKey,
                        ImagePath = ""
                    };
                    settings.Elements = [element];
                    settingsService.Settings = settings;
                    window.RefreshPanel();

                    Button before = GetOnlyPanelButton(window);
                    Assert.Equal("\uF45B", before.Content);
                    Assert.Equal(FontHelper.Resolve(FontHelper.FluentKey), before.FontFamily);

                    CustomElement updated = settingsService.CloneElement(element);
                    updated.Icon = "\uE11F";
                    updated.IconFont = FontHelper.FluentKey;
                    updated.ImagePath = "";
                    await window.SaveElement(updated, element.Id);

                    Button after = GetOnlyPanelButton(window);
                    Assert.Equal("\uE11F", after.Content);
                    Assert.Equal(FontHelper.Resolve(FontHelper.FluentKey), after.FontFamily);
                    Assert.Equal("\uE11F", Assert.Single(settingsService.Elements).Icon);
                }
                finally
                {
                    window.Close();
                }
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveElement_NewButtonUsesSelectedFluentGlyphOnPanel()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(async () =>
            {
                EnsureApplicationResources();
                var settingsService = new AppSettingsService(
                    Path.Combine(root, "custom_buttons.json"),
                    Path.Combine(root, "settings.json"));
                var window = new MainWindow(settingsService);
                try
                {
                    AppSettings settings = PrepareSettings(window);
                    settings.Elements = [];
                    settingsService.Settings = settings;
                    window.RefreshPanel();

                    CustomElement element = CreateElement(settings.ActiveContextId);
                    element.Icon = "\uE11F";
                    await window.SaveElement(element);

                    Button button = GetOnlyPanelButton(window);
                    Assert.Equal("\uE11F", button.Content);
                    Assert.Equal(FontHelper.Resolve(FontHelper.FluentKey), button.FontFamily);
                }
                finally
                {
                    window.Close();
                }
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveElement_ChangedImageToGlyph_RemovesOldPanelImage()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await RunStaAsync(async () =>
            {
                EnsureApplicationResources();
                string imagePath = Path.Combine(root, "icon.png");
                WriteTestPng(imagePath);

                var settingsService = new AppSettingsService(
                    Path.Combine(root, "custom_buttons.json"),
                    Path.Combine(root, "settings.json"));
                var window = new MainWindow(settingsService);
                try
                {
                    AppSettings settings = PrepareSettings(window);
                    var element = CreateElement(settings.ActiveContextId);
                    element.ImagePath = imagePath;
                    settings.Elements = [element];
                    settingsService.Settings = settings;
                    window.RefreshPanel();
                    await WaitForPanelImageAsync(window);

                    Button before = GetOnlyPanelButton(window);
                    Assert.IsType<System.Windows.Controls.Image>(before.Content);

                    CustomElement updated = settingsService.CloneElement(element);
                    updated.Icon = "\uF606";
                    updated.IconFont = FontHelper.FluentKey;
                    updated.ImagePath = "";
                    await window.SaveElement(updated, element.Id);

                    Button after = GetOnlyPanelButton(window);
                    Assert.Equal("\uF606", after.Content);
                    Assert.Equal(FontHelper.Resolve(FontHelper.FluentKey), after.FontFamily);
                }
                finally
                {
                    window.Close();
                }
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Button GetOnlyPanelButton(MainWindow window)
    {
        var panel = Assert.IsType<OverflowWrapPanel>(window.FindName("UnifiedButtonsPanel"));
        return Assert.IsType<Button>(Assert.Single(panel.Children));
    }

    private static AppSettings PrepareSettings(MainWindow window)
    {
        AppSettings settings = window.GetAppSettings();
        settings.Contexts = ContextStateHelper.NormalizeContexts(settings.Contexts);
        settings.ActiveContextId = settings.Contexts[0].Id;
        foreach (UtilityButtonDefinition definition in UtilityButtonCatalog.All)
        {
            definition.SetVisible(settings, false);
        }

        return settings;
    }

    private static CustomElement CreateElement(string contextId) => new()
    {
        Id = "button-1",
        Name = "Button",
        ContextId = contextId,
        ActionType = nameof(ActionType.Web),
        ActionValue = "https://example.com",
        Icon = "\uF45B",
        IconFont = FontHelper.FluentKey,
        ImagePath = ""
    };

    private static async Task WaitForPanelImageAsync(MainWindow window)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (GetOnlyPanelButton(window).Content is not System.Windows.Controls.Image &&
               DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }

    private static void WriteTestPng(string path)
    {
        var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 0x00, 0x7A, 0xCC, 0xFF },
            4);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void EnsureApplicationResources()
    {
        if (Application.ResourceAssembly == null)
        {
            Application.ResourceAssembly = typeof(App).Assembly;
        }

        if (Application.Current == null)
        {
            var app = new App();
            app.InitializeComponent();
        }
    }

    private static Task RunStaAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            _ = Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            });
            Dispatcher.Run();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
