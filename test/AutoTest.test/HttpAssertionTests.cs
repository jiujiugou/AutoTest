using System;
using System.Threading.Tasks;
using AutoTest.Assertions.Http;
using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Assertion;
using Moq;
using Xunit;

namespace AutoTest.Tests.Assertions.Http
{
    public class HttpAssertionTests
    {
        // -----------------------------
        // 1. 非 IHttpExecutionResult
        // -----------------------------
        [Fact]
        public async Task EvaluateAsync_NotHttpExecutionResult_ShouldFail()
        {
            var assertion = new HttpAssertion(
                Guid.NewGuid(),
                HttpAssertionField.Body,
                HttpAssertionOperator.Equal,
                "expected"
            );

            var mock = new FakeExecutionResult(false, "failed");

            var result = await assertion.EvaluateAsync(mock);

            Assert.False(result.IsSuccess);
            Assert.Contains("not HttpExecutionResult", result.Message);
            Assert.Equal("expected", result.Expected);
        }
        [Fact]
        public async Task EvaluateAsync_Equal_ShouldPass()
        {
            var assertion = new HttpAssertion(
                Guid.NewGuid(),
                HttpAssertionField.Body,
                HttpAssertionOperator.Equal,
                "hello"
            );

            var executionResult = new FakeHttpExecutionResult(true, "success")
            {
                Body = "hello",
                StatusCode = 200
            };

            var result = await assertion.EvaluateAsync(executionResult);

            Assert.True(result.IsSuccess);
            Assert.Equal("Assertion passed", result.Message);
            Assert.Equal("hello", result.Actual);
        }
        [Fact]
        public async Task EvaluateAsync_Equal_ShouldFail()
        {
            var assertion = new HttpAssertion(
                Guid.NewGuid(),
                HttpAssertionField.Body,
                HttpAssertionOperator.Equal,
                "world"
            );

            var executionResult = new FakeHttpExecutionResult(true, "success")
            {
                Body = "hello"
            };

            var result = await assertion.EvaluateAsync(executionResult);

            Assert.False(result.IsSuccess);
            Assert.Contains("Assertion failed", result.Message);
        }
    }


    public class FakeExecutionResult : ExecutionResult
    {
        public FakeExecutionResult(bool success, string message) : base(success, message)
        {
        }
    }

    public class FakeHttpExecutionResult : ExecutionResult, IHttpExecutionResult
    {
        public FakeHttpExecutionResult(bool success, string message) : base(success, message)
        {
        }

        public int StatusCode { get; set; }
        public string Body { get; set; } = "";
    }
}