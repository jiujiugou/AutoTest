namespace AutoTest.Core.Execution;

/// <summary>
/// Python 沙箱执行配置
/// </summary>
public class PythonSandboxOptions
{
    /// <summary>沙箱模式: "process" (直接进程) / "docker" (容器隔离)</summary>
    public string Mode { get; set; } = "process";

    /// <summary>Docker 镜像</summary>
    public string DockerImage { get; set; } = "python:3.11-slim";

    /// <summary>内存限制 (例: "256m")</summary>
    public string MemoryLimit { get; set; } = "256m";

    /// <summary>CPU 限制 (核数)</summary>
    public string CpuLimit { get; set; } = "1.0";

    /// <summary>是否禁用容器网络</summary>
    public bool NoNetwork { get; set; } = true;

    /// <summary>容器只读文件系统</summary>
    public bool ReadOnlyRoot { get; set; } = true;

    /// <summary>Docker 运行超时秒数 (额外 buffer)</summary>
    public int DockerTimeoutBufferSeconds { get; set; } = 5;
}
