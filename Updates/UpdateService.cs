using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MusicMediaKeys
{
    internal sealed record UpdateInfo(Version Version, string DownloadUrl, long Size, string? Sha256);

    internal enum UpdateCheckResult
    {
        UpToDate,
        Available,
        Failed,
        RateLimited
    }

    internal static class UpdateService
    {
        public const string Repository = "exalesss/MusicMediaKeys";
        public const string AssetName = "MusicMediaKeys.exe";
        private const string AfterUpdateArgument = "--after-update";
        private const string ProductName = "Music Media Keys";

        private static readonly HttpClient Http = CreateHttpClient();

        public static UpdateInfo? Available { get; private set; }

        public static event Action? AvailableChanged;

        public static Version CurrentVersion { get; } =
            Version.TryParse(AppInfo.Version, out Version? version) ? version : new Version(0, 0);

        private static Version? _installedVersion;

        private static readonly TimeSpan ReuseAnswerFor = TimeSpan.FromMinutes(1);
        private static readonly SemaphoreSlim CheckLock = new(1, 1);
        private static string? _lastJson;
        private static string? _lastETag;
        private static DateTime _lastAnswerUtc;

        public static async Task<UpdateCheckResult> CheckAsync()
        {
            await CheckLock.WaitAsync();
            try
            {
                string? json = DateTime.UtcNow - _lastAnswerUtc < ReuseAnswerFor ? _lastJson : await FetchLatestReleaseAsync();
                if (json == null)
                    return UpdateCheckResult.RateLimited;

                UpdateInfo? update = ParseRelease(json, _installedVersion ?? CurrentVersion);
                SetAvailable(update);
                return update != null ? UpdateCheckResult.Available : UpdateCheckResult.UpToDate;
            }
            catch (Exception ex)
            {
                EventLogService.Log("Update", "Update check failed: " + ex.Message);
                return UpdateCheckResult.Failed;
            }
            finally
            {
                CheckLock.Release();
            }
        }

        private static async Task<string?> FetchLatestReleaseAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            if (_lastETag != null && _lastJson != null)
                request.Headers.TryAddWithoutValidation("If-None-Match", _lastETag);

            using HttpResponseMessage response = await Http.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NotModified && _lastJson != null)
            {
                _lastAnswerUtc = DateTime.UtcNow;
                return _lastJson;
            }

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests &&
                response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.FirstOrDefault() == "0")
            {
                EventLogService.Log("Update", "Update check skipped: GitHub's hourly limit is used up");
                return null;
            }

            response.EnsureSuccessStatusCode();
            _lastJson = await response.Content.ReadAsStringAsync();
            _lastETag = response.Headers.ETag?.ToString();
            _lastAnswerUtc = DateTime.UtcNow;
            return _lastJson;
        }

        internal static UpdateInfo? ParseRelease(string json, Version current)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            string tag = root.TryGetProperty("tag_name", out JsonElement tagProp) && tagProp.ValueKind == JsonValueKind.String
                ? tagProp.GetString() ?? ""
                : "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out Version? version) || Normalize(version) <= Normalize(current))
                return null;

            if (!root.TryGetProperty("assets", out JsonElement assetsProp) || assetsProp.ValueKind != JsonValueKind.Array)
                return null;

            foreach (JsonElement asset in assetsProp.EnumerateArray())
            {
                string? name = asset.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind == JsonValueKind.String
                    ? nameProp.GetString()
                    : null;
                if (!string.Equals(name, AssetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                string? url = asset.TryGetProperty("browser_download_url", out JsonElement urlProp) && urlProp.ValueKind == JsonValueKind.String
                    ? urlProp.GetString()
                    : null;
                if (string.IsNullOrEmpty(url) || !IsAllowedDownloadUrl(url))
                    return null;

                string? digest = asset.TryGetProperty("digest", out JsonElement digestProp) && digestProp.ValueKind == JsonValueKind.String
                    ? digestProp.GetString()
                    : null;
                string? sha256 = digest != null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                    ? digest[7..]
                    : null;
                long size = asset.TryGetProperty("size", out JsonElement sizeProp) && sizeProp.TryGetInt64(out long parsedSize)
                    ? parsedSize
                    : 0;

                return new UpdateInfo(version, url, size, sha256);
            }

            return null;
        }

        internal static bool IsAllowedDownloadUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && IsAllowedDownloadUri(uri);
        }

        internal static bool IsAllowedDownloadUri(Uri? uri)
        {
            if (uri == null || !uri.IsAbsoluteUri)
                return false;

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return false;

            string host = uri.Host;
            return host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task InstallAsync(UpdateInfo update, IProgress<double> progress, CancellationToken cancellationToken)
        {
            string exePath = AppInfo.ExecutablePath ?? throw new InvalidOperationException("The path of the running exe is unknown.");
            string downloadPath = exePath + ".new";

            try
            {
                await DownloadAsync(update, downloadPath, progress, cancellationToken);
                Verify(update, downloadPath);
                ReplaceExecutable(exePath, downloadPath);
            }
            finally
            {
                TryDelete(downloadPath);
            }

            EventLogService.Log("Update", $"Installed version {update.Version}");
            _installedVersion = update.Version;
            SetAvailable(null);
        }

        internal static void ReplaceExecutable(string exePath, string newFile)
        {
            string oldPath = exePath + ".old";
            TryDelete(oldPath);
            if (File.Exists(oldPath))
                oldPath = $"{exePath}.{Guid.NewGuid():N}.old";

            File.Move(exePath, oldPath, overwrite: true);
            try
            {
                File.Move(newFile, exePath, overwrite: true);
            }
            catch
            {
                try
                {
                    File.Move(oldPath, exePath, overwrite: true);
                }
                catch (Exception rollbackEx)
                {
                    EventLogService.Log("Update", "Rollback failed: " + rollbackEx.Message);
                }
                throw;
            }
        }

        public static void StartUpdatedInstance()
        {
            string exePath = AppInfo.ExecutablePath ?? throw new InvalidOperationException("The path of the running exe is unknown.");
            using Process? started = Process.Start(new ProcessStartInfo(exePath, $"{AfterUpdateArgument} {Environment.ProcessId}") { UseShellExecute = false });
        }

        public static void FinishPreviousUpdate(string[] args)
        {
            int index = Array.IndexOf(args, AfterUpdateArgument);
            if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int previousPid))
            {
                try
                {
                    using Process previous = Process.GetProcessById(previousPid);
                    string? expectedName = Path.GetFileNameWithoutExtension(AppInfo.ExecutablePath);
                    if (string.IsNullOrEmpty(expectedName) ||
                        string.Equals(previous.ProcessName, expectedName, StringComparison.OrdinalIgnoreCase))
                    {
                        previous.WaitForExit(15000);
                    }
                }
                catch (Exception ex)
                {
                    EventLogService.Log("Update", $"Skipped waiting for previous process {previousPid}: {ex.Message}");
                }
            }

            string? exePath = AppInfo.ExecutablePath;
            if (exePath != null)
            {
                Task.Run(async () =>
                {
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        TryDelete(exePath + ".old");
                        if (!File.Exists(exePath + ".old"))
                            break;
                        await Task.Delay(300).ConfigureAwait(false);
                    }

                    string? dir = Path.GetDirectoryName(exePath);
                    string fileName = Path.GetFileName(exePath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        try
                        {
                            foreach (string leftover in Directory.EnumerateFiles(dir, $"{fileName}.*.old"))
                                TryDelete(leftover);
                        }
                        catch { }
                    }
                });
            }
        }

        private const long MaxDownloadBytes = 100L * 1024 * 1024;

        internal static async Task DownloadAsync(UpdateInfo update, string path, IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (!IsAllowedDownloadUrl(update.DownloadUrl))
                throw new InvalidOperationException("Update download URL is not an allowed GitHub HTTPS URL.");

            using HttpResponseMessage response = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            if (response.RequestMessage?.RequestUri is Uri finalUri && !IsAllowedDownloadUri(finalUri))
                throw new InvalidOperationException("Update download redirected to a disallowed host.");

            long maxAllowed = update.Size > 0 ? Math.Min(MaxDownloadBytes, update.Size) : MaxDownloadBytes;
            long total = response.Content.Headers.ContentLength ?? update.Size;
            if (total > maxAllowed)
                throw new InvalidDataException("The update download exceeds the maximum allowed size.");

            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using FileStream target = File.Create(path);

            byte[] buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                received += read;
                if (received > maxAllowed)
                    throw new InvalidDataException("The update download exceeded the expected size limit.");

                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                if (total > 0)
                    progress.Report((double)received / total);
            }
        }

        internal static void Verify(UpdateInfo update, string path)
        {
            if (update.Size > 0 && new FileInfo(path).Length != update.Size)
                throw new InvalidDataException("The downloaded file is incomplete.");

            if (update.Sha256 != null)
            {
                using FileStream stream = File.OpenRead(path);
                string actual = Convert.ToHexString(SHA256.HashData(stream));
                if (!actual.Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded file is damaged (checksum mismatch).");
            }

            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            if (info.ProductName != ProductName ||
                !Version.TryParse(info.ProductVersion?.Split('+')[0], out Version? fileVersion) ||
                Normalize(fileVersion) != Normalize(update.Version))
            {
                throw new InvalidDataException("The downloaded file is not the expected version of Music Media Keys.");
            }
        }

        private static void SetAvailable(UpdateInfo? update)
        {
            if (Available == update)
                return;

            Available = update;
            AvailableChanged?.Invoke();
        }

        private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

        internal static string FormatVersion(Version v) => Normalize(v).ToString(3);

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                EventLogService.Log("Update", $"Could not delete {Path.GetFileName(path)}: {ex.Message}");
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"MusicMediaKeys/{AppInfo.Version}");
            return client;
        }
    }
}
