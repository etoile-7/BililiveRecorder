using System;
using System.Collections.Generic;

namespace BililiveRecorder.Core.Artifacts
{
    public interface IRecordResourceQueryService
    {
        IReadOnlyList<RecordSessionDescriptor> ListSessions();
        RecordSessionDescriptor? GetSession(Guid sessionId);
        RecordIndexDescriptor? GetIndex(Guid sessionId);
        IReadOnlyList<RecordSegmentDescriptor> ListSegments(Guid sessionId);
        RecordSegmentDescriptor? GetSegment(Guid sessionId, int sequence);
        RecordPlaybackManifest? GetPlaybackManifest(Guid sessionId);
        RecordArtifactFile? GetSessionArtifact(Guid sessionId, string role);
        RecordArtifactFile? GetSegmentArtifact(Guid sessionId, int sequence, string role);
    }
}
