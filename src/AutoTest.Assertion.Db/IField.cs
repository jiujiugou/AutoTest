using AutoTest.Core;
using AutoTest.Execution.Db;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Assertion.Db
{
    public interface IField
    {
        bool CanResolve(ExecutionResult context);

        object? Resolve(DbAssertionField field, ExecutionResult result, int rowIndex = 0, string columnName = "");
    }
}
