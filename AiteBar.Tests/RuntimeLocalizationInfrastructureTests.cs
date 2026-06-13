using System;
using System.IO;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
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
    public void LocalizationService_Get_UsesAppliedCultureAcrossThreads()
    {
        string originalCulture = LocalizationService.NormalizeCultureName(Thread.CurrentThread.CurrentUICulture.Name);

        try
        {
            LocalizationService.ApplyCulture("de");

            string? valueFromOtherThread = null;
            RunSta(() =>
            {
                Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en");
                valueFromOtherThread = LocalizationService.Get("Menu_Open");
            });

            Assert.Equal("Öffnen", valueFromOtherThread);
        }
        finally
        {
            LocalizationService.ApplyCulture(originalCulture);
        }
    }

    [Fact]
    public void RefreshLocalizedBindings_UpdatesDetachedContextMenuHeaders()
    {
        RunSta(() =>
        {
            string originalCulture = LocalizationService.NormalizeCultureName(Thread.CurrentThread.CurrentUICulture.Name);

            try
            {
                LocalizationService.ApplyCulture("en");

                var host = new Button();
                var menuItem = new MenuItem();
                BindingOperations.SetBinding(
                    menuItem,
                    HeaderedItemsControl.HeaderProperty,
                    new Binding("[Menu_OpenLocation]") { Source = LocalizationService.Strings });

                host.ContextMenu = new ContextMenu();
                host.ContextMenu.Items.Add(menuItem);

                Assert.Equal("Open location", menuItem.Header);

                LocalizationService.ApplyCulture("de");
                LocalizationService.RefreshLocalizedBindings(host);

                Assert.Equal("Speicherort öffnen", menuItem.Header);
            }
            finally
            {
                LocalizationService.ApplyCulture(originalCulture);
            }
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
