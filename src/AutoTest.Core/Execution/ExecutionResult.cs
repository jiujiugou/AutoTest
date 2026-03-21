using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTest.Core
{
    public abstract class ExecutionResult
    {
        public bool IsExecutionSuccess { get; private set; }
        public string ErrorMessage { get; private set; }
        public ExecutionResult(bool success, string message)
        {
            IsExecutionSuccess = success;
            ErrorMessage = message;
        }

    }
}
