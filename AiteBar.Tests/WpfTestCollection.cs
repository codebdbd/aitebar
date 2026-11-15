using System;
using Xunit;

namespace AiteBar.Tests;

[CollectionDefinition("WpfTestCollection")]
public class WpfTestCollection : ICollectionFixture<WpfTestFixture>
{
}

public class WpfTestFixture : IDisposable
{
    public WpfTestFixture()
    {
        WpfTestApplication.EnsureCreated();
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Creates the single per-process <see cref="App"/> instance (with App.xaml resources) exactly once.
/// Tests from different collections may run in parallel, so creation must be serialized; otherwise a
/// bare Application or a second instance can win the race and break resource lookups in later tests.
/// </summary>
internal static class WpfTestApplication
{
    private static readonly object Sync = new();

    public static void EnsureCreated()
    {
        lock (Sync)
        {
            System.Windows.Application.ResourceAssembly ??= typeof(App).Assembly;

            if (System.Windows.Application.Current == null)
            {
                var app = new App();
                app.InitializeComponent();
            }
        }
    }
}
