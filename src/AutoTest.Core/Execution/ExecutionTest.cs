using AutoTest.Core.Assertion;

namespace AutoTest.Core.Execution;

public class ExecutionTest : IAggregateRoot
{
    public Guid Id { get; }
    public MonitorStatus Status { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public ExecutionResult? Result { get; private set; }

    public IReadOnlyList<AssertionResult>? AssertionResults { get; private set; }

    public void Start()
    {
        Status = MonitorStatus.Running;
        StartTime = DateTime.UtcNow;
    }

    public void End(ExecutionResult result, IEnumerable<AssertionResult> assertionResults)
    {
        Result = result;
        AssertionResults = assertionResults.ToList();
        EndTime = DateTime.UtcNow;
        Status = assertionResults.All(r => r.IsSuccess)
            ? MonitorStatus.Success
            : MonitorStatus.Failed;
    }

}


