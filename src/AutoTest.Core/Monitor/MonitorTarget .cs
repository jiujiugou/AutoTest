namespace AutoTest.Core;

public abstract class MonitorTarget
{
    // 用于数据库存储
    public abstract string Type { get; }

    // 用于数据库存储
    public abstract string ToJson();
}