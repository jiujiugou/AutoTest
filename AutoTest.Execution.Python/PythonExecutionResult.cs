using AutoTest.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Execution.Python
{
    /// <summary>
    /// Python 脚本执行结果
    /// </summary>
    public class PythonExecutionResult : ExecutionResult
    {
        /// <summary>退出码</summary>
        public int ExitCode { get; set; }

        private string _stdout = string.Empty;
        /// <summary>标准输出，可截断防止数据库爆</summary>
        public string StdOut
        {
            get => _stdout;
            set => _stdout = Truncate(value, 64 * 1024); // 默认最大 64KB
        }

        private string _stderr = string.Empty;
        /// <summary>标准错误，可截断防止数据库爆</summary>
        public string StdErr
        {
            get => _stderr;
            set => _stderr = Truncate(value, 64 * 1024);
        }

        /// <summary>执行耗时（毫秒）</summary>
        public long ElapsedMs { get; set; }

        /// <summary>是否超时</summary>
        public bool TimedOut { get; set; }

        /// <summary>命令行预览（可用于排查，注意不要包含敏感参数）</summary>
        public string? CommandLinePreview { get; set; }

        /// <summary>带参数构造</summary>
        public PythonExecutionResult(int exitCode, string stdout, string stderr, bool isExecutionSuccess, long elapsedMs, bool timedOut, string? Errormessage=null,  string? commandLinePreview = null)
        : base(
            !timedOut && isExecutionSuccess,
            !timedOut && isExecutionSuccess
                ? "Python 脚本执行成功"
                : (timedOut ? "执行超时" : (Errormessage ?? $"执行失败, ExitCode={exitCode}"))
        )
        {
            ExitCode = exitCode;
            StdOut = stdout;
            StdErr = stderr;
            ElapsedMs = elapsedMs;
            TimedOut = timedOut;
            CommandLinePreview = commandLinePreview;
        }

        /// <summary>截断字符串，防止过长输出</summary>
        private static string Truncate(string? input, int maxLength)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Length <= maxLength ? input : input.Substring(0, maxLength) + "...[truncated]";
        }
    }
}
