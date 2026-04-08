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
        public int StatusCode { get; set;}

        /// <summary>
        /// 响应内容
        /// </summary>
        public string? Body { get; set;}

        public IReadOnlyDictionary<string, string[]>? Headers { get; set;}

        public long? ElapsedMilliseconds { get; set; }

        /// <summary>
        /// 构造成功结果
        /// </summary>
        public HttpExecutionResult(int statusCode, string? body, bool isExecutionSuccess, IReadOnlyDictionary<string, string[]> headers, long elapsedMilliseconds = 0)
            : base(isExecutionSuccess, "")
        {
            StatusCode = statusCode;
            Body = body;
            Headers = headers;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        /// <summary>
        /// 构造失败结果
        /// </summary>
        public HttpExecutionResult(int statusCode, string? body, bool isExecutionSuccess, string errorMessage)
            : base(isExecutionSuccess, errorMessage)
        {
            StatusCode = statusCode;
            Body = body;
        }
    }
}
