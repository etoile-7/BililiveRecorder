using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BililiveRecorder.Core.Api;
using BililiveRecorder.Core.Artifacts;
using BililiveRecorder.Core.Config;
using BililiveRecorder.Core.Event;
using BililiveRecorder.Core.Recording.Ffmpeg;
using BililiveRecorder.Core.Scripting;
using Serilog;

namespace BililiveRecorder.Core.Recording
{
    internal sealed class Fmp4RecordTask : RecordTaskBase
    {
        private readonly object artifactLock = new object();
        private readonly Stopwatch sessionStopwatch = new Stopwatch();

        private FfmpegProcessRunner? ffmpeg;
        private CancellationTokenSource? monitorCts;
        private RecordSessionDescriptor? session;
        private RecordIndexDescriptor? index;
        private string? sessionDirectory;
        private string? relativeSessionDirectory;
        private string? initFileName;
        private string? segmentsDirectoryName;
        private DateTimeOffset? firstMediaDataArrivedAt;
        private long totalInputBytes;
        private long totalOutputBytes;

        public Fmp4RecordTask(
            IRoom room,
            ILogger logger,
            IApiClient apiClient,
            UserScriptRunner userScriptRunner)
            : base(
                room: room,
                logger: logger?.ForContext<Fmp4RecordTask>().ForContext(LoggingContext.RoomId, room.RoomConfig.RoomId)!,
                apiClient: apiClient,
                userScriptRunner: userScriptRunner)
        {
        }

        protected override void StartRecordingLoop(Stream stream)
        {
            _ = Task.Run(async () => await this.RecordingLoopAsync(stream).ConfigureAwait(false));
        }

        private async Task RecordingLoopAsync(Stream stream)
        {
            var finalStatus = RecordSessionStatus.Completed;
            string? errorMessage = null;
            Task? monitorTask = null;

            try
            {
                this.ValidateCodec();
                this.InitializeSessionArtifacts();

                if (this.sessionDirectory is null)
                    throw new InvalidOperationException("fMP4 会话目录尚未初始化");

                this.ffmpeg = new FfmpegProcessRunner(this.logger);
                var args = FfmpegArgumentBuilder.BuildDashArguments(this.room.RoomConfig, this.sessionDirectory);
                this.ffmpeg.Start(this.room.RoomConfig.FfmpegExecutablePath ?? "ffmpeg", args, this.sessionDirectory);

                this.monitorCts = CancellationTokenSource.CreateLinkedTokenSource(this.ct);
                var monitor = new FfmpegOutputMonitor(Path.Combine(this.sessionDirectory, this.segmentsDirectoryName!), TimeSpan.FromMilliseconds(750));
                monitor.SegmentCompleted += this.Monitor_SegmentCompleted;
                monitorTask = Task.Run(async () => await monitor.RunAsync(this.monitorCts.Token).ConfigureAwait(false));

                this.timer.Start();
                this.sessionStopwatch.Start();
                await this.CopyStreamToFfmpegAsync(stream, this.ffmpeg.StandardInput).ConfigureAwait(false);

                var exitCode = await this.ffmpeg.CompleteInputAndWaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
                if (exitCode != 0 && !this.ct.IsCancellationRequested)
                {
                    finalStatus = RecordSessionStatus.Failed;
                    errorMessage = "FFmpeg 退出码: " + exitCode.ToString(CultureInfo.InvariantCulture);
                }

#if NET6_0_OR_GREATER
                await this.monitorCts.CancelAsync().ConfigureAwait(false);
#else
                this.monitorCts.Cancel();
#endif
                await monitorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                finalStatus = RecordSessionStatus.Stopped;
                this.logger.Debug("fMP4 录制被取消");
            }
            catch (Exception ex)
            {
                finalStatus = RecordSessionStatus.Failed;
                errorMessage = ex.Message;
                this.logger.Warning(ex, "fMP4 录制时发生错误");
            }
            finally
            {
                this.timer.Stop();
                this.sessionStopwatch.Stop();

                try
                {
                    if (this.monitorCts is not null)
                    {
#if NET6_0_OR_GREATER
                        await this.monitorCts.CancelAsync().ConfigureAwait(false);
#else
                        this.monitorCts.Cancel();
#endif
                    }
                    if (monitorTask is not null)
                        await monitorTask.ConfigureAwait(false);
                    this.monitorCts?.Dispose();
                    this.monitorCts = null;
                }
                catch (Exception) { }

                try
                {
                    if (this.ffmpeg is not null && !this.ffmpeg.HasExited)
                        _ = await this.ffmpeg.CompleteInputAndWaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this.logger.Warning(ex, "结束 FFmpeg 时发生错误");
                }

                try
                {
#if NET6_0_OR_GREATER
                    await stream.DisposeAsync().ConfigureAwait(false);
#else
                    stream.Dispose();
#endif
                }
                catch (Exception) { }

                this.FinalizeSession(finalStatus, errorMessage);
                this.ffmpeg?.Dispose();
                this.ffmpeg = null;
                this.RequestStop();
                this.OnRecordSessionEnded(EventArgs.Empty);
                this.logger.Information("fMP4 录制结束");
            }
        }

        private void ValidateCodec()
        {
            if (this.selectedCodecQn.Codec == StreamCodec.AVC)
                return;

            if (this.room.RoomConfig.Fmp4UnsupportedCodecPolicy == Fmp4UnsupportedCodecPolicy.AllowUnsupported)
            {
                this.logger.Warning("当前直播流编码为 {Codec}，配置允许继续尝试 fMP4 录制", this.selectedCodecQn.Codec);
                return;
            }

            throw new NotSupportedException("fMP4 首期只支持 AVC 视频流，当前编码为 " + this.selectedCodecQn.Codec);
        }

        private void InitializeSessionArtifacts()
        {
            var (baseFullPath, baseRelativePath) = this.CreateFileName(".mp4", appendRequiredExtension: false);
            var baseDirectory = Path.GetDirectoryName(baseFullPath)!;
            var baseName = Path.GetFileNameWithoutExtension(baseFullPath);
            var relativeDirectory = Path.GetDirectoryName(baseRelativePath);
            var relativeBaseName = Path.GetFileNameWithoutExtension(baseRelativePath);

            this.sessionDirectory = Path.Combine(baseDirectory, baseName);
            this.relativeSessionDirectory = string.IsNullOrWhiteSpace(relativeDirectory)
                ? relativeBaseName
                : Path.Combine(relativeDirectory!, relativeBaseName);

            this.initFileName = CleanFileName(this.room.RoomConfig.Fmp4InitFileName, "init.mp4");
            this.segmentsDirectoryName = CleanFileName(this.room.RoomConfig.Fmp4SegmentsDirectoryName, "segments");

            Directory.CreateDirectory(this.sessionDirectory);
            Directory.CreateDirectory(Path.Combine(this.sessionDirectory, this.segmentsDirectoryName));

            var startedAt = DateTimeOffset.UtcNow;
            this.session = new RecordSessionDescriptor
            {
                SessionId = this.SessionId,
                RoomId = this.room.RoomConfig.RoomId,
                ShortId = this.room.ShortId,
                Uid = this.room.Uid,
                StreamerName = this.room.Name,
                Title = this.room.Title,
                AreaParent = this.room.AreaNameParent,
                AreaChild = this.room.AreaNameChild,
                RecordMode = RecordMode.Fmp4.ToString(),
                Container = "fmp4",
                Status = RecordSessionStatus.Recording,
                StartedAt = startedAt,
                Codec = this.selectedCodecQn.Codec.ToString(),
                Qn = this.qn,
                StreamHost = this.streamHostFull ?? this.streamHost,
                SegmentDurationSeconds = this.room.RoomConfig.Fmp4SegmentDurationSeconds == 0 ? 6 : this.room.RoomConfig.Fmp4SegmentDurationSeconds,
                InitSegmentPath = this.ToArtifactRelativePath(this.initFileName),
            };

            this.index = new RecordIndexDescriptor
            {
                SessionId = this.SessionId,
                Status = RecordSessionStatus.Recording,
                UpdatedAt = startedAt,
                InitSegment = new RecordArtifactDescriptor
                {
                    Role = "init",
                    Path = this.session.InitSegmentPath,
                    Container = "fmp4",
                },
            };

            this.WriteArtifacts();

            var initFullPath = Path.Combine(this.sessionDirectory, this.initFileName);
            var initRelativePath = this.ToArtifactRelativePath(Path.Combine(this.relativeSessionDirectory!, this.initFileName));
            this.OnRecordFileOpening(new RecordFileOpeningEventArgs(this.room)
            {
                SessionId = this.SessionId,
                FullPath = initFullPath,
                RelativePath = initRelativePath,
                FileOpenTime = startedAt,
            });
        }

        private async Task CopyStreamToFfmpegAsync(Stream input, Stream output)
        {
            var buffer = new byte[1024 * 64];

            while (!this.ct.IsCancellationRequested)
            {
#if NET6_0_OR_GREATER
                var bytesRead = await input.ReadAsync(buffer, this.ct).ConfigureAwait(false);
#else
                var bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, this.ct).ConfigureAwait(false);
#endif
                if (bytesRead == 0)
                    break;

                if (this.firstMediaDataArrivedAt is null)
                    this.firstMediaDataArrivedAt = DateTimeOffset.UtcNow;

                Interlocked.Add(ref this.ioNetworkDownloadedBytes, bytesRead);
                Interlocked.Add(ref this.totalInputBytes, bytesRead);

                this.ioDiskStopwatch.Restart();
#if NET6_0_OR_GREATER
                await output.WriteAsync(buffer.AsMemory(0, bytesRead), this.ct).ConfigureAwait(false);
#else
                await output.WriteAsync(buffer, 0, bytesRead, this.ct).ConfigureAwait(false);
#endif
                await output.FlushAsync(this.ct).ConfigureAwait(false);
                this.ioDiskStopwatch.Stop();

                lock (this.ioDiskStatsLock)
                {
                    this.ioDiskWriteDuration += this.ioDiskStopwatch.Elapsed;
                    this.ioDiskWrittenBytes += bytesRead;
                }
                this.ioDiskStopwatch.Reset();
            }
        }

        private void Monitor_SegmentCompleted(object? sender, string segmentPath)
        {
            lock (this.artifactLock)
            {
                if (this.session is null || this.index is null || this.sessionDirectory is null)
                    return;

                var sequence = TryParseSequence(segmentPath);
                if (sequence <= 0)
                    sequence = this.index.Segments.Count + 1;

                foreach (var existing in this.index.Segments)
                    if (existing.Sequence == sequence)
                        return;

                var now = DateTimeOffset.UtcNow;
                var durationMs = (long)this.session.SegmentDurationSeconds * 1000L;
                var streamStart = (sequence - 1L) * durationMs;
                var relativePath = this.ToArtifactRelativePath(Path.Combine(this.segmentsDirectoryName!, Path.GetFileName(segmentPath)));
                var size = new FileInfo(segmentPath).Length;

                var segment = new RecordSegmentDescriptor
                {
                    Sequence = sequence,
                    Path = relativePath,
                    OpenedAt = now.AddMilliseconds(-durationMs),
                    ClosedAt = now,
                    DurationMs = durationMs,
                    Size = size,
                    StreamTimestampStartMs = streamStart,
                    StreamTimestampEndMs = streamStart + durationMs,
                    Status = "completed",
                };
                segment.Artifacts.Add(new RecordArtifactDescriptor
                {
                    Role = "media",
                    Path = relativePath,
                    Container = "fmp4",
                    Size = size,
                });

                this.index.Segments.Add(segment);
                this.index.Segments.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
                this.index.UpdatedAt = now;
                this.session.SegmentCount = this.index.Segments.Count;
                this.session.FirstSegmentReadyAt ??= now;

                this.totalOutputBytes += size;
                this.WriteArtifacts();
                this.OnRecordingStats(new RecordingStatsEventArgs
                {
                    SessionDuration = this.sessionStopwatch.Elapsed.TotalMilliseconds,
                    TotalInputBytes = Interlocked.Read(ref this.totalInputBytes),
                    TotalOutputBytes = this.totalOutputBytes,
                    CurrentFileSize = size,
                    SessionMaxTimestamp = (int)Math.Min(int.MaxValue, segment.StreamTimestampEndMs),
                    FileMaxTimestamp = (int)Math.Min(int.MaxValue, segment.DurationMs),
                    AddedDuration = segment.DurationMs,
                    PassedTime = segment.DurationMs,
                    DurationRatio = 1,
                });
            }
        }

        private void FinalizeSession(RecordSessionStatus status, string? errorMessage)
        {
            lock (this.artifactLock)
            {
                if (this.session is null || this.index is null || this.sessionDirectory is null || this.initFileName is null)
                    return;

                if (status == RecordSessionStatus.Completed && this.ct.IsCancellationRequested)
                    status = RecordSessionStatus.Stopped;

                var endedAt = DateTimeOffset.UtcNow;
                var initPath = Path.Combine(this.sessionDirectory, this.initFileName);
                var initSize = File.Exists(initPath) ? new FileInfo(initPath).Length : 0L;

                this.index.Status = status;
                this.index.UpdatedAt = endedAt;
                this.index.InitSegment.Size = initSize;

                this.session.Status = status;
                this.session.EndedAt = endedAt;
                this.session.SegmentCount = this.index.Segments.Count;
                this.session.ErrorMessage = errorMessage;
                this.WriteArtifacts();

                this.OnRecordFileClosed(new RecordFileClosedEventArgs(this.room)
                {
                    SessionId = this.SessionId,
                    FullPath = initPath,
                    RelativePath = this.ToArtifactRelativePath(Path.Combine(this.relativeSessionDirectory!, this.initFileName)),
                    FileOpenTime = this.session.StartedAt,
                    FileCloseTime = endedAt,
                    Duration = (uint)Math.Min(uint.MaxValue, this.sessionStopwatch.Elapsed.TotalMilliseconds),
                    FileSize = initSize,
                });
            }
        }

        private void WriteArtifacts()
        {
            if (this.sessionDirectory is null || this.session is null || this.index is null)
                return;

            RecordArtifactJson.WriteAtomic(Path.Combine(this.sessionDirectory, "index.json"), this.index);
            RecordArtifactJson.WriteAtomic(Path.Combine(this.sessionDirectory, "session.json"), this.session);
        }

        private string ToArtifactRelativePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static int TryParseSequence(string path)
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            return int.TryParse(fileName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence) ? sequence : -1;
        }

        private static string CleanFileName(string? value, string fallback)
        {
            return FfmpegArgumentBuilder.CleanFileName(value, fallback);
        }
    }
}
