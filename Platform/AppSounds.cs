using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace MusicMediaKeys
{
    internal static class AppSounds
    {
        private const uint SND_ASYNC = 0x0001;
        private const uint SND_NODEFAULT = 0x0002;
        private const uint SND_MEMORY = 0x0004;
        private const uint SND_FILENAME = 0x00020000;

        [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool PlaySoundMemory(IntPtr pszSound, IntPtr hmod, uint fdwSound);

        [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool PlaySoundFile(string? pszSound, IntPtr hmod, uint fdwSound);

        private const int SampleRate = 44100;
        private const long MaxCustomSoundBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedExtensions = { ".wav", ".mp3", ".wma", ".m4a", ".aac" };
        private static readonly Lazy<byte[]> StartupJingleWaveLazy = new(GenerateStartupJingleWave, LazyThreadSafetyMode.PublicationOnly);
        private static readonly Lazy<byte[]> EasterEggWaveLazy = new(GenerateEasterEggWave, LazyThreadSafetyMode.PublicationOnly);
        private static readonly object _soundLock = new();
        private static MediaPlayer? _activeMediaPlayer;
        private static GCHandle _pinnedWavHandle;
        private static long _playbackGeneration;

        public static void StopAllSounds()
        {
            Interlocked.Increment(ref _playbackGeneration);
            StopCurrentPlayback();
        }

        private static void StopCurrentPlayback()
        {
            StopActiveMediaPlayer();
            lock (_soundLock)
            {
                StopWaveLocked();
            }
        }

        private static void StopWaveLocked()
        {
            try
            {
                PlaySoundMemory(IntPtr.Zero, IntPtr.Zero, 0);
            }
            catch
            {
            }

            if (_pinnedWavHandle.IsAllocated)
            {
                _pinnedWavHandle.Free();
            }
        }

        public static void PlayStartupSound(string? customFileName)
        {
            long gen = Interlocked.Increment(ref _playbackGeneration);
            StopCurrentPlayback();

            string? fullPath = ResolveSafeCustomSoundPath(customFileName);
            if (fullPath != null && File.Exists(fullPath))
            {
                string ext = Path.GetExtension(fullPath);
                if (ext.Equals(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    PlayWavFile(fullPath, fallbackToDefault: true);
                    return;
                }

                if (TryPlayViaMediaPlayer(fullPath, gen))
                    return;
            }

            PlayWaveBytesAsync(StartupJingleWaveLazy.Value);
        }

        public static void PlayStartupJingle()
        {
            Interlocked.Increment(ref _playbackGeneration);
            StopCurrentPlayback();
            PlayWaveBytesAsync(StartupJingleWaveLazy.Value);
        }

        public static void PlayEasterEggSound()
        {
            Interlocked.Increment(ref _playbackGeneration);
            StopCurrentPlayback();
            PlayWaveBytesAsync(EasterEggWaveLazy.Value);
        }

        internal static string SanitizeCustomSoundFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "";

            string trimmed = fileName.Trim();
            string onlyName = Path.GetFileName(trimmed);
            if (!string.Equals(trimmed, onlyName, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(onlyName) ||
                onlyName is "." or "..")
            {
                return "";
            }

            string ext = Path.GetExtension(onlyName).ToLowerInvariant();
            return Array.IndexOf(AllowedExtensions, ext) >= 0 ? onlyName : "";
        }

        internal static string? ResolveSafeCustomSoundPath(string? customFileName, string? targetDirectory = null)
        {
            string safeName = SanitizeCustomSoundFileName(customFileName);
            if (string.IsNullOrEmpty(safeName))
                return null;

            string baseDir = Path.GetFullPath(targetDirectory ?? AppPaths.DataDirectory);
            string baseDirWithSep = baseDir.EndsWith(Path.DirectorySeparatorChar)
                ? baseDir
                : baseDir + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(baseDir, safeName));

            return fullPath.StartsWith(baseDirWithSep, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : null;
        }

        private static void StopActiveMediaPlayer()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return;

            void ClosePlayer()
            {
                if (_activeMediaPlayer != null)
                {
                    try
                    {
                        _activeMediaPlayer.Stop();
                        _activeMediaPlayer.Close();
                    }
                    catch { }
                    _activeMediaPlayer = null;
                }
            }

            if (dispatcher.CheckAccess())
                ClosePlayer();
            else
                dispatcher.Invoke(ClosePlayer);
        }

        public static void SetCustomStartupSound(string sourceFilePath, AppSettings settings, string? targetDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
                throw new FileNotFoundException("Audio file not found.", sourceFilePath);

            var fi = new FileInfo(sourceFilePath);
            if (fi.Length == 0 || fi.Length > MaxCustomSoundBytes)
                throw new InvalidDataException(Localization.GetString("StartupSoundTooLarge"));

            string ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (Array.IndexOf(AllowedExtensions, ext) < 0)
                throw new InvalidDataException(Localization.GetString("StartupSoundInvalidFormat"));

            string dataDir = targetDirectory ?? AppPaths.DataDirectory;
            Directory.CreateDirectory(dataDir);

            StopAllSounds();
            DeleteExistingCustomFile(settings.CustomStartupSoundFile, dataDir);

            string destFileName = "custom_startup" + ext;
            string destPath = Path.Combine(dataDir, destFileName);
            File.Copy(sourceFilePath, destPath, overwrite: true);

            settings.CustomStartupSoundFile = destFileName;
            settings.Save();
        }

        public static void ResetCustomStartupSound(AppSettings settings, string? targetDirectory = null)
        {
            string dataDir = targetDirectory ?? AppPaths.DataDirectory;
            StopAllSounds();
            DeleteExistingCustomFile(settings.CustomStartupSoundFile, dataDir);
            settings.CustomStartupSoundFile = "";
            settings.Save();
        }

        private static void DeleteExistingCustomFile(string? fileName, string dataDir)
        {
            string? existing = ResolveSafeCustomSoundPath(fileName, dataDir);
            if (existing == null)
                return;

            try
            {
                if (File.Exists(existing))
                    File.Delete(existing);
            }
            catch
            {
            }
        }

        private static bool TryPlayViaMediaPlayer(string fullPath, long generation)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return false;

            void StartPlayer()
            {
                if (Volatile.Read(ref _playbackGeneration) != generation)
                    return;

                try
                {
                    if (_activeMediaPlayer != null)
                    {
                        _activeMediaPlayer.Stop();
                        _activeMediaPlayer.Close();
                        _activeMediaPlayer = null;
                    }

                    var player = new MediaPlayer { Volume = 0.5 };
                    player.MediaEnded += (s, e) =>
                    {
                        player.Close();
                        if (ReferenceEquals(_activeMediaPlayer, player))
                            _activeMediaPlayer = null;
                    };
                    player.MediaFailed += (s, e) =>
                    {
                        player.Close();
                        if (ReferenceEquals(_activeMediaPlayer, player))
                            _activeMediaPlayer = null;
                        if (Volatile.Read(ref _playbackGeneration) == generation)
                            PlayStartupJingle();
                    };
                    player.Open(new Uri(fullPath, UriKind.Absolute));
                    player.Play();
                    _activeMediaPlayer = player;
                }
                catch
                {
                    if (Volatile.Read(ref _playbackGeneration) == generation)
                        PlayStartupJingle();
                }
            }

            if (dispatcher.CheckAccess())
                StartPlayer();
            else
                dispatcher.BeginInvoke(new Action(StartPlayer));

            return true;
        }

        private static void PlayWavFile(string fullPath, bool fallbackToDefault)
        {
            bool started = false;
            try
            {
                lock (_soundLock)
                {
                    StopWaveLocked();
                    started = PlaySoundFile(fullPath, IntPtr.Zero, SND_FILENAME | SND_ASYNC | SND_NODEFAULT);
                }
            }
            catch
            {
                started = false;
            }

            if (!started && fallbackToDefault)
            {
                PlayWaveBytesAsync(StartupJingleWaveLazy.Value);
            }
        }

        private static void PlayWaveBytesAsync(byte[] wavBytes)
        {
            try
            {
                lock (_soundLock)
                {
                    StopWaveLocked();
                    _pinnedWavHandle = GCHandle.Alloc(wavBytes, GCHandleType.Pinned);
                    if (!PlaySoundMemory(_pinnedWavHandle.AddrOfPinnedObject(), IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT))
                    {
                        if (_pinnedWavHandle.IsAllocated)
                            _pinnedWavHandle.Free();
                    }
                }
            }
            catch
            {
                lock (_soundLock)
                {
                    if (_pinnedWavHandle.IsAllocated)
                        _pinnedWavHandle.Free();
                }
            }
        }

        internal static byte[] GenerateStartupJingleWave()
        {
            const double duration = 0.85;
            const double fadeOutStart = 0.62;
            double[] freqs = { 523.25, 659.25, 783.99, 1174.66 };
            return GenerateWaveBytes(duration, t =>
            {
                double attack = Math.Min(1.0, t / 0.025);
                double tailFade = t >= fadeOutStart
                    ? 0.5 * (1.0 + Math.Cos(Math.PI * (t - fadeOutStart) / (duration - fadeOutStart)))
                    : 1.0;

                double sum = 0.0;
                for (int i = 0; i < freqs.Length; i++)
                {
                    double delay = i * 0.04;
                    if (t >= delay)
                    {
                        double localT = t - delay;
                        double noteEnv = Math.Min(1.0, localT / 0.018) * Math.Exp(-localT * 4.2);
                        sum += Math.Sin(2.0 * Math.PI * freqs[i] * localT) * noteEnv;
                    }
                }
                return (sum / freqs.Length) * 0.16 * attack * tailFade;
            });
        }

        internal static byte[] GenerateEasterEggWave()
        {
            double[] notes = { 523.25, 659.25, 783.99, 1046.50 };
            const double noteDur = 0.10;
            return GenerateWaveBytes(0.48, t =>
            {
                int idx = Math.Min(notes.Length - 1, (int)(t / noteDur));
                double localT = t - idx * noteDur;
                double env = Math.Min(1.0, localT / 0.008) * Math.Exp(-localT * 10.0);
                if (idx == notes.Length - 1)
                    env = Math.Min(1.0, localT / 0.008) * Math.Exp(-localT * 6.0);

                double fundamental = Math.Sin(2.0 * Math.PI * notes[idx] * localT);
                double overtone = 0.25 * Math.Sin(4.0 * Math.PI * notes[idx] * localT);
                return (fundamental + overtone) * 0.12 * env;
            });
        }

        internal static byte[] GenerateWaveBytes(double durationSeconds, Func<double, double> sampleSelector)
        {
            int sampleCount = Math.Max(1, (int)(SampleRate * durationSeconds));
            int dataSize = sampleCount * 2;
            using var ms = new MemoryStream(44 + dataSize);
            using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(SampleRate);
            bw.Write(SampleRate * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);

            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / SampleRate;
                double sample = Math.Clamp(sampleSelector(t), -1.0, 1.0);
                bw.Write((short)Math.Round(sample * short.MaxValue));
            }

            bw.Flush();
            return ms.ToArray();
        }
    }
}
