namespace AutoTest.Core.Assertion;

public record AssertionResult(
    Guid AssertionId,
    string Target,
    bool IsSuccess,
    string? Actual,
    string? Expected,
    string? Message
);
