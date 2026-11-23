using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MusicMediaKeys
{
    internal sealed class MediaVolumeService
    {
        private const int ClsCtxAll = 0x17;
        private const float DefaultVolumeStep = 0.05f;
        private static readonly Guid EmptyGuid = Guid.Empty;

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator
        {
        }

        private enum EDataFlow
        {
            eRender,
            eCapture,
            eAll
        }

        private enum ERole
        {
            eConsole,
            eMultimedia,
            eCommunications
        }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl();

            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        private static void LogFailure(string action, Exception ex)
        {
            EventLogService.Log("Volume", $"{action} failed: {ex.Message}");
        }

        public sealed class VolumeSessionContext : IDisposable
        {
            private readonly bool _useSystemVolume;
            private readonly List<object> _volumeObjects = new();

            public VolumeSessionContext(bool useSystemVolume, IEnumerable<int>? processIds)
            {
                _useSystemVolume = useSystemVolume || processIds == null;
                try
                {
                    if (_useSystemVolume)
                    {
                        if (DefaultDeviceVolume.TryGetEndpointVolume(out var vol) && vol != null)
                        {
                            _volumeObjects.Add(vol);
                        }
                    }
                    else
                    {
                        foreach (int pid in processIds!)
                        {
                            if (pid == MediaPlayerProviders.OurProcessId)
                                continue;

                            if (CoreAudioVolume.TryGetVolumeForProcess(pid, out var vol) && vol != null)
                            {
                                _volumeObjects.Add(vol);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogFailure("Opening the volume controls", ex);
                }
            }

            public void SetVolume(float level)
            {
                foreach (var volObj in _volumeObjects)
                {
                    try
                    {
                        if (_useSystemVolume && volObj is DefaultDeviceVolume.IAudioEndpointVolume endpointVol)
                        {
                            endpointVol.SetMasterVolumeLevelScalar(level, Guid.Empty);
                            if (level > 0f && endpointVol.GetMute(out bool muted) == 0 && muted)
                            {
                                endpointVol.SetMute(false, Guid.Empty);
                            }
                        }
                        else if (volObj is CoreAudioVolume.ISimpleAudioVolume simpleVol)
                        {
                            simpleVol.SetMasterVolume(level, Guid.Empty);
                            if (level > 0f && simpleVol.GetMute(out bool muted) == 0 && muted)
                            {
                                simpleVol.SetMute(false, Guid.Empty);
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }

            public float GetVolume()
            {
                foreach (var volObj in _volumeObjects)
                {
                    try
                    {
                        if (_useSystemVolume && volObj is DefaultDeviceVolume.IAudioEndpointVolume endpointVol)
                        {
                            if (endpointVol.GetMasterVolumeLevelScalar(out float level) == 0)
                                return Math.Clamp(level, 0f, 1f);
                        }
                        else if (volObj is CoreAudioVolume.ISimpleAudioVolume simpleVol)
                        {
                            if (simpleVol.GetMasterVolume(out float level) == 0)
                                return Math.Clamp(level, 0f, 1f);
                        }
                    }
                    catch { }
                }
                return 1.0f;
            }

            public bool GetMute()
            {
                foreach (var volObj in _volumeObjects)
                {
                    try
                    {
                        if (_useSystemVolume && volObj is DefaultDeviceVolume.IAudioEndpointVolume endpointVol)
                        {
                            if (endpointVol.GetMute(out bool muted) == 0)
                                return muted;
                        }
                        else if (volObj is CoreAudioVolume.ISimpleAudioVolume simpleVol)
                        {
                            if (simpleVol.GetMute(out bool muted) == 0)
                                return muted;
                        }
                    }
                    catch { }
                }
                return false;
            }

            public void SetMute(bool muted)
            {
                foreach (var volObj in _volumeObjects)
                {
                    try
                    {
                        if (_useSystemVolume && volObj is DefaultDeviceVolume.IAudioEndpointVolume endpointVol)
                            endpointVol.SetMute(muted, Guid.Empty);
                        else if (volObj is CoreAudioVolume.ISimpleAudioVolume simpleVol)
                            simpleVol.SetMute(muted, Guid.Empty);
                    }
                    catch { }
                }
            }

            public void Dispose()
            {
                foreach (var volObj in _volumeObjects)
                {
                    if (volObj != null && Marshal.IsComObject(volObj))
                    {
                        try { Marshal.ReleaseComObject(volObj); } catch { }
                    }
                }
                _volumeObjects.Clear();
            }
        }

        public VolumeSessionContext CreateSessionContext(bool useSystemVolume, IEnumerable<int>? processIds)
        {
            return new VolumeSessionContext(useSystemVolume, processIds);
        }

        public bool TryAdjustDefaultDevice(MediaCommand command, float step = 0.05f)
        {
            return command switch
            {
                MediaCommand.VolumeUp => DefaultDeviceVolume.TryVolumeUp(step),
                MediaCommand.VolumeDown => DefaultDeviceVolume.TryVolumeDown(step),
                MediaCommand.VolumeMute => DefaultDeviceVolume.TryToggleMute(),
                _ => false
            };
        }

        public bool TryAdjustTargetProcesses(MediaCommand command, IEnumerable<int> processIds, float step = 0.05f)
        {
            try
            {
                foreach (int pid in processIds)
                {
                    if (pid == MediaPlayerProviders.OurProcessId)
                        continue;

                    try
                    {
                        bool ok = command switch
                        {
                            MediaCommand.VolumeUp => CoreAudioVolume.TryVolumeUp(pid, step),
                            MediaCommand.VolumeDown => CoreAudioVolume.TryVolumeDown(pid, step),
                            MediaCommand.VolumeMute => CoreAudioVolume.TryToggleMute(pid),
                            _ => false
                        };

                        if (ok)
                            return true;
                    }
                    catch
                    {
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public float GetVolume(bool useSystemVolume, System.Collections.Generic.IEnumerable<int>? processIds)
        {
            if (useSystemVolume || processIds == null)
            {
                if (DefaultDeviceVolume.TryGetEndpointVolume(out var vol) && vol != null)
                {
                    try
                    {
                        vol.GetMasterVolumeLevelScalar(out float level);
                        return level;
                    }
                    catch (Exception ex) { LogFailure("Reading the volume", ex); }
                    finally
                    {
                        if (Marshal.IsComObject(vol)) Marshal.ReleaseComObject(vol);
                    }
                }
                return 0f;
            }

            foreach (int pid in processIds)
            {
                if (pid == MediaPlayerProviders.OurProcessId)
                    continue;

                if (CoreAudioVolume.TryGetVolumeForProcess(pid, out var vol) && vol != null)
                {
                    try
                    {
                        vol.GetMasterVolume(out float level);
                        return level;
                    }
                    catch (Exception ex) { LogFailure("Reading the volume", ex); }
                    finally
                    {
                        if (Marshal.IsComObject(vol)) Marshal.ReleaseComObject(vol);
                    }
                }
            }

            return 0f;
        }

        public void SetVolume(bool useSystemVolume, System.Collections.Generic.IEnumerable<int>? processIds, float level)
        {
            if (useSystemVolume || processIds == null)
            {
                if (DefaultDeviceVolume.TryGetEndpointVolume(out var vol) && vol != null)
                {
                    try
                    {
                        vol.SetMasterVolumeLevelScalar(level, Guid.Empty);
                        if (level > 0f && vol.GetMute(out bool muted) == 0 && muted)
                        {
                            vol.SetMute(false, Guid.Empty);
                        }
                    }
                    catch (Exception ex) { LogFailure("Setting the volume", ex); }
                    finally
                    {
                        if (Marshal.IsComObject(vol)) Marshal.ReleaseComObject(vol);
                    }
                }
                return;
            }

            foreach (int pid in processIds)
            {
                if (pid == MediaPlayerProviders.OurProcessId)
                    continue;

                if (CoreAudioVolume.TryGetVolumeForProcess(pid, out var vol) && vol != null)
                {
                    try
                    {
                        vol.SetMasterVolume(level, Guid.Empty);
                        if (level > 0f && vol.GetMute(out bool muted) == 0 && muted)
                        {
                            vol.SetMute(false, Guid.Empty);
                        }
                    }
                    catch (Exception ex) { LogFailure("Setting the volume", ex); }
                    finally
                    {
                        if (Marshal.IsComObject(vol)) Marshal.ReleaseComObject(vol);
                    }
                }
            }
        }

        public bool GetMute(bool useSystemVolume, System.Collections.Generic.IEnumerable<int>? processIds)
        {
            if (useSystemVolume || processIds == null)
            {
                if (DefaultDeviceVolume.TryGetEndpointVolume(out var vol) && vol != null)
                {
                    try
                    {
                        vol.GetMute(out bool mute);
                        return mute;
                    }
                    catch (Exception ex) { LogFailure("Reading mute", ex); }
                    finally
                    {
                        if (Marshal.IsComObject(vol)) Marshal.ReleaseComObject(vol);
                    }
                }
                return false;
            }

            foreach (int pid in processIds)
            {
                if (pid == MediaPlayerProviders.OurProcessId)
                    continue;

                if (CoreAudioVolume.TryGetVolumeForProcess(pid, out var vol) && vol != null)
                {
                    try
                    {
                        vol.GetMute(out bool mute);
                        return mute;
                    }
                    catch (Exception ex) { LogFailure("Reading mute", ex); }
                    finally
                    {
                        if (Marshal.IsComObject(vol)) Marshal.ReleaseComObject(vol);
                    }
                }
            }

            return false;
        }

        public void SetMute(bool useSystemVolume, System.Collections.Generic.IEnumerable<int>? processIds, bool mute)
        {
            if (useSystemVolume || processIds == null)
            {
                if (DefaultDeviceVolume.TryGetEndpointVolume(out var vol) && vol != null)
                {
                    try
                    {
                        vol.SetMute(mute, Guid.Empty);
                    }
                    catch (Exception ex) { LogFailure("Setting mute", ex); }
                    finally
                    {
                        if (Marshal.IsComObject(vol)) Marshal.ReleaseComObject(vol);
                    }
                }
                return;
            }

            foreach (int pid in processIds)
            {
                if (pid == MediaPlayerProviders.OurProcessId)
                    continue;

                if (CoreAudioVolume.TryGetVolumeForProcess(pid, out var vol) && vol != null)
                {
                    try
                    {
                        vol.SetMute(mute, Guid.Empty);
                    }
                    catch (Exception ex) { LogFailure("Setting mute", ex); }
                    finally
                    {
                        if (Marshal.IsComObject(vol)) Marshal.ReleaseComObject(vol);
                    }
                }
            }
        }

        internal static class DefaultDeviceVolume
        {
            [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            internal interface IAudioEndpointVolume
            {
                [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
                [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
                [PreserveSig] int GetChannelCount(out int channelCount);
                [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, [MarshalAs(UnmanagedType.LPStruct)] Guid pguidEventContext);
                [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, [MarshalAs(UnmanagedType.LPStruct)] Guid pguidEventContext);
                [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
                [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
                [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, [MarshalAs(UnmanagedType.LPStruct)] Guid pguidEventContext);
                [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, [MarshalAs(UnmanagedType.LPStruct)] Guid pguidEventContext);
                [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
                [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
                [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, [MarshalAs(UnmanagedType.LPStruct)] Guid pguidEventContext);
                [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
                [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
                [PreserveSig] int VolumeStepUp([MarshalAs(UnmanagedType.LPStruct)] Guid pguidEventContext);
                [PreserveSig] int VolumeStepDown([MarshalAs(UnmanagedType.LPStruct)] Guid pguidEventContext);
                [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
                [PreserveSig] int GetVolumeRange(out float pflVolumeMinDB, out float pflVolumeMaxDB, out float pflVolumeIncrementDB);
            }

            public static bool TryVolumeUp(float step = DefaultVolumeStep)
            {
                if (!TryGetEndpointVolume(out IAudioEndpointVolume? volume) || volume == null)
                    return false;

                try
                {
                    if (volume.GetMasterVolumeLevelScalar(out float level) != 0)
                        return false;

                    return volume.SetMasterVolumeLevelScalar(Math.Min(1f, level + step), EmptyGuid) == 0;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    if (Marshal.IsComObject(volume)) Marshal.ReleaseComObject(volume);
                }
            }

            public static bool TryVolumeDown(float step = DefaultVolumeStep)
            {
                if (!TryGetEndpointVolume(out IAudioEndpointVolume? volume) || volume == null)
                    return false;

                try
                {
                    if (volume.GetMasterVolumeLevelScalar(out float level) != 0)
                        return false;

                    return volume.SetMasterVolumeLevelScalar(Math.Max(0f, level - step), EmptyGuid) == 0;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    if (Marshal.IsComObject(volume)) Marshal.ReleaseComObject(volume);
                }
            }

            public static bool TryToggleMute()
            {
                if (!TryGetEndpointVolume(out IAudioEndpointVolume? volume) || volume == null)
                    return false;

                try
                {
                    if (volume.GetMute(out bool mute) != 0)
                        return false;

                    return volume.SetMute(!mute, EmptyGuid) == 0;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    if (Marshal.IsComObject(volume)) Marshal.ReleaseComObject(volume);
                }
            }

            internal static bool TryGetEndpointVolume(out IAudioEndpointVolume? volume)
            {
                volume = null;
                IMMDeviceEnumerator? enumerator = null;
                IMMDevice? device = null;
                try
                {

                    enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                    if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out device) != 0 || device == null)
                        return false;

                    Guid iid = typeof(IAudioEndpointVolume).GUID;
                    if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out object? instance) != 0 || instance == null)
                        return false;

                    volume = (IAudioEndpointVolume)instance;
                    return true;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    if (device != null && Marshal.IsComObject(device))
                        Marshal.ReleaseComObject(device);
                    if (enumerator != null && Marshal.IsComObject(enumerator))
                        Marshal.ReleaseComObject(enumerator);
                }
            }
        }

        private static class CoreAudioVolume
        {
            [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IAudioSessionManager2
            {
                [PreserveSig] int GetAudioSessionControl([MarshalAs(UnmanagedType.LPStruct)] Guid audioSessionGuid, int streamFlags, out IntPtr sessionControl);
                [PreserveSig] int GetSimpleAudioVolume([MarshalAs(UnmanagedType.LPStruct)] Guid audioSessionGuid, int streamFlags, out ISimpleAudioVolume audioVolume);
                [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
            }

            [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IAudioSessionEnumerator
            {
                [PreserveSig] int GetCount(out int sessionCount);
                [PreserveSig] int GetSession(int sessionCount, out IAudioSessionControl2 session);
            }

            [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private interface IAudioSessionControl2
            {
                [PreserveSig] int GetState(out int pRetVal);
                [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
                [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, [MarshalAs(UnmanagedType.LPStruct)] Guid guid);
                [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
                [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, [MarshalAs(UnmanagedType.LPStruct)] Guid guid);
                [PreserveSig] int GetGroupingParam(out Guid pRetVal);
                [PreserveSig] int SetGroupingParam([MarshalAs(UnmanagedType.LPStruct)] Guid guid, [MarshalAs(UnmanagedType.LPStruct)] Guid guid2);
                [PreserveSig] int RegisterDuckNotification(IntPtr pNotify, IntPtr cookie);
                [PreserveSig] int UnregisterDuckNotification(IntPtr cookie);
                [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
                [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
                [PreserveSig] int GetProcessId(out int pRetVal);
                [PreserveSig] int IsSystemSoundsSession();
                [PreserveSig] int SetDuckingPreference(bool optOut);
            }

            [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            internal interface ISimpleAudioVolume
            {
                [PreserveSig] int SetMasterVolume(float fLevel, [MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);
                [PreserveSig] int GetMasterVolume(out float pfLevel);
                [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, [MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);
                [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
            }

            public static bool TryVolumeUp(int processId, float step = DefaultVolumeStep)
            {
                if (!TryGetVolumeForProcess(processId, out ISimpleAudioVolume? volume) || volume == null)
                    return false;

                try
                {
                    if (volume.GetMasterVolume(out float level) != 0)
                        return false;

                    return volume.SetMasterVolume(Math.Min(1f, level + step), EmptyGuid) == 0;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    if (Marshal.IsComObject(volume)) Marshal.ReleaseComObject(volume);
                }
            }

            public static bool TryVolumeDown(int processId, float step = DefaultVolumeStep)
            {
                if (!TryGetVolumeForProcess(processId, out ISimpleAudioVolume? volume) || volume == null)
                    return false;

                try
                {
                    if (volume.GetMasterVolume(out float level) != 0)
                        return false;

                    return volume.SetMasterVolume(Math.Max(0f, level - step), EmptyGuid) == 0;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    if (Marshal.IsComObject(volume)) Marshal.ReleaseComObject(volume);
                }
            }

            public static bool TryToggleMute(int processId)
            {
                if (!TryGetVolumeForProcess(processId, out ISimpleAudioVolume? volume) || volume == null)
                    return false;

                try
                {
                    if (volume.GetMute(out bool mute) != 0)
                        return false;

                    return volume.SetMute(!mute, EmptyGuid) == 0;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    if (Marshal.IsComObject(volume)) Marshal.ReleaseComObject(volume);
                }
            }

            internal static bool TryGetVolumeForProcess(int processId, out ISimpleAudioVolume? volume)
            {
                volume = null;
                if (processId == MediaPlayerProviders.OurProcessId)
                    return false;

                IMMDeviceEnumerator? enumerator = null;
                IMMDevice? device = null;
                IAudioSessionManager2? manager = null;
                IAudioSessionEnumerator? sessionEnum = null;
                try
                {

                    enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                    if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out device) != 0 || device == null)
                        return false;

                    Guid iidMgr = typeof(IAudioSessionManager2).GUID;
                    if (device.Activate(ref iidMgr, ClsCtxAll, IntPtr.Zero, out object? instance) != 0 || instance == null)
                        return false;

                    manager = (IAudioSessionManager2)instance;
                    if (manager.GetSessionEnumerator(out sessionEnum) != 0 || sessionEnum == null)
                        return false;
                    if (sessionEnum.GetCount(out int count) != 0)
                        return false;

                    for (int i = 0; i < count; i++)
                    {
                        if (sessionEnum.GetSession(i, out IAudioSessionControl2? control) != 0 || control == null)
                            continue;

                        bool keepAliveForCaller = false;
                        try
                        {
                            if (control.GetProcessId(out int pid) != 0 || pid != processId)
                                continue;

                            if (control is ISimpleAudioVolume simpleVol)
                            {
                                volume = simpleVol;
                                keepAliveForCaller = true;
                                return true;
                            }
                        }
                        finally
                        {
                            if (!keepAliveForCaller && Marshal.IsComObject(control))
                                Marshal.ReleaseComObject(control);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogFailure($"Finding the audio session of process {processId}", ex);
                }
                finally
                {
                    if (sessionEnum != null && Marshal.IsComObject(sessionEnum))
                        Marshal.ReleaseComObject(sessionEnum);
                    if (manager != null && Marshal.IsComObject(manager))
                        Marshal.ReleaseComObject(manager);
                    if (device != null && Marshal.IsComObject(device))
                        Marshal.ReleaseComObject(device);
                    if (enumerator != null && Marshal.IsComObject(enumerator))
                        Marshal.ReleaseComObject(enumerator);
                }

                return false;
            }
        }
    }
}
