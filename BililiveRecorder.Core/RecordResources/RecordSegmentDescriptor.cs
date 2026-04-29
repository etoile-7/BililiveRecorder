using System;
using System.Collections.Generic;

namespace BililiveRecorder.Core.Artifacts
{
    public sealed class RecordSegmentDescriptor
    {
        public int Sequence { get; set; }
        public string Path { get; set; } = string.Empty;
        public DateTimeOffset OpenedAt { get; set; }
        public DateTimeOffset ClosedAt { get; set; }
        public long DurationMs { get; set; }
        public long Size { get; set; }
        public long StreamTimestampStartMs { get; set; }
        public long StreamTimestampEndMs { get; set; }
        public string Status { get; set; } = "completed";
        public List<RecordArtifactDescriptor> Artifacts { get; set; } = new List<RecordArtifactDescriptor>();
    }
}
