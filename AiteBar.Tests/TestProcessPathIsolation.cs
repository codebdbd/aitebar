using System;
using System.IO;
using System.Runtime.CompilerServices;
using AiteBar;

namespace AiteBar.Tests;

internal static class TestProcessPathIsolation
{
    internal static string Root { get; } = Path.Combine(
        Path.GetTempPath(),
        "AiteBarTests",
        "TestProcess",
        $"{Environment.ProcessId}-{Guid.NewGuid():N}");

    [ModuleInitializer]
    internal static void Initialize()
    {
        Directory.CreateDirectory(Root);
        PathHelper.SetAppDataFolderFallbackOverride(Root);
    }
}
