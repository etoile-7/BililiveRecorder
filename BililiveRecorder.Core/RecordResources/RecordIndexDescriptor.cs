using System;
using System.Collections.Generic;

namespace BililiveRecorder.Core.Artifacts
{
    public sealed class RecordIndexDescriptor
    {
        public Guid SessionId { get; set; }
        public RecordSessionStatus Status { get; set; } = RecordSessionStatus.Recording;
        public DateTimeOffset UpdatedAt { get; set; }
        public RecordArtifactDescriptor InitSegment { get; set; } = new RecordArtifactDescriptor();
        public List<RecordSegmentDescriptor> Segments { get; set; } = new List<RecordSegmentDescriptor>();
    }
}
