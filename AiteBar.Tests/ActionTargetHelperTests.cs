using System;
using System.IO;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class ActionTargetHelperTests
{
    [Fact]
    public void NormalizeActionType_ReturnsEnumName_ForKnownActionType()
    {
        string normalized = ActionTargetHelper.NormalizeActionType(nameof(ActionType.File), @"C:\temp\doc.txt");

        Assert.Equal(nameof(ActionType.File), normalized);
    }

    [Fact]
    public void NormalizeActionType_ReturnsProgram_ForExecutablePath()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");

        try
        {
            File.WriteAllText(tempFile, string.Empty);

            string normalized = ActionTargetHelper.NormalizeActionType("Exe", tempFile);

            Assert.Equal(nameof(ActionType.Program), normalized);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void NormalizeActionType_ReturnsFolder_ForExistingDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aitebar-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);

            string normalized = ActionTargetHelper.NormalizeActionType("Exe", tempDir);

            Assert.Equal(nameof(ActionType.Folder), normalized);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void IsRegularFilePath_ReturnsTrue_ForNonExecutableFile()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllText(tempFile, "ok");

            Assert.True(ActionTargetHelper.IsRegularFilePath(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
