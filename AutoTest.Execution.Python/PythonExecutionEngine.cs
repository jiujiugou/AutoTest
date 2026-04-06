using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Target.Python;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace AutoTest.Execution.Python
{
    /// <summary>
    /// Python 脚本执行引擎（跨平台支持 Windows / Linux / macOS）
    /// </summary>
    internal class PythonExecutionEngine : IExecutionEngine
    {
        private readonly ILogger<PythonExecutionEngine> _logger;

        private static readonly SemaphoreSlim _globalSemaphore = new SemaphoreSlim(5); // 默认最大并发 5

        public PythonExecutionEngine(ILogger<PythonExecutionEngine> logger)
        {
            _logger = logger;
        }

        public bool CanExecute(MonitorTarget target) => target is PythonTarget;

        public async Task<ExecutionResult> ExecuteAsync(MonitorTarget target)
        {
            if (target is not PythonTarget pyTarget)
                throw new ArgumentException("Target 必须是 PythonTarget", nameof(target));

            if (pyTarget.EnableRateLimit)
            {
                _logger.LogDebug($"等待全局并发信号量 (MaxConcurrency={pyTarget.MaxConcurrency})");
                await _globalSemaphore.WaitAsync();
                _logger.LogDebug("获得全局并发信号量");
            }
            
            try
            {
                int attempt = 0;
                while (true)
                {
                    attempt++;
                    try
                    {
                        _logger.LogInformation($"开始执行 Python 脚本 [{pyTarget.ScriptPath}], 第 {attempt} 次尝试");
                        var result = await RunProcessAsync(pyTarget);
                        _logger.LogInformation($"Python 脚本 [{pyTarget.ScriptPath}] 执行完成, ExitCode={result.ExitCode}");
                        if (!result.IsExecutionSuccess)
                            _logger.LogWarning($"Python 脚本执行失败: {result.ErrorMessage}");
                        return result;
                    }
                    catch (Exception ex) when (pyTarget.EnableRetry && attempt <= pyTarget.RetryCount)
                    {
                        _logger.LogWarning(ex, $"Python 脚本 [{pyTarget.ScriptPath}] 执行失败，第 {attempt} 次重试");
                        await Task.Delay(pyTarget.RetryDelayMs);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Python 脚本 [{pyTarget.ScriptPath}] 执行异常, 不再重试");
                        throw;
                    }
                }
            }
            finally
            {
                if (pyTarget.EnableRateLimit)
                {
                    _globalSemaphore.Release();
                    _logger.LogDebug("释放全局并发信号量");
                }
            }
        }

        private async Task<PythonExecutionResult> RunProcessAsync(PythonTarget pyTarget)
        {
            var stopwatch = Stopwatch.StartNew();
            var startInfo = new ProcessStartInfo
            {
                FileName = pyTarget.PythonExecutable,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            // 参数安全传递
            startInfo.ArgumentList.Add(pyTarget.ScriptPath);
            foreach (var arg in pyTarget.Args)
                startInfo.ArgumentList.Add(arg);

            if (!string.IsNullOrWhiteSpace(pyTarget.WorkingDirectory))
                startInfo.WorkingDirectory = pyTarget.WorkingDirectory;

            if (pyTarget.Env != null)
            {
                foreach (var kv in pyTarget.Env)
                    startInfo.Environment[kv.Key] = kv.Value;
            }

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) stdoutBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };

            var tcs = new TaskCompletionSource<int>();
            process.Exited += (s, e) => tcs.TrySetResult(process.ExitCode);

            try
            {
                _logger.LogDebug($"启动 Python 进程: {pyTarget.PythonExecutable} {pyTarget.ScriptPath}");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"启动 Python 进程失败: {pyTarget.PythonExecutable}");
                throw;
            }

            var timeoutTask =Task.Delay(pyTarget.TimeoutSeconds * 1000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            var timeoutOccurred = completedTask == timeoutTask;
            if (timeoutOccurred)
            {
                _logger.LogWarning($"Python 脚本 [{pyTarget.ScriptPath}] 执行超时 ({pyTarget.TimeoutSeconds}s), 尝试终止进程");
                KillProcessTree(process);
                throw new TimeoutException($"Python 脚本执行超时 ({pyTarget.TimeoutSeconds}s)");
            }

            int exitCode = await tcs.Task;
            bool isSuccess = Array.Exists(pyTarget.SuccessExitCodes, code => code == exitCode);

            if (!isSuccess)
            {
                _logger.LogWarning($"Python 脚本 [{pyTarget.ScriptPath}] 退出码异常: {exitCode}");
            }

            return new PythonExecutionResult(
                exitCode,
                stdoutBuilder.ToString(),
                stderrBuilder.ToString(),
                isSuccess,
                stopwatch.ElapsedMilliseconds,
                timeoutOccurred
            );
        }

        private void KillProcessTree(Process process)
        {
            if (process.HasExited) return;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    _logger.LogDebug($"Windows: 杀掉进程及子进程 PID={process.Id}");
                    using var kill = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/PID {process.Id} /T /F",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }
                    };
                    kill.Start();
                    kill.WaitForExit();
                }
                else
                {
                    _logger.LogDebug($"Linux/macOS: 杀掉进程 PID={process.Id} (entireProcessTree=true)");
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        if (!process.HasExited)
                        {
                            _logger.LogWarning($"KillProcessTree fallback: 仅杀掉主进程 PID={process.Id}");
                            process.Kill();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"KillProcessTree 失败 PID={process.Id}");
            }
        }
    }
}
