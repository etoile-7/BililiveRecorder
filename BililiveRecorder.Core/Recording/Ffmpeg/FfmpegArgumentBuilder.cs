using System.Collections.Generic;
using System.IO;
using BililiveRecorder.Core.Config.V3;

namespace BililiveRecorder.Core.Recording.Ffmpeg
{
    internal static class FfmpegArgumentBuilder
    {
        private static readonly char[] AdditionalInvalidFileNameChars =
        {
            '<', '>', ':', '"', '/', '\\', '|', '?', '*',
        };

        public static IReadOnlyList<string> BuildDashArguments(RoomConfig config, string sessionDirectory)
        {
            var segmentDuration = config.Fmp4SegmentDurationSeconds == 0 ? 6 : config.Fmp4SegmentDurationSeconds;
            var initFileName = CleanFileName(config.Fmp4InitFileName, "init.mp4");
            var segmentsDirectoryName = CleanFileName(config.Fmp4SegmentsDirectoryName, "segments");
            var manifestPath = Path.Combine(sessionDirectory, "manifest.mpd");

            var args = new List<string>
            {
                "-hide_banner",
            };

            if (!string.IsNullOrWhiteSpace(config.FfmpegExtraArgs))
                args.AddRange(SplitArguments(config.FfmpegExtraArgs!));

            args.AddRange(new[]
            {
                "-fflags",
                "+genpts",
                "-i",
                "pipe:0",
                "-map",
                "0:v:0",
                "-map",
                "0:a?",
                "-c",
                "copy",
                "-f",
                "dash",
                "-seg_duration",
                segmentDuration.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-use_template",
                "1",
                "-use_timeline",
                "0",
                "-init_seg_name",
                initFileName,
                "-media_seg_name",
                segmentsDirectoryName.Trim('/', '\\') + "/$Number%06d$.m4s",
                manifestPath,
            });

            return args;
        }

        private static IEnumerable<string> SplitArguments(string value)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            var inQuote = false;

            foreach (var c in value)
            {
                if (c == '"')
                {
                    inQuote = !inQuote;
                    continue;
                }

                if (char.IsWhiteSpace(c) && !inQuote)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
                result.Add(current.ToString());

            return result;
        }

        internal static string CleanFileName(string? value, string fallback)
        {
            var name = string.IsNullOrWhiteSpace(value) ? fallback : value!;

            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            foreach (var c in AdditionalInvalidFileNameChars)
                name = name.Replace(c, '_');

            return name;
        }
    }
}
