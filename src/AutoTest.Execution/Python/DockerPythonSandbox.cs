using System.Diagnostics;
using System.Text;
using AutoTest.Core.Execution;
using AutoTest.Core.Target.Python;
using Microsoft.Extensions.Logging;

namespace AutoTest.Execution.Python;

/// <summary>
/// Docker 容器沙箱执行 Python 脚本：网络隔离、内存/CPU 限制、只读文件系统。
/// </summary>
internal class DockerPythonSandbox
{
    private readonly PythonSandboxOptions _options;
    private readonly ILogger<DockerPythonSandbox> _logger;

    public DockerPythonSandbox(PythonSandboxOptions options, ILogger<DockerPythonSandbox> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsAvailable => _options.Mode.Equals("docker", StringComparison.OrdinalIgnoreCase);

    public async Task<SandboxResult> RunAsync(PythonTarget target, string scriptPath, int timeoutSeconds, CancellationToken ct)
    {
        var containerName = $"autotest-py-{Guid.NewGuid():N}";
        var scriptDir = Path.GetDirectoryName(scriptPath)!;
        var scriptFile = Path.GetFileName(scriptPath);
        var workDir = target.WorkingDirectory ?? scriptDir;
        var totalTimeout = timeoutSeconds + _options.DockerTimeoutBufferSeconds;

        var args = new List<string>
        {
            "run",
            "--rm",
            $"--name={containerName}",
            $"--memory={_options.MemoryLimit}",
            $"--cpus={_options.CpuLimit}",
            $"-v={scriptDir}:/script:ro",
            $"-w=/work",
        };

        if (_options.NoNetwork)
            args.Add("--network=none");

        if (_options.ReadOnlyRoot)
        {
            args.Add("--read-only");
            args.Add("--tmpfs=/tmp:rw,noexec,nosuid,size=64m");
            args.Add("--tmpfs=/work:rw,noexec,nosuid,size=16m");
        }

        // 环境变量
        if (target.Env != null)
        {
            foreach (var kv in target.Env)
                args.Add($"-e={kv.Key}={kv.Value}");
        }

        args.Add(_options.DockerImage);
        args.Add("python");
        args.Add($"/script/{scriptFile}");

        // 用户脚本参数
        foreach (var arg in target.Args)
            args.Add(arg);

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var a in args)
            startInfo.ArgumentList.Add(a);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var sw = Stopwatch.StartNew();

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        var tcs = new TaskCompletionSource<int>();
        process.Exited += (s, e) => tcs.TrySetResult(process.ExitCode);

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(totalTimeout));
            var timeoutTask = Task.Delay(Timeout.Infinite, timeoutCts.Token);

            var completed = await Task.WhenAny(tcs.Task, timeoutTask);
            if (completed == timeoutTask)
            {
                KillContainer(containerName);
                return new SandboxResult
                {
                    ExitCode = -1,
                    StdOut = stdout.ToString(),
                    StdErr = stderr.ToString(),
                    ElapsedMs = sw.ElapsedMilliseconds,
                    TimedOut = true,
                    ErrorMessage = $"脚本在 Docker 沙箱中超时 ({timeoutSeconds}s + {_options.DockerTimeoutBufferSeconds}s buffer)"
                };
            }

            await tcs.Task; // 确保退出
            sw.Stop();

            return new SandboxResult
            {
                ExitCode = process.ExitCode,
                StdOut = stdout.ToString(),
                StdErr = stderr.ToString(),
                ElapsedMs = sw.ElapsedMilliseconds,
                TimedOut = false,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            KillContainer(containerName);
            throw;
        }
        finally
        {
            // 确保容器被清理
            try { KillContainer(containerName); } catch { }
        }
    }

    private void KillContainer(string name)
    {
        try
        {
            using var kill = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"rm -f {name}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            kill.Start();
            kill.WaitForExit(3000);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill Docker container {Name}", name);
        }
    }
}

internal class SandboxResult
{
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = "";
    public string StdErr { get; init; } = "";
    public long ElapsedMs { get; init; }
    public bool TimedOut { get; init; }
    public string? ErrorMessage { get; init; }
}
