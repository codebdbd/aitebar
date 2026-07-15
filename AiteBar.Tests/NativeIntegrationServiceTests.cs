using System;

namespace AiteBar.Tests;

public sealed class NativeIntegrationServiceTests
{
    [Fact]
    public void Dispose_IsIdempotentAndPreventsReinstallingHook()
    {
        var service = new NativeIntegrationService();

        service.Dispose();
        service.Dispose();

        Assert.Throws<ObjectDisposedException>(service.InstallMouseHook);
    }
}
