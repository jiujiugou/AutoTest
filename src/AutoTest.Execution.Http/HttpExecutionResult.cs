using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;

namespace AutoTest.Execution.Http
{
    public class HttpExecutionResult : ExecutionResult, IHttpExecutionResult
    {
        /// <summary>
        /// HTTP 状态码，比如 200、404
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// 响应内容
        /// </summary>
        public string Body { get; }

        public long ElapsedMilliseconds { get; set; }
        /// <summary>
        /// 构造成功结果
        /// </summary>
        public HttpExecutionResult(int statusCode, string body, bool isExecutionSuccess, long elapsedMilliseconds = 0)
            : base(isExecutionSuccess, "")
        {
            StatusCode = statusCode;
            Body = body;
            ElapsedMilliseconds = elapsedMilliseconds;
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
