using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoTest.Core.Assertion;

namespace AutoTest.Core
{
    public abstract class ExecutionResult
    {
        public bool IsExecutionSuccess { get; private set; }
        public string ErrorMessage { get; private set; }
        public List<AssertionResult> Assertions { get; set; } = new();
        public ExecutionResult(bool success, string message)
        {
            IsExecutionSuccess = success;
            ErrorMessage = message;
        }

    }
}
