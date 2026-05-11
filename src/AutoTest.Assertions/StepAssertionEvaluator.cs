using AutoTest.Core.Dsl;

namespace AutoTest.Assertions;

/// <summary>
/// 对步骤执行结果逐条运行断言规则，支持 Equal / Contains / NotEquals / LessThan / GreaterThan。
/// </summary>
internal class StepAssertionEvaluator : IStepAssertionEvaluator
{
    public List<StepAssertionResult> Evaluate(StepResult result, List<AssertionDef> assertions)
    {
        return assertions.Select(a =>
        {
            var actual = a.Field.ToLowerInvariant() switch
            {
                "statuscode" => result.StatusCode.ToString(),
                "body" => result.Body,
                "responsetime" => result.ElapsedMs.ToString(),
                "elapsed" => result.ElapsedMs.ToString(),
                "header" when a.HeaderKey != null && result.Headers != null
                    => result.Headers.TryGetValue(a.HeaderKey, out var vals) ? string.Join(",", vals) : null,
                _ => null
            };

            var passed = actual != null && a.Operator.ToLowerInvariant() switch
            {
                "equal" => string.Equals(actual, a.Expected, StringComparison.OrdinalIgnoreCase),
                "contains" => actual.Contains(a.Expected, StringComparison.OrdinalIgnoreCase),
                "notequals" => !string.Equals(actual, a.Expected, StringComparison.OrdinalIgnoreCase),
                "lessthan" => double.TryParse(actual, out var an) && double.TryParse(a.Expected, out var en) && an < en,
                "greaterthan" => double.TryParse(actual, out var an2) && double.TryParse(a.Expected, out var en2) && an2 > en2,
                _ => false
            };

            return new StepAssertionResult
            {
                Field = a.Field,
                Operator = a.Operator,
                Expected = a.Expected,
                Actual = actual,
                Passed = passed
            };
        }).ToList();
    }
}
