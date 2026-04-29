using System;
using System.Linq;
using BililiveRecorder.Core;
using BililiveRecorder.Core.Artifacts;
using BililiveRecorder.Core.Config.V3;
using BililiveRecorder.Web.Models;
using BililiveRecorder.Web.Models.Graphql;
using GraphQL;
using GraphQL.Types;

namespace BililiveRecorder.Web.Graphql
{
    internal class RecorderQuery : ObjectGraphType
    {
        private readonly IRecorder recorder;
        private readonly IRecordResourceQueryService recordResourceQueryService;

        public RecorderQuery(IRecorder recorder, IRecordResourceQueryService recordResourceQueryService)
        {
            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            this.recordResourceQueryService = recordResourceQueryService ?? throw new ArgumentNullException(nameof(recordResourceQueryService));

            this.SetupFields();
        }

        private void SetupFields()
        {
            this.Field<RecorderVersionType>("version", resolve: context => RecorderVersion.Instance);

            this.Field<GlobalConfigType>("config", resolve: context => this.recorder.Config.Global);

            this.Field<DefaultConfigType>("defaultConfig", resolve: context => DefaultConfig.Instance);

            this.Field<ListGraphType<RoomType>>("rooms", arguments: new QueryArguments(
                    new QueryArgument<ListGraphType<IdGraphType>> { Name = "objectIds" },
                    new QueryArgument<ListGraphType<IntGraphType>> { Name = "roomIds" }
                ),
                resolve: context =>
                {
                    var objectIds = context.GetArgument<Guid[]>("objectIds");
                    var roomIds = context.GetArgument<int[]>("roomIds");

                    // If no arguments are provided, return all rooms
                    if (objectIds == null && roomIds == null)
                        return this.recorder.Rooms;

                    // Remove any "0" from the roomIds
                    roomIds = roomIds?.Where(x => x != 0).ToArray();

                    // Otherwise, filter the rooms
                    return this.recorder.Rooms.Where(x =>
                        (objectIds?.Contains(x.ObjectId) ?? false) ||
                        (roomIds?.Contains(x.RoomConfig.RoomId) ?? false) ||
                        (roomIds?.Contains(x.ShortId) ?? false)
                    );
                });

            this.Field<RoomType>("room",
                arguments: new QueryArguments(
                    new QueryArgument<IdGraphType> { Name = "objectId" },
                    new QueryArgument<IntGraphType> { Name = "roomId" }
                ),
                resolve: context =>
                {
                    var objectId = context.GetArgument<Guid>("objectId");
                    var roomId = context.GetArgument<int>("roomId");

                    IRoom? room;
                    if (objectId != default)
                        room = this.recorder.Rooms.FirstOrDefault(x => x.ObjectId == objectId);
                    else if (roomId != 0)
                        room = this.recorder.Rooms.FirstOrDefault(x => x.RoomConfig.RoomId == roomId || x.ShortId == roomId);
                    else
                        room = null;

                    return room;
                }
            );

            this.Field<ListGraphType<RecordSessionDescriptorType>>("sessions",
                arguments: new QueryArguments(
                    new QueryArgument<BooleanGraphType> { Name = "activeOnly" }
                ),
                resolve: context =>
                {
                    var sessions = this.recordResourceQueryService.ListSessions();
                    return context.GetArgument<bool>("activeOnly")
                        ? sessions.Where(x => x.Status == RecordSessionStatus.Recording)
                        : sessions;
                });

            this.Field<RecordSessionDescriptorType>("session",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "sessionId" }),
                resolve: context => this.recordResourceQueryService.GetSession(context.GetArgument<Guid>("sessionId")));

            this.Field<ListGraphType<RecordSegmentDescriptorType>>("segments",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "sessionId" }),
                resolve: context => this.recordResourceQueryService.ListSegments(context.GetArgument<Guid>("sessionId")));

            this.Field<RecordPlaybackManifestType>("playbackManifest",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "sessionId" }),
                resolve: context => this.recordResourceQueryService.GetPlaybackManifest(context.GetArgument<Guid>("sessionId")));
        }
    }
}
