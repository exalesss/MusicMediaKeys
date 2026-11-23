using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;

namespace MusicMediaKeys
{
    internal enum SmtcPlaybackStatus
    {
        Closed = 0,
        Opened = 1,
        Changing = 2,
        Stopped = 3,
        Playing = 4,
        Paused = 5
    }

    internal sealed record SmtcTimelineProperties(TimeSpan Position, TimeSpan EndTime, DateTimeOffset LastUpdatedTime);

    internal sealed record SmtcMediaProperties(string Title, string Artist, byte[]? ThumbnailBytes);

    internal static unsafe class WinRtNative
    {
        public static readonly Guid IID_IAgileObject = new("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90");
        public static readonly Guid IID_IAsyncInfo = new("00000036-0000-0000-C000-000000000046");
        public static readonly Guid IID_IClosable = new("30d5a829-7fa4-4026-83bb-d75bae4ea99e");
        public static readonly Guid IID_IStream = new("0000000c-0000-0000-C000-000000000046");
        public static readonly Guid IID_ISessionManagerStatics = new("2050c4ee-11a0-57de-aed7-c97c70338245");

        public static readonly Guid IID_IAsyncOpCompleted_Manager = new("10f0074e-923d-5510-8f4a-dde37754ca0e");
        public static readonly Guid IID_IAsyncOpCompleted_Bool = new("c1d3d1a2-ae17-5a5f-b5a2-bdcc8844889a");
        public static readonly Guid IID_IAsyncOpCompleted_MediaProps = new("84593a3d-951a-55b6-8353-5205e577797b");
        public static readonly Guid IID_IAsyncOpCompleted_RandomAccessStream = new("3dddecf4-1d39-58e8-83b1-dbed541c7f35");

        public static readonly Guid IID_TypedEventHandler_CurrentSessionChanged = new("228bd0ed-1fa2-5e9b-a6ec-42566173103b");
        public static readonly Guid IID_TypedEventHandler_SessionsChanged = new("2e2a8630-dc8c-530a-9746-bc984d4b029e");
        public static readonly Guid IID_TypedEventHandler_MediaPropertiesChanged = new("0f2ce2b7-afa7-5ed0-8cb6-8c40cf9b3a5f");
        public static readonly Guid IID_TypedEventHandler_PlaybackInfoChanged = new("2bdf1426-d41f-5896-897f-efc0b0fa7392");
        public static readonly Guid IID_TypedEventHandler_TimelinePropertiesChanged = new("e8bf62af-fac1-5fff-9053-0bf191ae777e");

        [DllImport("combase.dll", ExactSpelling = true)]
        public static extern int WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
            uint length,
            out IntPtr hstring);

        [DllImport("combase.dll", ExactSpelling = true)]
        public static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll", ExactSpelling = true)]
        public static extern char* WindowsGetStringRawBuffer(IntPtr hstring, out uint length);

        [DllImport("combase.dll", ExactSpelling = true)]
        public static extern int RoGetActivationFactory(IntPtr activatableClassId, in Guid iid, out IntPtr factory);

        [DllImport("shcore.dll", ExactSpelling = true)]
        public static extern int CreateStreamOverRandomAccessStream(IntPtr randomAccessStream, in Guid riid, out IntPtr ppv);

        public static string HStringToString(IntPtr hstring)
        {
            if (hstring == IntPtr.Zero)
                return string.Empty;

            try
            {
                char* ptr = WindowsGetStringRawBuffer(hstring, out uint len);
                return ptr != null && len > 0 ? new string(ptr, 0, (int)len) : string.Empty;
            }
            finally
            {
                WindowsDeleteString(hstring);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void** VTable(IntPtr obj) => *(void***)obj;

        public static void TryCloseAndRelease(IntPtr obj)
        {
            if (obj == IntPtr.Zero)
                return;

            try
            {
                Guid closableIid = IID_IClosable;
                if (Marshal.QueryInterface(obj, in closableIid, out IntPtr closable) == 0 && closable != IntPtr.Zero)
                {
                    try
                    {
                        ((delegate* unmanaged[Stdcall]<IntPtr, int>)VTable(closable)[6])(closable);
                    }
                    finally
                    {
                        Marshal.Release(closable);
                    }
                }
            }
            catch { }
            finally
            {
                Marshal.Release(obj);
            }
        }

        public static void TryCloseAsyncInfo(IntPtr asyncOp)
        {
            if (asyncOp == IntPtr.Zero)
                return;

            try
            {
                Guid asyncInfoIid = IID_IAsyncInfo;
                if (Marshal.QueryInterface(asyncOp, in asyncInfoIid, out IntPtr asyncInfo) == 0 && asyncInfo != IntPtr.Zero)
                {
                    try
                    {
                        ((delegate* unmanaged[Stdcall]<IntPtr, int>)VTable(asyncInfo)[10])(asyncInfo);
                    }
                    finally
                    {
                        Marshal.Release(asyncInfo);
                    }
                }
            }
            catch { }
        }

        public static Task<IntPtr> AwaitPtrAsync(IntPtr asyncOpPtr, Guid completedHandlerIid)
        {
            if (asyncOpPtr == IntPtr.Zero)
                return Task.FromResult(IntPtr.Zero);

            var tcs = new TaskCompletionSource<IntPtr>(TaskCreationOptions.RunContinuationsAsynchronously);
            var callback = new WinRtCallback(completedHandlerIid, (sender, statusPtr) =>
            {
                int status = unchecked((int)(long)statusPtr);
                try
                {
                    if (status == 1)
                    {
                        IntPtr result = IntPtr.Zero;
                        int hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)VTable(sender)[8])(sender, &result);
                        tcs.TrySetResult(hr == 0 ? result : IntPtr.Zero);
                    }
                    else
                    {
                        tcs.TrySetResult(IntPtr.Zero);
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    TryCloseAsyncInfo(sender);
                    Marshal.Release(sender);
                }
            });

            IntPtr handlerPtr = WinRtCallbackWrappers.Instance.GetOrCreateComInterfaceForObject(callback, CreateComInterfaceFlags.None);
            try
            {
                int hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)VTable(asyncOpPtr)[6])(asyncOpPtr, handlerPtr);
                if (hr != 0)
                {
                    TryCloseAsyncInfo(asyncOpPtr);
                    Marshal.Release(asyncOpPtr);
                    tcs.TrySetResult(IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                TryCloseAsyncInfo(asyncOpPtr);
                Marshal.Release(asyncOpPtr);
                tcs.TrySetException(ex);
            }
            finally
            {
                Marshal.Release(handlerPtr);
            }

            return tcs.Task;
        }

        public static Task<bool> AwaitBoolAsync(IntPtr asyncOpPtr)
        {
            if (asyncOpPtr == IntPtr.Zero)
                return Task.FromResult(false);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var callback = new WinRtCallback(IID_IAsyncOpCompleted_Bool, (sender, statusPtr) =>
            {
                int status = unchecked((int)(long)statusPtr);
                try
                {
                    if (status == 1)
                    {
                        byte result = 0;
                        int hr = ((delegate* unmanaged[Stdcall]<IntPtr, byte*, int>)VTable(sender)[8])(sender, &result);
                        tcs.TrySetResult(hr == 0 && result != 0);
                    }
                    else
                    {
                        tcs.TrySetResult(false);
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    TryCloseAsyncInfo(sender);
                    Marshal.Release(sender);
                }
            });

            IntPtr handlerPtr = WinRtCallbackWrappers.Instance.GetOrCreateComInterfaceForObject(callback, CreateComInterfaceFlags.None);
            try
            {
                int hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)VTable(asyncOpPtr)[6])(asyncOpPtr, handlerPtr);
                if (hr != 0)
                {
                    TryCloseAsyncInfo(asyncOpPtr);
                    Marshal.Release(asyncOpPtr);
                    tcs.TrySetResult(false);
                }
            }
            catch (Exception ex)
            {
                TryCloseAsyncInfo(asyncOpPtr);
                Marshal.Release(asyncOpPtr);
                tcs.TrySetException(ex);
            }
            finally
            {
                Marshal.Release(handlerPtr);
            }

            return tcs.Task;
        }

        public static byte[]? ReadRandomAccessStreamBytes(IntPtr randomAccessStream)
        {
            if (randomAccessStream == IntPtr.Zero)
                return null;

            const int MaxThumbnailBytes = 10 * 1024 * 1024;
            IntPtr iStreamPtr = IntPtr.Zero;
            object? rcw = null;
            try
            {
                int hr = CreateStreamOverRandomAccessStream(randomAccessStream, IID_IStream, out iStreamPtr);
                if (hr != 0 || iStreamPtr == IntPtr.Zero)
                    return null;

                rcw = Marshal.GetObjectForIUnknown(iStreamPtr);
                if (rcw is not IStream stream)
                    return null;

                stream.Seek(0, 0, IntPtr.Zero);
                using var ms = new MemoryStream();
                byte[] buffer = new byte[16384];
                IntPtr bytesReadPtr = Marshal.AllocCoTaskMem(sizeof(int));
                try
                {
                    while (true)
                    {
                        Marshal.WriteInt32(bytesReadPtr, 0);
                        stream.Read(buffer, buffer.Length, bytesReadPtr);
                        int read = Marshal.ReadInt32(bytesReadPtr);
                        if (read <= 0)
                            break;
                        if (ms.Length + read > MaxThumbnailBytes)
                            return null;
                        ms.Write(buffer, 0, read);
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(bytesReadPtr);
                }

                return ms.Length > 0 ? ms.ToArray() : null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (rcw != null && Marshal.IsComObject(rcw))
                    Marshal.ReleaseComObject(rcw);
                if (iStreamPtr != IntPtr.Zero)
                    Marshal.Release(iStreamPtr);
            }
        }
    }

    internal sealed class WinRtCallback
    {
        public Guid HandlerIid { get; }
        private readonly Action<IntPtr, IntPtr> _handler;

        public WinRtCallback(Guid handlerIid, Action<IntPtr, IntPtr> handler)
        {
            HandlerIid = handlerIid;
            _handler = handler;
        }

        public void Invoke(IntPtr arg1, IntPtr arg2) => _handler(arg1, arg2);
    }

    internal sealed unsafe class WinRtCallbackWrappers : ComWrappers
    {
        public static WinRtCallbackWrappers Instance { get; } = new();

        private static readonly IntPtr HandlerVtable;
        private static readonly IntPtr AgileVtable;

        static WinRtCallbackWrappers()
        {
            GetIUnknownImpl(out IntPtr fpQueryInterface, out IntPtr fpAddRef, out IntPtr fpRelease);

            HandlerVtable = (IntPtr)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(WinRtCallbackWrappers), IntPtr.Size * 4);
            IntPtr* hVtbl = (IntPtr*)HandlerVtable;
            hVtbl[0] = fpQueryInterface;
            hVtbl[1] = fpAddRef;
            hVtbl[2] = fpRelease;
            hVtbl[3] = (IntPtr)(delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, int>)&InvokeStub;

            AgileVtable = (IntPtr)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(WinRtCallbackWrappers), IntPtr.Size * 3);
            IntPtr* aVtbl = (IntPtr*)AgileVtable;
            aVtbl[0] = fpQueryInterface;
            aVtbl[1] = fpAddRef;
            aVtbl[2] = fpRelease;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static int InvokeStub(IntPtr thisPtr, IntPtr arg1, IntPtr arg2)
        {
            try
            {
                var callback = ComInterfaceDispatch.GetInstance<WinRtCallback>((ComInterfaceDispatch*)thisPtr);
                callback.Invoke(arg1, arg2);
                return 0;
            }
            catch
            {
                return unchecked((int)0x80004005);
            }
        }

        protected override ComInterfaceEntry* ComputeVtables(object obj, CreateComInterfaceFlags flags, out int count)
        {
            if (obj is not WinRtCallback cb)
            {
                count = 0;
                return null;
            }

            var entries = (ComInterfaceEntry*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(WinRtCallbackWrappers), sizeof(ComInterfaceEntry) * 2);
            entries[0].IID = cb.HandlerIid;
            entries[0].Vtable = HandlerVtable;
            entries[1].IID = WinRtNative.IID_IAgileObject;
            entries[1].Vtable = AgileVtable;
            count = 2;
            return entries;
        }

        protected override object? CreateObject(IntPtr externalComObject, CreateObjectFlags flags) => null;

        protected override void ReleaseObjects(IEnumerable objects) { }
    }

    internal sealed class SmtcSessionManager : IDisposable
    {
        private IntPtr _ptr;
        private int _disposed;
        private long _currentSessionToken;
        private long _sessionsToken;
        private EventHandler? _currentSessionChanged;
        private EventHandler? _sessionsChanged;
        private WinRtCallback? _currentSessionCallback;
        private WinRtCallback? _sessionsCallback;

        private SmtcSessionManager(IntPtr ptr)
        {
            _ptr = ptr;
        }

        public static async Task<SmtcSessionManager?> RequestAsync()
        {
            IntPtr asyncOp = BeginRequestAsync();
            if (asyncOp == IntPtr.Zero)
                return null;

            IntPtr managerPtr = await WinRtNative.AwaitPtrAsync(asyncOp, WinRtNative.IID_IAsyncOpCompleted_Manager).ConfigureAwait(false);
            return managerPtr != IntPtr.Zero ? new SmtcSessionManager(managerPtr) : null;
        }

        private static unsafe IntPtr BeginRequestAsync()
        {
            const string className = "Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager";
            int hr = WinRtNative.WindowsCreateString(className, (uint)className.Length, out IntPtr hClassName);
            if (hr != 0)
                return IntPtr.Zero;

            IntPtr factory = IntPtr.Zero;
            try
            {
                hr = WinRtNative.RoGetActivationFactory(hClassName, WinRtNative.IID_ISessionManagerStatics, out factory);
                if (hr != 0 || factory == IntPtr.Zero)
                    return IntPtr.Zero;

                IntPtr asyncOp = IntPtr.Zero;
                hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)WinRtNative.VTable(factory)[6])(factory, &asyncOp);
                return hr == 0 ? asyncOp : IntPtr.Zero;
            }
            finally
            {
                if (factory != IntPtr.Zero)
                    Marshal.Release(factory);
                WinRtNative.WindowsDeleteString(hClassName);
            }
        }

        public event EventHandler? CurrentSessionChanged
        {
            add
            {
                if (_disposed != 0 || _ptr == IntPtr.Zero || value == null) return;
                bool subscribe = _currentSessionChanged == null;
                _currentSessionChanged += value;
                if (subscribe)
                    SubscribeCurrentSessionChanged();
            }
            remove
            {
                if (value == null) return;
                _currentSessionChanged -= value;
                if (_currentSessionChanged == null && _currentSessionToken != 0 && _ptr != IntPtr.Zero)
                    UnsubscribeCurrentSessionChanged();
            }
        }

        private unsafe void SubscribeCurrentSessionChanged()
        {
            _currentSessionCallback = new WinRtCallback(
                WinRtNative.IID_TypedEventHandler_CurrentSessionChanged,
                (_, _) => _currentSessionChanged?.Invoke(this, EventArgs.Empty));
            IntPtr handlerPtr = WinRtCallbackWrappers.Instance.GetOrCreateComInterfaceForObject(_currentSessionCallback, CreateComInterfaceFlags.None);
            try
            {
                long token = 0;
                if (((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, long*, int>)WinRtNative.VTable(_ptr)[8])(_ptr, handlerPtr, &token) == 0)
                    _currentSessionToken = token;
            }
            finally
            {
                Marshal.Release(handlerPtr);
            }
        }

        private unsafe void UnsubscribeCurrentSessionChanged()
        {
            long token = Interlocked.Exchange(ref _currentSessionToken, 0);
            if (token != 0)
                ((delegate* unmanaged[Stdcall]<IntPtr, long, int>)WinRtNative.VTable(_ptr)[9])(_ptr, token);
            _currentSessionCallback = null;
        }

        public event EventHandler? SessionsChanged
        {
            add
            {
                if (_disposed != 0 || _ptr == IntPtr.Zero || value == null) return;
                bool subscribe = _sessionsChanged == null;
                _sessionsChanged += value;
                if (subscribe)
                    SubscribeSessionsChanged();
            }
            remove
            {
                if (value == null) return;
                _sessionsChanged -= value;
                if (_sessionsChanged == null && _sessionsToken != 0 && _ptr != IntPtr.Zero)
                    UnsubscribeSessionsChanged();
            }
        }

        private unsafe void SubscribeSessionsChanged()
        {
            _sessionsCallback = new WinRtCallback(
                WinRtNative.IID_TypedEventHandler_SessionsChanged,
                (_, _) => _sessionsChanged?.Invoke(this, EventArgs.Empty));
            IntPtr handlerPtr = WinRtCallbackWrappers.Instance.GetOrCreateComInterfaceForObject(_sessionsCallback, CreateComInterfaceFlags.None);
            try
            {
                long token = 0;
                if (((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, long*, int>)WinRtNative.VTable(_ptr)[10])(_ptr, handlerPtr, &token) == 0)
                    _sessionsToken = token;
            }
            finally
            {
                Marshal.Release(handlerPtr);
            }
        }

        private unsafe void UnsubscribeSessionsChanged()
        {
            long token = Interlocked.Exchange(ref _sessionsToken, 0);
            if (token != 0)
                ((delegate* unmanaged[Stdcall]<IntPtr, long, int>)WinRtNative.VTable(_ptr)[11])(_ptr, token);
            _sessionsCallback = null;
        }

        public unsafe SmtcSession? GetCurrentSession()
        {
            if (_disposed != 0 || _ptr == IntPtr.Zero)
                return null;

            IntPtr sessionPtr = IntPtr.Zero;
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)WinRtNative.VTable(_ptr)[6])(_ptr, &sessionPtr);
            return hr == 0 && sessionPtr != IntPtr.Zero ? new SmtcSession(sessionPtr) : null;
        }

        public unsafe List<SmtcSession> GetSessions()
        {
            var result = new List<SmtcSession>();
            if (_disposed != 0 || _ptr == IntPtr.Zero)
                return result;

            IntPtr vectorView = IntPtr.Zero;
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)WinRtNative.VTable(_ptr)[7])(_ptr, &vectorView);
            if (hr != 0 || vectorView == IntPtr.Zero)
                return result;

            try
            {
                uint size = 0;
                if (((delegate* unmanaged[Stdcall]<IntPtr, uint*, int>)WinRtNative.VTable(vectorView)[7])(vectorView, &size) != 0)
                    return result;

                for (uint i = 0; i < size; i++)
                {
                    IntPtr itemPtr = IntPtr.Zero;
                    if (((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)WinRtNative.VTable(vectorView)[6])(vectorView, i, &itemPtr) == 0 &&
                        itemPtr != IntPtr.Zero)
                    {
                        result.Add(new SmtcSession(itemPtr));
                    }
                }
            }
            finally
            {
                Marshal.Release(vectorView);
            }

            return result;
        }

        public unsafe void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            IntPtr ptr = Interlocked.Exchange(ref _ptr, IntPtr.Zero);
            if (ptr != IntPtr.Zero)
            {
                long t1 = Interlocked.Exchange(ref _currentSessionToken, 0);
                if (t1 != 0)
                    ((delegate* unmanaged[Stdcall]<IntPtr, long, int>)WinRtNative.VTable(ptr)[9])(ptr, t1);
                long t2 = Interlocked.Exchange(ref _sessionsToken, 0);
                if (t2 != 0)
                    ((delegate* unmanaged[Stdcall]<IntPtr, long, int>)WinRtNative.VTable(ptr)[11])(ptr, t2);

                _currentSessionChanged = null;
                _sessionsChanged = null;
                _currentSessionCallback = null;
                _sessionsCallback = null;
                Marshal.Release(ptr);
            }
        }
    }

    internal sealed class SmtcSession : IDisposable
    {
        private IntPtr _ptr;
        private int _disposed;
        private long _timelineToken;
        private long _playbackToken;
        private long _mediaPropsToken;
        private EventHandler? _timelineChanged;
        private EventHandler? _playbackInfoChanged;
        private EventHandler? _mediaPropertiesChanged;
        private WinRtCallback? _timelineCallback;
        private WinRtCallback? _playbackCallback;
        private WinRtCallback? _mediaPropsCallback;

        internal SmtcSession(IntPtr ptr)
        {
            _ptr = ptr;
            SourceAppUserModelId = ReadSourceAppUserModelId(ptr);
        }

        public string SourceAppUserModelId { get; }

        private static unsafe string ReadSourceAppUserModelId(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return string.Empty;
            IntPtr hstr = IntPtr.Zero;
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)WinRtNative.VTable(ptr)[6])(ptr, &hstr);
            return hr == 0 ? WinRtNative.HStringToString(hstr) : string.Empty;
        }

        public event EventHandler? TimelinePropertiesChanged
        {
            add
            {
                if (_disposed != 0 || _ptr == IntPtr.Zero || value == null) return;
                bool subscribe = _timelineChanged == null;
                _timelineChanged += value;
                if (subscribe)
                    SubscribeTimelineChanged();
            }
            remove
            {
                if (value == null) return;
                _timelineChanged -= value;
                if (_timelineChanged == null && _timelineToken != 0 && _ptr != IntPtr.Zero)
                    UnsubscribeTimelineChanged();
            }
        }

        private unsafe void SubscribeTimelineChanged()
        {
            _timelineCallback = new WinRtCallback(
                WinRtNative.IID_TypedEventHandler_TimelinePropertiesChanged,
                (_, _) => _timelineChanged?.Invoke(this, EventArgs.Empty));
            IntPtr handlerPtr = WinRtCallbackWrappers.Instance.GetOrCreateComInterfaceForObject(_timelineCallback, CreateComInterfaceFlags.None);
            try
            {
                long token = 0;
                if (((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, long*, int>)WinRtNative.VTable(_ptr)[25])(_ptr, handlerPtr, &token) == 0)
                    _timelineToken = token;
            }
            finally
            {
                Marshal.Release(handlerPtr);
            }
        }

        private unsafe void UnsubscribeTimelineChanged()
        {
            long token = Interlocked.Exchange(ref _timelineToken, 0);
            if (token != 0)
                ((delegate* unmanaged[Stdcall]<IntPtr, long, int>)WinRtNative.VTable(_ptr)[26])(_ptr, token);
            _timelineCallback = null;
        }

        public event EventHandler? PlaybackInfoChanged
        {
            add
            {
                if (_disposed != 0 || _ptr == IntPtr.Zero || value == null) return;
                bool subscribe = _playbackInfoChanged == null;
                _playbackInfoChanged += value;
                if (subscribe)
                    SubscribePlaybackChanged();
            }
            remove
            {
                if (value == null) return;
                _playbackInfoChanged -= value;
                if (_playbackInfoChanged == null && _playbackToken != 0 && _ptr != IntPtr.Zero)
                    UnsubscribePlaybackChanged();
            }
        }

        private unsafe void SubscribePlaybackChanged()
        {
            _playbackCallback = new WinRtCallback(
                WinRtNative.IID_TypedEventHandler_PlaybackInfoChanged,
                (_, _) => _playbackInfoChanged?.Invoke(this, EventArgs.Empty));
            IntPtr handlerPtr = WinRtCallbackWrappers.Instance.GetOrCreateComInterfaceForObject(_playbackCallback, CreateComInterfaceFlags.None);
            try
            {
                long token = 0;
                if (((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, long*, int>)WinRtNative.VTable(_ptr)[27])(_ptr, handlerPtr, &token) == 0)
                    _playbackToken = token;
            }
            finally
            {
                Marshal.Release(handlerPtr);
            }
        }

        private unsafe void UnsubscribePlaybackChanged()
        {
            long token = Interlocked.Exchange(ref _playbackToken, 0);
            if (token != 0)
                ((delegate* unmanaged[Stdcall]<IntPtr, long, int>)WinRtNative.VTable(_ptr)[28])(_ptr, token);
            _playbackCallback = null;
        }

        public event EventHandler? MediaPropertiesChanged
        {
            add
            {
                if (_disposed != 0 || _ptr == IntPtr.Zero || value == null) return;
                bool subscribe = _mediaPropertiesChanged == null;
                _mediaPropertiesChanged += value;
                if (subscribe)
                    SubscribeMediaPropsChanged();
            }
            remove
            {
                if (value == null) return;
                _mediaPropertiesChanged -= value;
                if (_mediaPropertiesChanged == null && _mediaPropsToken != 0 && _ptr != IntPtr.Zero)
                    UnsubscribeMediaPropsChanged();
            }
        }

        private unsafe void SubscribeMediaPropsChanged()
        {
            _mediaPropsCallback = new WinRtCallback(
                WinRtNative.IID_TypedEventHandler_MediaPropertiesChanged,
                (_, _) => _mediaPropertiesChanged?.Invoke(this, EventArgs.Empty));
            IntPtr handlerPtr = WinRtCallbackWrappers.Instance.GetOrCreateComInterfaceForObject(_mediaPropsCallback, CreateComInterfaceFlags.None);
            try
            {
                long token = 0;
                if (((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, long*, int>)WinRtNative.VTable(_ptr)[29])(_ptr, handlerPtr, &token) == 0)
                    _mediaPropsToken = token;
            }
            finally
            {
                Marshal.Release(handlerPtr);
            }
        }

        private unsafe void UnsubscribeMediaPropsChanged()
        {
            long token = Interlocked.Exchange(ref _mediaPropsToken, 0);
            if (token != 0)
                ((delegate* unmanaged[Stdcall]<IntPtr, long, int>)WinRtNative.VTable(_ptr)[30])(_ptr, token);
            _mediaPropsCallback = null;
        }

        public unsafe SmtcPlaybackStatus GetPlaybackStatus()
        {
            if (_disposed != 0 || _ptr == IntPtr.Zero)
                return SmtcPlaybackStatus.Closed;

            IntPtr infoPtr = IntPtr.Zero;
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)WinRtNative.VTable(_ptr)[9])(_ptr, &infoPtr);
            if (hr != 0 || infoPtr == IntPtr.Zero)
                return SmtcPlaybackStatus.Closed;

            try
            {
                int status = 0;
                if (((delegate* unmanaged[Stdcall]<IntPtr, int*, int>)WinRtNative.VTable(infoPtr)[7])(infoPtr, &status) == 0 &&
                    Enum.IsDefined(typeof(SmtcPlaybackStatus), status))
                {
                    return (SmtcPlaybackStatus)status;
                }
                return SmtcPlaybackStatus.Closed;
            }
            finally
            {
                Marshal.Release(infoPtr);
            }
        }

        public unsafe SmtcTimelineProperties? GetTimelineProperties()
        {
            if (_disposed != 0 || _ptr == IntPtr.Zero)
                return null;

            IntPtr timelinePtr = IntPtr.Zero;
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)WinRtNative.VTable(_ptr)[8])(_ptr, &timelinePtr);
            if (hr != 0 || timelinePtr == IntPtr.Zero)
                return null;

            try
            {
                long endTicks = 0;
                long posTicks = 0;
                long lastUpdatedFileTime = 0;

                ((delegate* unmanaged[Stdcall]<IntPtr, long*, int>)WinRtNative.VTable(timelinePtr)[7])(timelinePtr, &endTicks);
                ((delegate* unmanaged[Stdcall]<IntPtr, long*, int>)WinRtNative.VTable(timelinePtr)[10])(timelinePtr, &posTicks);
                ((delegate* unmanaged[Stdcall]<IntPtr, long*, int>)WinRtNative.VTable(timelinePtr)[11])(timelinePtr, &lastUpdatedFileTime);

                DateTimeOffset lastUpdated = lastUpdatedFileTime > 0
                    ? DateTimeOffset.FromFileTime(lastUpdatedFileTime)
                    : DateTimeOffset.MinValue;

                return new SmtcTimelineProperties(
                    TimeSpan.FromTicks(posTicks),
                    TimeSpan.FromTicks(endTicks),
                    lastUpdated);
            }
            finally
            {
                Marshal.Release(timelinePtr);
            }
        }

        public Task<SmtcMediaProperties?> TryGetMediaPropertiesAsync()
        {
            return TryGetMediaPropertiesCoreAsync();
        }

        private async Task<SmtcMediaProperties?> TryGetMediaPropertiesCoreAsync()
        {
            IntPtr asyncOp = BeginGetMediaProperties();
            if (asyncOp == IntPtr.Zero)
                return null;

            IntPtr propsPtr = await WinRtNative.AwaitPtrAsync(asyncOp, WinRtNative.IID_IAsyncOpCompleted_MediaProps).ConfigureAwait(false);
            if (propsPtr == IntPtr.Zero)
                return null;

            try
            {
                var (title, artist, openThumbOp) = ReadMediaPropertiesAndBeginThumbnail(propsPtr);
                byte[]? thumbBytes = null;
                if (openThumbOp != IntPtr.Zero)
                {
                    IntPtr streamPtr = await WinRtNative.AwaitPtrAsync(openThumbOp, WinRtNative.IID_IAsyncOpCompleted_RandomAccessStream).ConfigureAwait(false);
                    if (streamPtr != IntPtr.Zero)
                    {
                        try
                        {
                            thumbBytes = WinRtNative.ReadRandomAccessStreamBytes(streamPtr);
                        }
                        finally
                        {
                            WinRtNative.TryCloseAndRelease(streamPtr);
                        }
                    }
                }

                return new SmtcMediaProperties(title, artist, thumbBytes);
            }
            finally
            {
                Marshal.Release(propsPtr);
            }
        }

        private unsafe IntPtr BeginGetMediaProperties()
        {
            if (_disposed != 0 || _ptr == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr asyncOp = IntPtr.Zero;
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)WinRtNative.VTable(_ptr)[7])(_ptr, &asyncOp);
            return hr == 0 ? asyncOp : IntPtr.Zero;
        }

        private static unsafe (string Title, string Artist, IntPtr OpenThumbOp) ReadMediaPropertiesAndBeginThumbnail(IntPtr propsPtr)
        {
            IntPtr hTitle = IntPtr.Zero;
            string title = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)WinRtNative.VTable(propsPtr)[6])(propsPtr, &hTitle) == 0
                ? WinRtNative.HStringToString(hTitle)
                : string.Empty;

            IntPtr hArtist = IntPtr.Zero;
            string artist = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)WinRtNative.VTable(propsPtr)[9])(propsPtr, &hArtist) == 0
                ? WinRtNative.HStringToString(hArtist)
                : string.Empty;

            IntPtr openOp = IntPtr.Zero;
            IntPtr thumbRef = IntPtr.Zero;
            try
            {
                if (((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)WinRtNative.VTable(propsPtr)[15])(propsPtr, &thumbRef) == 0 &&
                    thumbRef != IntPtr.Zero)
                {
                    if (((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)WinRtNative.VTable(thumbRef)[6])(thumbRef, &openOp) != 0)
                        openOp = IntPtr.Zero;
                }
            }
            finally
            {
                if (thumbRef != IntPtr.Zero)
                    Marshal.Release(thumbRef);
            }

            return (title, artist, openOp);
        }

        public Task<bool> TrySkipNextAsync() => InvokeBoolOp(16);
        public Task<bool> TrySkipPreviousAsync() => InvokeBoolOp(17);
        public Task<bool> TryTogglePlayPauseAsync() => InvokeBoolOp(20);

        public unsafe Task<bool> TryChangePlaybackPositionAsync(long requestedPlaybackPositionTicks)
        {
            if (_disposed != 0 || _ptr == IntPtr.Zero)
                return Task.FromResult(false);

            IntPtr asyncOp = IntPtr.Zero;
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, long, IntPtr*, int>)WinRtNative.VTable(_ptr)[24])(_ptr, requestedPlaybackPositionTicks, &asyncOp);
            return hr == 0 && asyncOp != IntPtr.Zero
                ? WinRtNative.AwaitBoolAsync(asyncOp)
                : Task.FromResult(false);
        }

        private unsafe Task<bool> InvokeBoolOp(int slot)
        {
            if (_disposed != 0 || _ptr == IntPtr.Zero)
                return Task.FromResult(false);

            IntPtr asyncOp = IntPtr.Zero;
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)WinRtNative.VTable(_ptr)[slot])(_ptr, &asyncOp);
            return hr == 0 && asyncOp != IntPtr.Zero
                ? WinRtNative.AwaitBoolAsync(asyncOp)
                : Task.FromResult(false);
        }

        public unsafe void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            IntPtr ptr = Interlocked.Exchange(ref _ptr, IntPtr.Zero);
            if (ptr != IntPtr.Zero)
            {
                long t1 = Interlocked.Exchange(ref _timelineToken, 0);
                if (t1 != 0)
                    ((delegate* unmanaged[Stdcall]<IntPtr, long, int>)WinRtNative.VTable(ptr)[26])(ptr, t1);
                long t2 = Interlocked.Exchange(ref _playbackToken, 0);
                if (t2 != 0)
                    ((delegate* unmanaged[Stdcall]<IntPtr, long, int>)WinRtNative.VTable(ptr)[28])(ptr, t2);
                long t3 = Interlocked.Exchange(ref _mediaPropsToken, 0);
                if (t3 != 0)
                    ((delegate* unmanaged[Stdcall]<IntPtr, long, int>)WinRtNative.VTable(ptr)[30])(ptr, t3);

                _timelineChanged = null;
                _playbackInfoChanged = null;
                _mediaPropertiesChanged = null;
                _timelineCallback = null;
                _playbackCallback = null;
                _mediaPropsCallback = null;
                Marshal.Release(ptr);
            }
        }
    }
}
