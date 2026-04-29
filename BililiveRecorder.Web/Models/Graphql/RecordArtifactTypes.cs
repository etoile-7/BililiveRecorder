using System;
using BililiveRecorder.Core.Artifacts;
using GraphQL.Types;

namespace BililiveRecorder.Web.Models.Graphql
{
    internal class RecordSessionStatusEnum : EnumerationGraphType<RecordSessionStatus>
    {
    }

    internal class RecordArtifactDescriptorType : ObjectGraphType<RecordArtifactDescriptor>
    {
        public RecordArtifactDescriptorType()
        {
            this.Field(x => x.Role);
            this.Field(x => x.Path);
            this.Field(x => x.Container);
            this.Field(x => x.Size);
        }
    }

    internal class RecordSegmentDescriptorType : ObjectGraphType<RecordSegmentDescriptor>
    {
        public RecordSegmentDescriptorType()
        {
            this.Field(x => x.Sequence);
            this.Field(x => x.Path);
            this.Field<DateTimeGraphType>("openedAt", resolve: context => context.Source.OpenedAt.UtcDateTime);
            this.Field<DateTimeGraphType>("closedAt", resolve: context => context.Source.ClosedAt.UtcDateTime);
            this.Field(x => x.DurationMs);
            this.Field(x => x.Size);
            this.Field(x => x.StreamTimestampStartMs);
            this.Field(x => x.StreamTimestampEndMs);
            this.Field(x => x.Status);
            this.Field<ListGraphType<RecordArtifactDescriptorType>>("artifacts", resolve: context => context.Source.Artifacts);
        }
    }

    internal class RecordSessionDescriptorType : ObjectGraphType<RecordSessionDescriptor>
    {
        public RecordSessionDescriptorType()
        {
            this.Field(x => x.SessionId, type: typeof(IdGraphType));
            this.Field(x => x.RoomId);
            this.Field(x => x.ShortId);
            this.Field(x => x.Uid);
            this.Field(x => x.StreamerName);
            this.Field(x => x.Title);
            this.Field(x => x.AreaParent);
            this.Field(x => x.AreaChild);
            this.Field(x => x.RecordMode);
            this.Field(x => x.Container);
            this.Field(x => x.Status, type: typeof(RecordSessionStatusEnum));
            this.Field<DateTimeGraphType>("startedAt", resolve: context => context.Source.StartedAt.UtcDateTime);
            this.Field<DateTimeGraphType>("endedAt", resolve: context => context.Source.EndedAt?.UtcDateTime);
            this.Field<DateTimeGraphType>("firstSegmentReadyAt", resolve: context => context.Source.FirstSegmentReadyAt?.UtcDateTime);
            this.Field(x => x.Codec);
            this.Field(x => x.Qn);
            this.Field(x => x.StreamHost, nullable: true);
            this.Field(x => x.SegmentDurationSeconds);
            this.Field(x => x.InitSegmentPath);
            this.Field(x => x.SegmentCount);
            this.Field(x => x.ErrorMessage, nullable: true);
        }
    }

    internal class RecordIndexDescriptorType : ObjectGraphType<RecordIndexDescriptor>
    {
        public RecordIndexDescriptorType()
        {
            this.Field(x => x.SessionId, type: typeof(IdGraphType));
            this.Field(x => x.Status, type: typeof(RecordSessionStatusEnum));
            this.Field<DateTimeGraphType>("updatedAt", resolve: context => context.Source.UpdatedAt.UtcDateTime);
            this.Field<RecordArtifactDescriptorType>("initSegment", resolve: context => context.Source.InitSegment);
            this.Field<ListGraphType<RecordSegmentDescriptorType>>("segments", resolve: context => context.Source.Segments);
        }
    }

    internal class RecordPlaybackManifestType : ObjectGraphType<RecordPlaybackManifest>
    {
        public RecordPlaybackManifestType()
        {
            this.Field(x => x.SessionId, type: typeof(IdGraphType));
            this.Field(x => x.RecordMode);
            this.Field(x => x.Container);
            this.Field<RecordArtifactDescriptorType>("initArtifact", resolve: context => context.Source.InitArtifact);
            this.Field<ListGraphType<RecordSegmentDescriptorType>>("segments", resolve: context => context.Source.Segments);
        }
    }
}
