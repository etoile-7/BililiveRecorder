using System;
using BililiveRecorder.Core.Artifacts;
using BililiveRecorder.Web.Models.Rest;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BililiveRecorder.Web.Api
{
    [ApiController, Route("api/sessions", Name = "[controller] [action]")]
    public sealed class SessionsController : ControllerBase
    {
        private readonly IRecordResourceQueryService queryService;

        public SessionsController(IRecordResourceQueryService queryService)
        {
            this.queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        }

        [HttpGet]
        public ActionResult<RecordSessionDescriptor[]> GetSessions() => this.Ok(this.queryService.ListSessions());

        [HttpGet("{sessionId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RecordSessionDescriptor> GetSession(Guid sessionId)
        {
            var session = this.queryService.GetSession(sessionId);
            return session is null ? this.NotFound(new RestApiError { Message = "Session not found" }) : this.Ok(session);
        }

        [HttpGet("{sessionId:guid}/manifest")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RecordIndexDescriptor> GetManifest(Guid sessionId)
        {
            var index = this.queryService.GetIndex(sessionId);
            return index is null ? this.NotFound(new RestApiError { Message = "Session not found" }) : this.Ok(index);
        }

        [HttpGet("{sessionId:guid}/segments")]
        public ActionResult<RecordSegmentDescriptor[]> GetSegments(Guid sessionId) => this.Ok(this.queryService.ListSegments(sessionId));

        [HttpGet("{sessionId:guid}/segments/{sequence:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RecordSegmentDescriptor> GetSegment(Guid sessionId, int sequence)
        {
            var segment = this.queryService.GetSegment(sessionId, sequence);
            return segment is null ? this.NotFound(new RestApiError { Message = "Segment not found" }) : this.Ok(segment);
        }

        [HttpGet("{sessionId:guid}/playback-manifest")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RecordPlaybackManifest> GetPlaybackManifest(Guid sessionId)
        {
            var manifest = this.queryService.GetPlaybackManifest(sessionId);
            return manifest is null ? this.NotFound(new RestApiError { Message = "Session not found" }) : this.Ok(manifest);
        }

        [HttpGet("{sessionId:guid}/artifacts/{role}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public IActionResult GetSessionArtifact(Guid sessionId, string role)
        {
            var artifact = this.queryService.GetSessionArtifact(sessionId, role);
            return artifact is null ? this.NotFound(new RestApiError { Message = "Artifact not found" }) : this.PhysicalFile(artifact.FullPath, artifact.ContentType);
        }

        [HttpGet("{sessionId:guid}/segments/{sequence:int}/artifacts/{role}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public IActionResult GetSegmentArtifact(Guid sessionId, int sequence, string role)
        {
            var artifact = this.queryService.GetSegmentArtifact(sessionId, sequence, role);
            return artifact is null ? this.NotFound(new RestApiError { Message = "Artifact not found" }) : this.PhysicalFile(artifact.FullPath, artifact.ContentType);
        }
    }
}
