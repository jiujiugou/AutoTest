using AutoTest.Core;
using AutoTest.Execution.Db;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Assertion.Db
{
    internal class DbField : IField
    {
        public bool CanResolve(ExecutionResult context)
        {
            return context is DbExecutionResult;
        }

        public object? Resolve(DbAssertionField field, ExecutionResult result, int rowIndex = 0, string columnName = "")
        {
            if (result is not DbExecutionResult dbResult)
                return null;

            return field switch
            {
                DbAssertionField.RowValue => dbResult.Rows?[rowIndex].TryGetValue(columnName, out var val) == true ? val : null,
                DbAssertionField.AffectedRows => dbResult.AffectedRows,
                DbAssertionField.Scalar => dbResult.Scalar,
                DbAssertionField.ElapsedMilliseconds => dbResult.ElapsedMilliseconds,
                DbAssertionField.Sql => dbResult.Sql,
                _ => null
            };
        }
    }
}
