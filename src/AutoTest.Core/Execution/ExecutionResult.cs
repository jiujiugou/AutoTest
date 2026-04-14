using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoTest.Core.Assertion;

namespace AutoTest.Core
{
    /// <summary>
    /// 一次执行的基础结果。
    /// </summary>
    /// <remarks>
    /// 不同目标类型（HTTP/TCP/DB/Python）会派生出更具体的结果类型，并可附加额外字段。
    /// 断言结果通过 <see cref="Assertions"/> 聚合到同一个执行结果中，便于整体持久化与展示。
    /// </remarks>
    public abstract class ExecutionResult
    {
        /// <summary>
        /// 执行阶段是否成功（仅代表“执行过程”成功，不包含断言是否通过）。
        /// </summary>
        public bool IsExecutionSuccess { get; private set; }

        /// <summary>
        /// 执行阶段失败时的错误信息。
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// 断言结果列表。通常在执行完成后由断言步骤填充。
        /// </summary>
        public List<AssertionResult> Assertions { get; set; } = new();

        /// <summary>
        /// 创建执行结果。
        /// </summary>
        /// <param name="success">执行阶段是否成功。</param>
        /// <param name="message">错误信息或说明。</param>
        public ExecutionResult(bool success, string message)
        {
            IsExecutionSuccess = success;
            ErrorMessage = message;
        }

    }
}
