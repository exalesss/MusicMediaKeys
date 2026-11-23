using System;
using Xunit;

namespace MusicMediaKeys.Tests
{
    public class AudioVolumeSafetyTests
    {
        [Fact]
        public void MultipleGetVolumeCalls_DoNotAlterSystemVolume()
        {
            var volumeService = new MediaVolumeService();
            float initialVol = volumeService.GetVolume(true, null);

            for (int i = 0; i < 10; i++)
            {
                float currentVol = volumeService.GetVolume(true, null);
                Assert.Equal(initialVol, currentVol);
            }

            float finalVol = volumeService.GetVolume(true, null);
            Assert.Equal(initialVol, finalVol);
        }

        [Fact]
        public void EndpointVolume_VTableMethods_ReturnSuccess()
        {
            bool ok = MediaVolumeService.DefaultDeviceVolume.TryGetEndpointVolume(out var vol);
            Assert.True(ok);
            Assert.NotNull(vol);

            int hrScalar = vol!.GetMasterVolumeLevelScalar(out float level);
            Assert.Equal(0, hrScalar);
            Assert.True(level >= 0f && level <= 1f);

            int hrMute = vol.GetMute(out bool mute);
            Assert.Equal(0, hrMute);

            int hrStep = vol.GetVolumeStepInfo(out uint step, out uint stepCount);
            Assert.Equal(0, hrStep);
            Assert.True(stepCount > 0);
        }

        [Fact]
        public void TryAdjustTargetProcesses_WhenProcessesNotFound_ReturnsFalseAndPreservesVolume()
        {
            var volumeService = new MediaVolumeService();
            float volBefore = volumeService.GetVolume(true, null);

            bool adjusted = volumeService.TryAdjustTargetProcesses(MediaCommand.VolumeUp, new[] { 999999 }, 5);
            Assert.False(adjusted);

            float volAfter = volumeService.GetVolume(true, null);
            Assert.Equal(volBefore, volAfter);
        }
    }
}
