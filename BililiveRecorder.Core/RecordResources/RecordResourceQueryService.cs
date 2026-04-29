using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BililiveRecorder.Core.Templating;

namespace BililiveRecorder.Core.Artifacts
{
    public sealed class RecordResourceQueryService : IRecordResourceQueryService
    {
        private readonly IRecorder recorder;

        public RecordResourceQueryService(IRecorder recorder)
        {
            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        }

        public IReadOnlyList<RecordSessionDescriptor> ListSessions()
        {
            return this.EnumerateSessionFiles()
                .Select(static x => RecordArtifactJson.Read<RecordSessionDescriptor>(x))
                .Where(static x => x is not null)
                .Select(static x => x!)
                .OrderByDescending(static x => x.StartedAt)
                .ToArray();
        }

        public RecordSessionDescriptor? GetSession(Guid sessionId)
        {
            var sessionPath = this.FindSessionPath(sessionId);
            return sessionPath is null ? null : RecordArtifactJson.Read<RecordSessionDescriptor>(sessionPath);
        }

        public RecordIndexDescriptor? GetIndex(Guid sessionId)
        {
            var sessionPath = this.FindSessionPath(sessionId);
            if (sessionPath is null)
                return null;

            return RecordArtifactJson.Read<RecordIndexDescriptor>(Path.Combine(Path.GetDirectoryName(sessionPath)!, "index.json"));
        }

        public IReadOnlyList<RecordSegmentDescriptor> ListSegments(Guid sessionId)
        {
            var index = this.GetIndex(sessionId);
            return index is null ? Array.Empty<RecordSegmentDescriptor>() : index.Segments;
        }

        public RecordSegmentDescriptor? GetSegment(Guid sessionId, int sequence)
        {
            return this.GetIndex(sessionId)?.Segments.FirstOrDefault(x => x.Sequence == sequence);
        }

        public RecordPlaybackManifest? GetPlaybackManifest(Guid sessionId)
        {
            var session = this.GetSession(sessionId);
            var index = this.GetIndex(sessionId);
            if (session is null || index is null)
                return null;

            return new RecordPlaybackManifest
            {
                SessionId = session.SessionId,
                RecordMode = session.RecordMode,
                Container = session.Container,
                InitArtifact = index.InitSegment,
                Segments = index.Segments,
            };
        }

        public RecordArtifactFile? GetSessionArtifact(Guid sessionId, string role)
        {
            var sessionPath = this.FindSessionPath(sessionId);
            if (sessionPath is null)
                return null;

            var sessionDirectory = Path.GetDirectoryName(sessionPath)!;
            var fileName = role.ToLowerInvariant() switch
            {
                "session" => "session.json",
                "index" or "manifest" => "index.json",
                "playback-manifest" => "index.json",
                "danmaku" => "danmaku.jsonl",
                "init" => this.GetSession(sessionId)?.InitSegmentPath,
                _ => null,
            };

            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            return this.BuildArtifactFile(role, sessionDirectory, fileName!);
        }

        public RecordArtifactFile? GetSegmentArtifact(Guid sessionId, int sequence, string role)
        {
            var sessionPath = this.FindSessionPath(sessionId);
            var segment = this.GetSegment(sessionId, sequence);
            if (sessionPath is null || segment is null)
                return null;

            var artifact = segment.Artifacts.FirstOrDefault(x => string.Equals(x.Role, role, StringComparison.OrdinalIgnoreCase));
            if (artifact is null && string.Equals(role, "media", StringComparison.OrdinalIgnoreCase))
                artifact = new RecordArtifactDescriptor { Role = "media", Path = segment.Path, Container = "fmp4", Size = segment.Size };

            if (artifact is null)
                return null;

            return this.BuildArtifactFile(role, Path.GetDirectoryName(sessionPath)!, artifact.Path);
        }

        private IEnumerable<string> EnumerateSessionFiles()
        {
            var workDirectory = this.recorder.Config.Global.WorkDirectory;
            if (string.IsNullOrWhiteSpace(workDirectory) || !Directory.Exists(workDirectory))
                return Array.Empty<string>();

            return Directory.EnumerateFiles(workDirectory, "session.json", SearchOption.AllDirectories);
        }

        private string? FindSessionPath(Guid sessionId)
        {
            foreach (var path in this.EnumerateSessionFiles())
            {
                var session = RecordArtifactJson.Read<RecordSessionDescriptor>(path);
                if (session?.SessionId == sessionId)
                    return path;
            }

            return null;
        }

        private RecordArtifactFile? BuildArtifactFile(string role, string sessionDirectory, string relativePath)
        {
            var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(sessionDirectory, normalizedRelativePath));
            var workDirectory = this.recorder.Config.Global.WorkDirectory;
            if (string.IsNullOrWhiteSpace(workDirectory))
                return null;

            var rootDirectory = workDirectory!;
            if (!FileNameGenerator.CheckIsWithinPath(rootDirectory, fullPath) || !File.Exists(fullPath))
                return null;

            return new RecordArtifactFile
            {
                Role = role,
                RelativePath = normalizedRelativePath,
                FullPath = fullPath,
                ContentType = GetContentType(fullPath),
            };
        }

        private static string GetContentType(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".json" => "application/json; charset=utf-8",
                ".jsonl" => "application/x-ndjson; charset=utf-8",
                ".mp4" => "video/mp4",
                ".m4s" => "video/iso.segment",
                ".mpd" => "application/dash+xml",
                _ => "application/octet-stream",
            };
        }
    }
}
