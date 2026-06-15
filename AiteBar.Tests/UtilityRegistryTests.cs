using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AiteBar.Tests;

public sealed class UtilityRegistryTests : IDisposable
{
    public UtilityRegistryTests()
    {
        UtilityRegistry.Clear();
    }

    public void Dispose()
    {
        UtilityRegistry.Clear();
    }

    [Fact]
    public void RegisterAllFromAssembly_RegistersAllMarkedUtilities()
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        UtilityRegistry.RegisterAllFromAssembly(assembly);
        
        var utilities = UtilityRegistry.GetAll();
        var testUtility = utilities.FirstOrDefault(u => u.Id == "TestUtility");
        Assert.NotNull(testUtility);
    }

    [Fact]
    public void Register_SkipsIncompatibleUtility()
    {
        var incompatibleUtility = new IncompatibleTestUtility();
        
        UtilityRegistry.Register(incompatibleUtility);
        
        var utilities = UtilityRegistry.GetAll();
        var incompatible = utilities.FirstOrDefault(u => u.Id == "IncompatibleTestUtility");
        Assert.Null(incompatible);
    }

    [Fact]
    public void Register_RegistersCompatibleUtility()
    {
        var compatibleUtility = new CompatibleTestUtility();
        
        UtilityRegistry.Register(compatibleUtility);
        
        var utilities = UtilityRegistry.GetAll();
        var compatible = utilities.FirstOrDefault(u => u.Id == "CompatibleTestUtility");
        Assert.NotNull(compatible);
    }

    [Fact]
    public async System.Threading.Tasks.Task UtilityBase_LaunchAsync_HandlesExceptions()
    {
        var utility = new CrashingTestUtility();
        var settingsService = new AppSettingsService();
        
        // Should not throw exception
        await utility.LaunchAsync(settingsService, null);
    }
}

[Utility]
public class TestUtility : UtilityBase<TestWindow>
{
    public override string Id => "TestUtility";
    public override string DisplayNameKey => "Tool_Test";
    public override string IconGlyph => "T";
    public override string IconColor => "#FFFFFF";

    protected override TestWindow CreateWindow(AppSettingsService settingsService, System.Windows.Window? owner)
    {
        throw new System.NotImplementedException();
    }

    protected override void ShowWindow(TestWindow window, AppSettingsService settingsService)
    {
        throw new System.NotImplementedException();
    }
}

public class TestWindow : System.Windows.Window
{
}

public class IncompatibleTestUtility : IUtility
{
    public string Id => "IncompatibleTestUtility";
    public string DisplayNameKey => "Tool_IncompatibleTest";
    public string IconGlyph => "I";
    public string IconColor => "#FF0000";

    public Version ContractVersion => new(2, 0);
    public bool IsCompatibleWith(Version coreVersion) => false;

    public System.Threading.Tasks.Task LaunchAsync(AppSettingsService settingsService, System.Windows.Window? owner, Func<System.Threading.Tasks.Task>? onBeforeExecute = null)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}

public class CompatibleTestUtility : IUtility
{
    public string Id => "CompatibleTestUtility";
    public string DisplayNameKey => "Tool_CompatibleTest";
    public string IconGlyph => "C";
    public string IconColor => "#00FF00";

    public System.Threading.Tasks.Task LaunchAsync(AppSettingsService settingsService, System.Windows.Window? owner, Func<System.Threading.Tasks.Task>? onBeforeExecute = null)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}

public class CrashingTestUtility : UtilityBase<CrashingTestWindow>
{
    public override string Id => "CrashingTestUtility";
    public override string DisplayNameKey => "Tool_CrashingTest";
    public override string IconGlyph => "X";
    public override string IconColor => "#FF0000";

    protected override CrashingTestWindow CreateWindow(AppSettingsService settingsService, System.Windows.Window? owner)
    {
        throw new InvalidOperationException("Test crash");
    }

    protected override void ShowWindow(CrashingTestWindow window, AppSettingsService settingsService)
    {
        throw new System.NotImplementedException();
    }
}

public class CrashingTestWindow : System.Windows.Window
{
}