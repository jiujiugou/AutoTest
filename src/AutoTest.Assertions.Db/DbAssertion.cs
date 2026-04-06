using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;

namespace AutoTest.Assertions.Db
{
    public class DbAssertion : IAssertion
    {
        public Guid Id { get; set; }
        private readonly string _expectedsql;
        private readonly int AffectedRows;
        public DbAssertion(Guid id,string expectedsql, int affectedRows)
        {
            Id = id;
            _expectedsql = expectedsql;
            AffectedRows = affectedRows;
        }
        public Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult)
        {
            if(executionResult is not IDbExecutionResult dbResult)
            {
                bool passed = AffectedRows > 0;
                return Task.FromResult( new AssertionResult(
                
                    Id,
                    "db",
                    passed,
                    $"受影响的行数: {AffectedRows}",
                     $"期望受影响的行数: > 0",
                     passed ? "断言通过" : "没有影响的行"
                ));
            }
            return Task.FromResult(new AssertionResult(
                Id,
                "db",
                false,
                null,
                null,
                "执行结果不是数据库执行结果"
            ));
        }
    }
}
