namespace AutoTest.Core;

public enum MonitorStatus
{
    Pending,        // 等待执行
    Running,        // 执行中
    Success,        // 成功
    Failed,         // 失败
    Timeout,        // 超时
    Canceled        // 取消
}