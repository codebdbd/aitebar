using System.Threading;

namespace AiteBar.Tests;

public sealed class AppLifecycleTests
{
    [Fact]
    public void DisposeInstanceMutex_WhenProcessDoesNotOwnMutex_DoesNotReleaseIt()
    {
        var mutex = new Mutex(initiallyOwned: false);

        Exception? exception = Record.Exception(() => App.DisposeInstanceMutex(mutex, ownsMutex: false));

        Assert.Null(exception);
    }

    [Fact]
    public void DisposeInstanceMutex_WhenProcessOwnsMutex_ReleasesAndDisposesIt()
    {
        var mutex = new Mutex(initiallyOwned: true);

        Exception? exception = Record.Exception(() => App.DisposeInstanceMutex(mutex, ownsMutex: true));

        Assert.Null(exception);
    }
}
