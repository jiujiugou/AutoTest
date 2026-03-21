using AutoTest.Core;
using AutoTest.Core.Assertion;

namespace AutoTest.Execution.Http
{
    public class HttpExecutionResult : ExecutionResult
    {
        /// <summary>
        /// HTTP 状态码，比如 200、404
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// 响应内容
        /// </summary>
        public string Body { get; }

        /// <summary>
        /// 断言结果
        /// </summary>
        public List<AssertionResult> Assertions { get; set; } = new List<AssertionResult>();

        /// <summary>
        /// 构造成功结果
        /// </summary>
        public HttpExecutionResult(int statusCode, string body, bool isExecutionSuccess)
            : base(isExecutionSuccess, "")
        {
            StatusCode = statusCode;
            Body = body;
        }

        /// <summary>
        /// 构造失败结果
        /// </summary>
        public HttpExecutionResult(int statusCode, string body, bool isExecutionSuccess, string errorMessage)
            : base(isExecutionSuccess, errorMessage)
        {
            StatusCode = statusCode;
            Body = body;
        }
    }
}