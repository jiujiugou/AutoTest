namespace AutoTest.Core.Assertion;

/// <summary>
/// 断言评估结果。
/// </summary>
/// <param name="AssertionId">断言 ID。</param>
/// <param name="Target">断言目标字段/键的描述（用于展示与定位）。</param>
/// <param name="IsSuccess">断言是否通过。</param>
/// <param name="Actual">实际值（字符串形式，便于持久化）。</param>
/// <param name="Expected">期望值（字符串形式，便于持久化）。</param>
/// <param name="Message">额外信息（失败原因、提示等）。</param>
public record AssertionResult(
    Guid AssertionId,
    string Target,
    bool IsSuccess,
    string? Actual,
    string? Expected,
    string? Message
);
