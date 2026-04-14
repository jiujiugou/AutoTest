namespace AutoTest.Core.Assertion;

/// <summary>
/// 断言规则定义：描述对一次执行结果的判定方式。
/// </summary>
/// <remarks>
/// 断言规则采用 <see cref="Type"/> + <see cref="ConfigJson"/> 的方式存储，便于扩展不同类型断言。
/// </remarks>
public class AssertionRule
{
    /// <summary>
    /// 断言规则 ID。
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// 断言类型标识（如 HTTP/TCP/DB/PYTHON）。
    /// </summary>
    public readonly string Type;

    /// <summary>
    /// 断言配置 JSON（与 <see cref="Type"/> 对应）。
    /// </summary>
    public readonly string ConfigJson;

    /// <summary>
    /// 创建断言规则。
    /// </summary>
    /// <param name="id">断言 ID。</param>
    /// <param name="type">断言类型标识。</param>
    /// <param name="configJson">断言配置 JSON。</param>
    public AssertionRule(Guid id, string type, string configJson)
    {
        Id = id;
        Type = type;
        ConfigJson = configJson;
    }
}
