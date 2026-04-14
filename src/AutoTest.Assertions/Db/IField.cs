using AutoTest.Core;
using AutoTest.Execution.Db;

namespace AutoTest.Assertion.Db
{
    public interface IField
    {
        bool CanResolve(ExecutionResult context);

        object? Resolve(DbAssertionField field, ExecutionResult result, int rowIndex = 0, string columnName = "");
    }
}
