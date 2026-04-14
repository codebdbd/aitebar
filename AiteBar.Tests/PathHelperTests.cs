using System;
using System.IO;
using AiteBar;
using Xunit;

namespace AiteBar.Tests
{
    public class PathHelperTests
    {
        [Fact]
        public void AppDataFolder_ShouldBeUnderRoaming()
        {
            var folder = PathHelper.AppDataFolder;
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            
            Assert.StartsWith(roaming, folder);
            Assert.Contains(PathHelper.AppCompany, folder);
            Assert.Contains(PathHelper.AppName, folder);
        }

        [Fact]
        public void ConfigFile_ShouldBeInAppData()
        {
            var file = PathHelper.ConfigFile;
            Assert.StartsWith(PathHelper.AppDataFolder, file);
            Assert.EndsWith("custom_buttons.json", file);
        }

        [Fact]
        public void LogFile_ShouldBeInAppData()
        {
            var file = PathHelper.LogFile;
            Assert.StartsWith(PathHelper.AppDataFolder, file);
            Assert.EndsWith("error.log", file);
        }
    }
}
