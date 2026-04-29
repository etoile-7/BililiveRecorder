using System;
using System.Collections.Generic;

namespace BililiveRecorder.Core.Artifacts
{
    public sealed class RecordPlaybackManifest
    {
        public Guid SessionId { get; set; }
        public string RecordMode { get; set; } = "Fmp4";
        public string Container { get; set; } = "fmp4";
        public RecordArtifactDescriptor InitArtifact { get; set; } = new RecordArtifactDescriptor();
        public List<RecordSegmentDescriptor> Segments { get; set; } = new List<RecordSegmentDescriptor>();
    }
}
