using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace BililiveRecorder.Core.Recording.Ffmpeg
{
    internal sealed class FfmpegProcessRunner : IDisposable
    {
        private readonly ILogger logger;
        private Process? process;
        private Task? stderrTask;
        private bool disposedValue;

        public FfmpegProcessRunner(ILogger logger)
        {
            this.logger = logger?.ForContext<FfmpegProcessRunner>() ?? throw new ArgumentNullException(nameof(logger));
        }

        public Stream StandardInput => this.process?.StandardInput.BaseStream ?? throw new InvalidOperationException("FFmpeg 尚未启动");

        public bool HasExited => this.process?.HasExited ?? true;

        public int? ExitCode => this.process?.HasExited == true ? this.process.ExitCode : null;

        public void Start(string executablePath, IReadOnlyList<string> arguments, string workingDirectory)
        {
            if (this.process is not null)
                throw new InvalidOperationException("FFmpeg 已启动");

            Directory.CreateDirectory(workingDirectory);

            var startInfo = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(executablePath) ? "ffmpeg" : executablePath,
                Arguments = JoinArguments(arguments),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = false,
                CreateNoWindow = true,
            };

            this.process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            this.logger.Information("启动 FFmpeg: {FileName} {Arguments}", startInfo.FileName, startInfo.Arguments);
            this.process.Start();
            this.stderrTask = Task.Run(() => this.ReadStderrAsync(this.process));
        }

        public async Task<int> CompleteInputAndWaitAsync(TimeSpan gracefulTimeout, CancellationToken cancellationToken)
        {
            var currentProcess = this.process;
            if (currentProcess is null)
                return 0;

            try
            {
                currentProcess.StandardInput.Close();
            }
            catch (Exception ex)
            {
                this.logger.Debug(ex, "关闭 FFmpeg stdin 时出错");
            }

            var waitTask = Task.Run(() =>
            {
                currentProcess.WaitForExit();
                return currentProcess.ExitCode;
            });

            var delayTask = Task.Delay(gracefulTimeout, cancellationToken);
            var completed = await Task.WhenAny(waitTask, delayTask).ConfigureAwait(false);
            if (completed == waitTask)
            {
                await (this.stderrTask ?? Task.CompletedTask).ConfigureAwait(false);
                return await waitTask.ConfigureAwait(false);
            }

            this.logger.Warning("FFmpeg 未在 {Timeout} 内退出，将强制结束", gracefulTimeout);
            try
            {
                if (!currentProcess.HasExited)
                    currentProcess.Kill();
            }
            catch (Exception ex)
            {
                this.logger.Warning(ex, "强制结束 FFmpeg 时出错");
            }

            return await waitTask.ConfigureAwait(false);
        }

        private async Task ReadStderrAsync(Process target)
        {
            try
            {
                while (!target.StandardError.EndOfStream)
                {
                    var line = await target.StandardError.ReadLineAsync().ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(line))
                        this.logger.Debug("FFmpeg: {Line}", line);
                }
            }
            catch (Exception ex)
            {
                this.logger.Debug(ex, "读取 FFmpeg stderr 时出错");
            }
        }

        private static string JoinArguments(IReadOnlyList<string> arguments)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < arguments.Count; i++)
            {
                if (i > 0)
                    builder.Append(' ');
                builder.Append(Quote(arguments[i]));
            }
            return builder.ToString();
        }

        private static string Quote(string argument)
        {
            if (argument.Length == 0)
                return "\"\"";

            if (argument.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '"' }) < 0)
                return argument;

            return "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        public void Dispose()
        {
            if (this.disposedValue)
                return;

            this.disposedValue = true;
            this.process?.Dispose();
        }
    }
}
