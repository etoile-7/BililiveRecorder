using System.IO;
using System.Linq;
using BililiveRecorder.Core.Config.V3;
using BililiveRecorder.Core.Recording.Ffmpeg;
using Xunit;

namespace BililiveRecorder.Core.UnitTests.Recording
{
    public class FfmpegArgumentBuilderTests
    {
        [Fact]
        public void BuildDashArgumentsCleansConfiguredOutputNames()
        {
            var config = new RoomConfig
            {
                Fmp4SegmentDurationSeconds = 4,
                Fmp4InitFileName = "in/it:bad.mp4",
                Fmp4SegmentsDirectoryName = "seg/ments:bad",
            };

            var args = FfmpegArgumentBuilder.BuildDashArguments(config, Path.Combine("D:", "recordings", "session")).ToArray();

            Assert.Contains("in_it_bad.mp4", args);
            Assert.Contains("seg_ments_bad/$Number%06d$.m4s", args);
            Assert.Contains("4", args);
        }
    }
}
