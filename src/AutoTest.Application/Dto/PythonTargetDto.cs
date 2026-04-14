using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Application.Dto
{
    /// <summary>
    /// Python 监控目标 DTO。
    /// </summary>
    public class PythonTargetDto
    {
        /// <summary>Python 脚本路径（绝对或相对）</summary>
        public string ScriptPath { get; set; } = string.Empty;

        /// <summary>命令行参数数组</summary>
        public string[] Args { get; set; } = Array.Empty<string>();

        /// <summary>工作目录，可为空则使用默认</summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>Python 可执行程序路径，支持虚拟环境</summary>
        public string PythonExecutable { get; set; } = "python";

        /// <summary>执行超时时间（秒）</summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>是否启用重试机制</summary>
        public bool EnableRetry { get; set; } = false;

        /// <summary>重试次数</summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>重试延迟（毫秒）</summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>是否启用并发限流</summary>
        public bool EnableRateLimit { get; set; } = false;

        /// <summary>最大并发数（仅在 EnableRateLimit=true 时生效）</summary>
        public int MaxConcurrency { get; set; } = 1;

        /// <summary>环境变量字典，可选</summary>
        public Dictionary<string, string>? Env { get; set; }

        /// <summary>成功退出码列表，默认 [0]</summary>
        public int[] SuccessExitCodes { get; set; } = new int[] { 0 };
    }
}
