using AutoTest.Assertion;
using AutoTest.Assertions;
using AutoTest.Assertions.Http;
using AutoTest.Core.Assertion;
using AutoTest.Execution.Http;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoTest.Tests.Assertions.Http
{
    public class HttpAssertionTests
    {
        [Fact]
        public void HttpField_ShouldResolve_StatusCode_Body_Header_Elapsed()
        {
            var services = new ServiceCollection();
            services.AddHttpAssertion();
            var provider = services.BuildServiceProvider();
            var resolver = provider.GetRequiredService<IEnumerable<IField>>().Single();

            var result = new HttpExecutionResult(
                200,
                "ok",
                true,
                new Dictionary<string, string> { ["X-Test"] = "abc" },
                123);

            resolver.CanResolve(result).Should().BeTrue();
            resolver.Resolve(HttpAssertionField.StatusCode, result).Should().Be(200);
            resolver.Resolve(HttpAssertionField.Body, result).Should().Be("ok");
            resolver.Resolve(HttpAssertionField.Header, result, "X-Test").Should().Be("abc");
            resolver.Resolve(HttpAssertionField.Elapsed, result).Should().Be(123);
        }

        [Fact]
        public async Task HttpAssertion_ShouldPass_WhenEqual()
        {
            var services = new ServiceCollection();
            services.AddHttpAssertion();
            services.AddSingleton(typeof(AssertionOperator), AssertionOperator.Equal);
            services.AddOperatorAssertion();
            var provider = services.BuildServiceProvider();

            var resolvers = provider.GetRequiredService<IEnumerable<IField>>();
            var op = provider.GetRequiredService<IOperator>();

            var assertion = new HttpAssertion(
                Guid.NewGuid(),
                HttpAssertionField.StatusCode,
                "",
                "200",
                resolvers,
                op);

            var execution = new HttpExecutionResult(200, "ok", true, new Dictionary<string, string>(), 1);
            var r = await assertion.EvaluateAsync(execution);
            r.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task HttpAssertion_ShouldFail_WhenNoResolver()
        {
            var services = new ServiceCollection();
            services.AddSingleton(typeof(AssertionOperator), AssertionOperator.Equal);
            services.AddOperatorAssertion();
            var provider = services.BuildServiceProvider();

            var op = provider.GetRequiredService<IOperator>();
            var assertion = new HttpAssertion(
                Guid.NewGuid(),
                HttpAssertionField.StatusCode,
                "",
                "200",
                Enumerable.Empty<IField>(),
                op);

            var execution = new HttpExecutionResult(200, "ok", true, new Dictionary<string, string>(), 1);
            var r = await assertion.EvaluateAsync(execution);
            r.IsSuccess.Should().BeFalse();
            r.Message.Should().Contain("No resolver found");
        }
    }
}
