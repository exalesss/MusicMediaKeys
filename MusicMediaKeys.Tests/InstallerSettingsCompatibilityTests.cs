using System.Text.Json;
using MusicMediaKeys.Installer;
using Xunit;

namespace MusicMediaKeys.Tests
{
    public class InstallerSettingsCompatibilityTests
    {
        [Fact]
        public void BuildSettingsJson_DeserializesIntoAppSettingsWithMatchingFields()
        {
            var options = new InstallOptions
            {
                SelectedApplication = (int)ApplicationSelectionMode.Aimp,
                CustomPlayerProcessName = "musicbee",
                Language = (int)Language.Russian,
                InstalledLanguages = new System.Collections.Generic.List<int> { (int)Language.Russian },
                Theme = (int)Theme.Light,
                AutoStart = true,
                SkipUacPrompt = true,
                StartWithWindowOpen = true,
                ShowOverlay = false,
                PlayStartupSound = true,
                PreviousKey = 0x25,
                PreviousModifiers = 3,
                PlayPauseKey = 0x20,
                PlayPauseModifiers = 2,
                NextKey = 0x27,
                NextModifiers = 1
            };

            string json = InstallerModel.BuildSettingsJson(options);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, AppSettings.JsonOptions);

            Assert.NotNull(settings);
            Assert.True(settings!.IsFirstRunCompleted);
            Assert.Equal(ApplicationSelectionMode.Aimp, settings.SelectedApplication);
            Assert.Equal("musicbee", settings.CustomPlayerProcessName);
            Assert.Equal(Language.Russian, settings.Language);
            Assert.Single(settings.GetAvailableLanguages());
            Assert.Equal(Language.Russian, settings.GetAvailableLanguages()[0]);
            Assert.Equal(Theme.Light, settings.Theme);
            Assert.True(settings.AutoStart);
            Assert.True(settings.SkipUacPrompt);
            Assert.True(settings.StartWithWindowOpen);
            Assert.False(settings.ShowOverlay);
            Assert.True(settings.PlayStartupSound);
            Assert.Equal(0x25, settings.PreviousBinding.Key);
            Assert.Equal(3u, settings.PreviousBinding.Modifiers);
            Assert.Equal(0x20, settings.PlayPauseBinding.Key);
            Assert.Equal(2u, settings.PlayPauseBinding.Modifiers);
            Assert.Equal(0x27, settings.NextBinding.Key);
            Assert.Equal(1u, settings.NextBinding.Modifiers);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("relative\\path")]
        [InlineData("C:\\")]
        [InlineData("C:\\Windows")]
        [InlineData("C:\\Windows\\System32")]
        [InlineData("C:\\Program Files")]
        [InlineData("\\\\attacker\\share\\MusicMediaKeys")]
        public void IsSafeInstallDirectory_RejectsUnsafeOrSystemRoots(string path)
        {
            Assert.False(InstallerEngine.IsSafeInstallDirectory(path));
        }

        [Fact]
        public void IsSafeInstallDirectory_RejectsUserProfileAndAppDataRoots()
        {
            Assert.False(InstallerEngine.IsSafeInstallDirectory(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)));
            Assert.False(InstallerEngine.IsSafeInstallDirectory(System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory)));
            Assert.False(InstallerEngine.IsSafeInstallDirectory(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData)));
            Assert.False(InstallerEngine.IsSafeInstallDirectory(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData)));
        }

        [Fact]
        public void IsSafeInstallDirectory_AllowsDedicatedSubfolders()
        {
            string programFilesSubdir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles),
                "MusicMediaKeys");
            string localProgramsSubdir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "MusicMediaKeys");

            Assert.True(InstallerEngine.IsSafeInstallDirectory(programFilesSubdir));
            Assert.True(InstallerEngine.IsSafeInstallDirectory(localProgramsSubdir));
        }

        [Theory]
        [InlineData("MusicMediaKeys-Setup.exe", new string[0], true, false)]
        [InlineData("Setup.exe", new string[0], true, false)]
        [InlineData("MusicMediaKeys.exe", new[] { "--install" }, true, false)]
        [InlineData("MusicMediaKeys.exe", new[] { "--uninstall" }, false, true)]
        [InlineData("uninstall.exe", new string[0], false, true)]
        [InlineData("MusicMediaKeys.exe", new string[0], false, false)]
        public void ResolveLaunchMode_DetectsInstallUninstallAndNormalApp(string exeName, string[] args, bool expectedInstall, bool expectedUninstall)
        {
            var mode = Program.ResolveLaunchMode(args, exeName);
            Assert.Equal(expectedInstall, mode.IsInstall);
            Assert.Equal(expectedUninstall, mode.IsUninstall);
        }
    }
}
