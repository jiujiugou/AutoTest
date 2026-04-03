using System.Data;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;

namespace AutoTest.Core.Abstraction;

public interface IExecutionRecordRepository
{
    Task AddAsync(ExecutionRecord record, IDbTransaction? tx = null);
    Task AddAssertionResultsAsync(Guid executionId, IEnumerable<AssertionResult> results, IDbTransaction? tx = null);

    Task<ExecutionRecord?> GetLatestByMonitorIdAsync(Guid monitorId);
    Task<IEnumerable<ExecutionRecord>> GetByMonitorIdAsync(Guid monitorId, int take = 20);
    Task<IEnumerable<AssertionResult>> GetAssertionResultsAsync(Guid executionId);
}

