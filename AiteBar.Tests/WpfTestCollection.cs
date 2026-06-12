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
        if (System.Windows.Application.ResourceAssembly == null)
        {
            System.Windows.Application.ResourceAssembly = typeof(App).Assembly;
        }

        if (System.Windows.Application.Current == null)
        {
            var app = new App();
            app.InitializeComponent();
        }
    }

    public void Dispose()
    {
    }
}
