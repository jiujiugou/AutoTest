namespace AutoTest.Core.Dsl;

/// <summary>
/// 断言评估器：对步骤执行结果按断言规则逐条校验，返回每条的通过/失败结果。
/// </summary>
public interface IStepAssertionEvaluator
{
    List<StepAssertionResult> Evaluate(StepResult result, List<AssertionDef> assertions);
}
