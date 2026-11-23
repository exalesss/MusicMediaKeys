using System;
using System.IO;
using Xunit;

namespace MusicMediaKeys.Tests
{
    public class UpdateServiceTests
    {
        private static readonly Version Current = new(2, 1, 0);

        private static string Release(
            string tag,
            string assetName = "MusicMediaKeys.exe",
            string digest = "sha256:ABCDEF",
            string? downloadUrl = null)
        {
            string url = downloadUrl ?? $"https://github.com/exalesss/MusicMediaKeys/releases/download/{tag}/{assetName}";
            return $$"""
            {
              "tag_name": "{{tag}}",
              "assets": [
                { "name": "notes.txt", "browser_download_url": "https://github.com/exalesss/MusicMediaKeys/releases/download/{{tag}}/notes.txt", "size": 10 },
                { "name": "{{assetName}}", "browser_download_url": "{{url}}", "size": 71000000, "digest": "{{digest}}" }
              ]
            }
            """;
        }

        [Fact]
        public void ParseRelease_NewerVersion_ReturnsTheExeAsset()
        {
            UpdateInfo? update = UpdateService.ParseRelease(Release("v2.2.0"), Current);

            Assert.NotNull(update);
            Assert.Equal(new Version(2, 2, 0), update!.Version);
            Assert.Equal("https://github.com/exalesss/MusicMediaKeys/releases/download/v2.2.0/MusicMediaKeys.exe", update.DownloadUrl);
            Assert.Equal(71000000, update.Size);
            Assert.Equal("ABCDEF", update.Sha256);
        }

        [Theory]
        [InlineData("https://github.com/exalesss/MusicMediaKeys/releases/download/v2.2.0/MusicMediaKeys.exe", true)]
        [InlineData("https://objects.githubusercontent.com/github-production-release-asset/app.exe", true)]
        [InlineData("http://github.com/exalesss/MusicMediaKeys/releases/download/v2.2.0/MusicMediaKeys.exe", false)]
        [InlineData("https://evil.com/app.exe", false)]
        [InlineData("https://github.com.evil.com/app.exe", false)]
        public void IsAllowedDownloadUrl_ValidatesSchemeAndHost(string url, bool expected)
        {
            Assert.Equal(expected, UpdateService.IsAllowedDownloadUrl(url));
        }

        [Fact]
        public void ParseRelease_NonGitHubUrl_ReturnsNull()
        {
            string json = Release("v2.2.0", downloadUrl: "https://evil.com/MusicMediaKeys.exe");
            Assert.Null(UpdateService.ParseRelease(json, Current));
        }

        [Theory]
        [InlineData("v2.1.0")]
        [InlineData("2.1")]
        [InlineData("v2.0.5")]
        [InlineData("v1.0")]
        [InlineData("latest")]
        public void ParseRelease_SameOlderOrUnreadableVersion_ReturnsNull(string tag)
        {
            Assert.Null(UpdateService.ParseRelease(Release(tag), Current));
        }

        [Fact]
        public void ParseRelease_WithoutTheExe_ReturnsNull()
        {
            Assert.Null(UpdateService.ParseRelease(Release("v3.0.0", assetName: "Setup.exe"), Current));
        }

        [Fact]
        public void ParseRelease_WithoutDigest_HasNoChecksum()
        {
            Assert.Null(UpdateService.ParseRelease(Release("v2.2.0", digest: ""), Current)!.Sha256);
        }

        [Fact]
        public void ReplaceExecutable_PutsTheNewFileInPlaceAndKeepsTheOldOne()
        {
            string dir = Directory.CreateTempSubdirectory("mmk-update-").FullName;
            try
            {
                string exe = Path.Combine(dir, "MusicMediaKeys.exe");
                string downloaded = exe + ".new";
                File.WriteAllText(exe, "old");
                File.WriteAllText(downloaded, "new");
                File.WriteAllText(exe + ".old", "older");

                UpdateService.ReplaceExecutable(exe, downloaded);

                Assert.Equal("new", File.ReadAllText(exe));
                Assert.Equal("old", File.ReadAllText(exe + ".old"));
                Assert.False(File.Exists(downloaded));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void ReplaceExecutable_MissingNewFile_RestoresTheOldExe()
        {
            string dir = Directory.CreateTempSubdirectory("mmk-update-").FullName;
            try
            {
                string exe = Path.Combine(dir, "MusicMediaKeys.exe");
                File.WriteAllText(exe, "old");

                Assert.ThrowsAny<IOException>(() => UpdateService.ReplaceExecutable(exe, exe + ".new"));
                Assert.Equal("old", File.ReadAllText(exe));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void FormatVersion_TwoComponentVersion_DoesNotThrow()
        {
            Assert.Equal("2.2.0", UpdateService.FormatVersion(new Version(2, 2)));
            Assert.Equal("2.2.1", UpdateService.FormatVersion(new Version(2, 2, 1)));
        }

        [Theory]
        [InlineData("https://github.com/exalesss/MusicMediaKeys/releases/download/v2.2.0/MusicMediaKeys.exe", true)]
        [InlineData("https://objects.githubusercontent.com/github-production-release-asset-123/asset", true)]
        [InlineData("http://github.com/exalesss/MusicMediaKeys/releases/download/v2.2.0/MusicMediaKeys.exe", false)]
        [InlineData("https://evil.example.com/MusicMediaKeys.exe", false)]
        public void IsAllowedDownloadUrl_ValidatesGitHubHttpsHosts(string url, bool expected)
        {
            Assert.Equal(expected, UpdateService.IsAllowedDownloadUrl(url));
        }
    }
}
