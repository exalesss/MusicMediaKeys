using System;

namespace MusicMediaKeys
{
    internal sealed class VolumeDuckingService
    {
        private readonly object _lock = new();
        private bool _isDucked;
        private float _savedVolume;

        public bool IsDucked
        {
            get
            {
                lock (_lock)
                    return _isDucked;
            }
        }

        public bool Toggle(float targetDuckLevel, Func<float> getVolume, Action<float> setVolume)
        {
            if (getVolume == null) throw new ArgumentNullException(nameof(getVolume));
            if (setVolume == null) throw new ArgumentNullException(nameof(setVolume));

            lock (_lock)
            {
                if (!_isDucked)
                {
                    float current = Math.Clamp(getVolume(), 0f, 1f);
                    _savedVolume = current;
                    float clampedTarget = Math.Clamp(targetDuckLevel, 0.01f, 0.90f);
                    float nextVolume = current <= 0.001f ? clampedTarget : Math.Min(current, clampedTarget);
                    setVolume(nextVolume);
                    _isDucked = true;
                    return true;
                }
                else
                {
                    float restore = Math.Clamp(_savedVolume, 0f, 1f);
                    setVolume(restore);
                    _isDucked = false;
                    return false;
                }
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _isDucked = false;
                _savedVolume = 0f;
            }
        }
    }
}
