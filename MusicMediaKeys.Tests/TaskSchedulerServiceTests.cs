using System;
using System.IO;
using Xunit;

namespace MusicMediaKeys.Tests
{
    public class TaskSchedulerServiceTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsRunningFromUntrustedPath_NullOrEmpty_ReturnsTrue(string? path)
        {
            Assert.True(WindowsTaskSchedulerService.IsRunningFromUntrustedPath(path));
        }

        [Fact]
        public void IsRunningFromUntrustedPath_InvalidPathCharacters_ReturnsTrue()
        {
            Assert.True(WindowsTaskSchedulerService.IsRunningFromUntrustedPath("C:\\invalid\0path\\app.exe"));
        }

        [Fact]
        public void IsRunningFromUntrustedPath_TempDirectory_ReturnsTrue()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "testApp.exe");
            Assert.True(WindowsTaskSchedulerService.IsRunningFromUntrustedPath(tempFile));
        }

        [Fact]
        public void IsRunningFromUntrustedPath_DownloadsDirectory_ReturnsTrue()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                string downloadFile = Path.Combine(userProfile, "Downloads", "MusicMediaKeys (1).exe");
                Assert.True(WindowsTaskSchedulerService.IsRunningFromUntrustedPath(downloadFile));
            }
        }

        [Fact]
        public void IsRunningFromUntrustedPath_ProgramFiles_ReturnsFalse()
        {
            string progFilesPath = @"C:\Program Files\MusicMediaKeys\MusicMediaKeys.exe";
            Assert.False(WindowsTaskSchedulerService.IsRunningFromUntrustedPath(progFilesPath));
        }

        [Fact]
        public void IsRunningFromUntrustedPath_AppData_ReturnsFalse()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                string appDataFile = Path.Combine(localAppData, "MusicMediaKeys", "MusicMediaKeys.exe");
                Assert.False(WindowsTaskSchedulerService.IsRunningFromUntrustedPath(appDataFile));
            }
        }

        [Fact]
        public void IsTaskRegistered_NonExistentTask_ReturnsFalse()
        {
            string nonExistentTask = "MMK_Test_NonExistent_" + Guid.NewGuid().ToString("N");
            Assert.False(WindowsTaskSchedulerService.IsTaskRegistered(nonExistentTask));
        }

        [Fact]
        public void DeleteTask_NonExistentTask_ReturnsTrue()
        {
            string nonExistentTask = "MMK_Test_NonExistent_" + Guid.NewGuid().ToString("N");
            Assert.True(WindowsTaskSchedulerService.DeleteTask(nonExistentTask));
        }

        [Fact]
        public void RegisterLogonTask_UntrustedPath_RefusesAndReturnsFalse()
        {
            string tempExe = Path.Combine(Path.GetTempPath(), "UntrustedApp.exe");
            bool result = WindowsTaskSchedulerService.RegisterLogonTask("MMK_Untrusted_Test", tempExe, runElevated: false);
            Assert.False(result);
        }

        [Fact]
        public void RegisterOnDemandElevatedTask_UntrustedPath_RefusesAndReturnsFalse()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloadExe = Path.Combine(userProfile, "Downloads", "UntrustedApp.exe");
            bool result = WindowsTaskSchedulerService.RegisterOnDemandElevatedTask("MMK_Untrusted_Elevated_Test", downloadExe);
            Assert.False(result);
        }
    }
}
