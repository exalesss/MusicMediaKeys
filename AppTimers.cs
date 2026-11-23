using System;
using System.Windows.Threading;

namespace MusicMediaKeys
{
    internal sealed class AppTimers : IDisposable
    {
        private DispatcherTimer? _statusTimer;
        private DispatcherTimer? _trayIconDelayTimer;
        private DispatcherTimer? _updateTimer;

        public void StartStatusTimer(int intervalMs, EventHandler tickHandler)
        {
            _statusTimer ??= CreateTimer(intervalMs, tickHandler);
            _statusTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
            _statusTimer.Start();
        }

        public void ScheduleTrayIconCreation(int delayMs, EventHandler tickHandler)
        {
            StopAndDispose(ref _trayIconDelayTimer);
            _trayIconDelayTimer = CreateTimer(delayMs, (s, e) =>
            {
                StopAndDispose(ref _trayIconDelayTimer);
                tickHandler(s, e);
            });
            _trayIconDelayTimer.Start();
        }

        public void StartUpdateChecks(TimeSpan firstDelay, TimeSpan interval, Action check)
        {
            StopAndDispose(ref _updateTimer);
            _updateTimer = new DispatcherTimer { Interval = firstDelay };
            _updateTimer.Tick += (s, e) =>
            {
                _updateTimer.Interval = interval;
                check();
            };
            _updateTimer.Start();
        }

        public void Dispose()
        {
            StopAndDispose(ref _statusTimer);
            StopAndDispose(ref _trayIconDelayTimer);
            StopAndDispose(ref _updateTimer);
        }

        private static DispatcherTimer CreateTimer(int intervalMs, EventHandler tickHandler)
        {
            DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(intervalMs) };
            timer.Tick += tickHandler;
            return timer;
        }

        private static void StopAndDispose(ref DispatcherTimer? timer)
        {
            if (timer == null)
                return;

            timer.Stop();
            timer = null;
        }
    }
}
