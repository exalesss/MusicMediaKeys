using Xunit;

namespace MusicMediaKeys.Tests
{
    public class MediaCommandDispatchServiceTests
    {
        [Fact]
        public void BuildStatusText_ContainsCommandAndResult()
        {
            string text = MediaCommandDispatchService.BuildStatusText(MediaCommand.PlayPause, true, "ok");

            Assert.Contains("PlayPause", text);
            Assert.Contains("OK", text);
            Assert.Contains("ok", text);
        }
    }
}
