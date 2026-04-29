using System;

namespace BililiveRecorder.Core.Artifacts
{
    public sealed class RecordSessionDescriptor
    {
        public Guid SessionId { get; set; }
        public int RoomId { get; set; }
        public int ShortId { get; set; }
        public long Uid { get; set; }
        public string StreamerName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string AreaParent { get; set; } = string.Empty;
        public string AreaChild { get; set; } = string.Empty;
        public string RecordMode { get; set; } = "Fmp4";
        public string Container { get; set; } = "fmp4";
        public RecordSessionStatus Status { get; set; } = RecordSessionStatus.Recording;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public DateTimeOffset? FirstSegmentReadyAt { get; set; }
        public string Codec { get; set; } = string.Empty;
        public int Qn { get; set; }
        public string? StreamHost { get; set; }
        public uint SegmentDurationSeconds { get; set; }
        public string InitSegmentPath { get; set; } = string.Empty;
        public int SegmentCount { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
