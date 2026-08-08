using System;
using System.IO;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;

namespace AiteBar.Tests;

[Collection("LocalizationStateTestCollection")]
public sealed class RuntimeLocalizationInfrastructureTests
{
    [Fact]
    public void DarkWindow_ReactsToCultureChangesThroughSharedHook()
    {
        string repoRoot = FindRepoRoot();
        string darkWindowCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "DarkWindow.cs"));

        Assert.Contains("protected DarkWindow()", darkWindowCode);
        Assert.Contains("LocalizationService.EnsureAppliedCulture();", darkWindowCode);
        Assert.Contains("LocalizationService.CultureChanged += HandleCultureChanged;", darkWindowCode);
        Assert.Contains("LocalizationService.RefreshLocalizedBindings(this);", darkWindowCode);
        Assert.Contains("protected virtual void OnLocalizationChanged()", darkWindowCode);
    }

    [Fact]
    public void MainWindow_ReactsToCultureChangesExplicitly()
    {
        string repoRoot = FindRepoRoot();
        string mainWindowCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "MainWindow.xaml.cs"));

        Assert.Contains("SubscribeToLocalizationChanges();", mainWindowCode);
        Assert.Contains("LocalizationService.CultureChanged += HandleCultureChanged;", mainWindowCode);
        Assert.Contains("LocalizationService.EnsureAppliedCulture();", mainWindowCode);
        Assert.Contains("LocalizationService.RefreshLocalizedBindings(this);", mainWindowCode);
        Assert.Contains("ApplyLocalizedText();", mainWindowCode);
        Assert.Contains("RefreshPanel();", mainWindowCode);
    }

    [Fact]
    public void LocalizationService_KeepsAppliedCulturePreferenceForWindowInitialization()
    {
        string repoRoot = FindRepoRoot();
        string localizationCode = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "LocalizationService.cs"));

        Assert.Contains("private static string _appliedCulturePreference = AutoCulture;", localizationCode);
        Assert.Contains("private static CultureInfo _resolvedCulture = ResolveCulture(AutoCulture);", localizationCode);
        Assert.Contains("string normalizedPreference = NormalizeCultureName(savedCulture);", localizationCode);
        Assert.Contains("CultureInfo resolvedCulture = ResolveCulture(normalizedPreference);", localizationCode);
        Assert.Contains("_appliedCulturePreference = normalizedPreference;", localizationCode);
        Assert.Contains("_resolvedCulture = resolvedCulture;", localizationCode);
        Assert.Contains("public static void EnsureAppliedCulture()", localizationCode);
        Assert.Contains("ApplyResolvedCulture(_resolvedCulture);", localizationCode);
    }

    [Fact]
    public async Task LocalizationService_Get_UsesAppliedCultureAcrossThreads()
    {
        string expectedValue = LocalizationService.Get("Menu_Open");

        string valueFromOtherThread = await Task.Run(() =>
        {
            Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en");
            return LocalizationService.Get("Menu_Open");
        });

        Assert.Equal(expectedValue, valueFromOtherThread);
    }

    [Fact]
    public void RefreshLocalizedBindings_UpdatesDetachedContextMenuHeaders()
    {
        RunSta(() =>
        {
            var strings = new Dictionary<string, string>
            {
                ["Menu_OpenLocation"] = "before"
            };
            var host = new Button();
            var menuItem = new MenuItem();
            BindingOperations.SetBinding(
                menuItem,
                HeaderedItemsControl.HeaderProperty,
                new Binding("[Menu_OpenLocation]") { Source = strings });

            host.ContextMenu = new ContextMenu();
            host.ContextMenu.Items.Add(menuItem);
            Assert.Equal("before", menuItem.Header);

            strings["Menu_OpenLocation"] = "after";
            LocalizationService.RefreshLocalizedBindings(host);

            Assert.Equal("after", menuItem.Header);
        });
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

    private static void RunSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw exception;
        }
    }
}
