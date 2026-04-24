using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace AiteBar.Tests;

public sealed class ReleaseVersionTests
{
    [Fact]
    public void ReleaseVersion_IsSynchronizedAcrossProjectAssemblyAndInstaller()
    {
        string repoRoot = FindRepoRoot();

        string csprojPath = Path.Combine(repoRoot, "AiteBar", "AiteBar.csproj");
        string assemblyInfoPath = Path.Combine(repoRoot, "AiteBar", "AssemblyInfo.cs");
        string issPath = Path.Combine(repoRoot, "installer", "AiteBar.iss");

        XDocument projectXml = XDocument.Load(csprojPath);
        string? projectVersion = projectXml.Descendants("Version").Select(x => x.Value.Trim()).FirstOrDefault();
        string? assemblyInfoVersion = FindQuotedValue(assemblyInfoPath, "AssemblyInformationalVersion");
        string? installerVersion = FindQuotedValue(issPath, "#define AppVersion");

        Assert.Equal("1.6.1", projectVersion);
        Assert.Equal(projectVersion, assemblyInfoVersion);
        Assert.Equal(projectVersion, installerVersion);
    }

    [Fact]
    public void ReleaseLayout_DoesNotContainLegacyNestedInstallerCopies()
    {
        string repoRoot = FindRepoRoot();

        Assert.False(File.Exists(Path.Combine(repoRoot, "AiteBar", "installer", "AiteBar.iss")));
        Assert.False(File.Exists(Path.Combine(repoRoot, "AiteBar", "installer", "Build-Installer.ps1")));
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

    private static string? FindQuotedValue(string path, string marker)
    {
        foreach (string line in File.ReadLines(path))
        {
            if (!line.Contains(marker, StringComparison.Ordinal))
            {
                continue;
            }

            int firstQuote = line.IndexOf('"');
            int lastQuote = line.LastIndexOf('"');
            if (firstQuote >= 0 && lastQuote > firstQuote)
            {
                return line[(firstQuote + 1)..lastQuote];
            }
        }

        return null;
    }
}
