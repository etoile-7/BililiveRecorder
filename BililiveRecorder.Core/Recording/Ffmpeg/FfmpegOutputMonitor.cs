using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BililiveRecorder.Core.Recording.Ffmpeg
{
    internal sealed class FfmpegOutputMonitor
    {
        private readonly string segmentsDirectory;
        private readonly TimeSpan interval;
        private readonly Dictionary<string, long> lastSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public FfmpegOutputMonitor(string segmentsDirectory, TimeSpan interval)
        {
            this.segmentsDirectory = segmentsDirectory ?? throw new ArgumentNullException(nameof(segmentsDirectory));
            this.interval = interval;
        }

        public event EventHandler<string>? SegmentCompleted;

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                this.Scan();
                try
                {
                    await Task.Delay(this.interval, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            this.Scan(finalScan: true);
        }

        private void Scan(bool finalScan = false)
        {
            if (!Directory.Exists(this.segmentsDirectory))
                return;

            foreach (var path in Directory.GetFiles(this.segmentsDirectory, "*.m4s").OrderBy(static x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (this.completed.Contains(path))
                    continue;

                var info = new FileInfo(path);
                if (info.Length <= 0)
                    continue;

                if (finalScan || (this.lastSizes.TryGetValue(path, out var lastSize) && lastSize == info.Length))
                {
                    this.completed.Add(path);
                    this.SegmentCompleted?.Invoke(this, path);
                }
                else
                {
                    this.lastSizes[path] = info.Length;
                }
            }
        }
    }
}
