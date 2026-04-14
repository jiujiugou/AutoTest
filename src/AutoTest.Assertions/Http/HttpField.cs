using AutoTest.Core;
using AutoTest.Core.Execution;

namespace AutoTest.Assertions.Http
{
    internal class HttpField : IField
    {
        public bool CanResolve(ExecutionResult context)
        {
            // 只处理 IHttpExecutionResult 类型
            return context is IHttpExecutionResult;
        }

        public object? Resolve(HttpAssertionField field, ExecutionResult context, string? key = null)
        {
            if (context is not IHttpExecutionResult httpResult)
                return null;

            return field switch
            {
                HttpAssertionField.StatusCode => httpResult.StatusCode,
                HttpAssertionField.Body => httpResult.Body,
                HttpAssertionField.Header =>
                    key != null && httpResult.Headers != null && httpResult.Headers.TryGetValue(key, out var value)
                        ? value
                        : null,
                HttpAssertionField.ResponseTime => httpResult.ElapsedMilliseconds,
                HttpAssertionField.Elapsed => httpResult.ElapsedMilliseconds,
                _ => null
            };
        }
    }
}
