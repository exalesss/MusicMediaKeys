using Xunit;

namespace MusicMediaKeys.Tests
{
    public class VolumeDuckingServiceTests
    {
        [Fact]
        public void Toggle_DucksAndRestoresOriginalVolume()
        {
            var service = new VolumeDuckingService();
            float currentVolume = 0.80f;

            bool isDucked = service.Toggle(0.15f, () => currentVolume, v => currentVolume = v);

            Assert.True(isDucked);
            Assert.True(service.IsDucked);
            Assert.Equal(0.15f, currentVolume, precision: 3);

            bool afterRestore = service.Toggle(0.15f, () => currentVolume, v => currentVolume = v);

            Assert.False(afterRestore);
            Assert.False(service.IsDucked);
            Assert.Equal(0.80f, currentVolume, precision: 3);
        }

        [Fact]
        public void Toggle_DoesNotIncreaseVolumeWhenAlreadyBelowTarget()
        {
            var service = new VolumeDuckingService();
            float currentVolume = 0.10f;

            service.Toggle(0.25f, () => currentVolume, v => currentVolume = v);
            Assert.Equal(0.10f, currentVolume, precision: 3);

            service.Toggle(0.25f, () => currentVolume, v => currentVolume = v);
            Assert.Equal(0.10f, currentVolume, precision: 3);
        }

        [Fact]
        public void Toggle_ClampsTargetLevelAndSupportsReset()
        {
            var service = new VolumeDuckingService();
            float currentVolume = 0.95f;

            service.Toggle(0.99f, () => currentVolume, v => currentVolume = v);
            Assert.Equal(0.90f, currentVolume, precision: 3);

            service.Reset();
            Assert.False(service.IsDucked);

            currentVolume = 0.60f;
            bool isDucked = service.Toggle(0.0f, () => currentVolume, v => currentVolume = v);
            Assert.True(isDucked);
            Assert.Equal(0.01f, currentVolume, precision: 3);
        }
    }
}
