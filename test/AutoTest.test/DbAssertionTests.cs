using AutoTest.Assertion;
using AutoTest.Assertion.Db;
using AutoTest.Assertions;
using AutoTest.Core.Assertion;
using AutoTest.Execution.Db;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoTest.Tests.Assertions.Db;

public class DbAssertionTests
{
    [Fact]
    public void DbField_ShouldResolve_RowValue_AffectedRows_Scalar_Elapsed_Sql()
    {
        var services = new ServiceCollection();
        services.AddDbAssertion();
        var provider = services.BuildServiceProvider();

        var resolver = provider.GetRequiredService<IEnumerable<AutoTest.Assertion.Db.IField>>().Single();

        var result = new DbExecutionResult(true, "ok")
        {
            Rows = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["Name"] = "alice",
                    ["Age"] = 18
                }
            },
            AffectedRows = 2,
            Scalar = 7,
            Sql = "select 1",
            ElapsedMilliseconds = 123
        };

        resolver.CanResolve(result).Should().BeTrue();
        resolver.Resolve(DbAssertionField.RowValue, result, rowIndex: 0, columnName: "Name").Should().Be("alice");
        resolver.Resolve(DbAssertionField.AffectedRows, result).Should().Be(2);
        resolver.Resolve(DbAssertionField.Scalar, result).Should().Be(7);
        resolver.Resolve(DbAssertionField.ElapsedMilliseconds, result).Should().Be(123);
        resolver.Resolve(DbAssertionField.Sql, result).Should().Be("select 1");
    }

    [Fact]
    public void DbField_RowValue_ShouldReturnNull_WhenColumnMissing()
    {
        var services = new ServiceCollection();
        services.AddDbAssertion();
        var provider = services.BuildServiceProvider();

        var resolver = provider.GetRequiredService<IEnumerable<AutoTest.Assertion.Db.IField>>().Single();

        var result = new DbExecutionResult(true, "ok")
        {
            Rows = new List<Dictionary<string, object>> { new() { ["Name"] = "alice" } }
        };

        resolver.Resolve(DbAssertionField.RowValue, result, rowIndex: 0, columnName: "Missing").Should().BeNull();
    }

    [Fact]
    public void DbField_RowValue_ShouldThrow_WhenRowIndexOutOfRange()
    {
        var services = new ServiceCollection();
        services.AddDbAssertion();
        var provider = services.BuildServiceProvider();

        var resolver = provider.GetRequiredService<IEnumerable<AutoTest.Assertion.Db.IField>>().Single();

        var result = new DbExecutionResult(true, "ok")
        {
            Rows = new List<Dictionary<string, object>> { new() { ["Name"] = "alice" } }
        };

        Action act = () => resolver.Resolve(DbAssertionField.RowValue, result, rowIndex: 1, columnName: "Name");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task DbAssertion_ShouldPass_WhenEqual()
    {
        var services = new ServiceCollection();
        services.AddDbAssertion();
        services.AddSingleton(typeof(AssertionOperator), AssertionOperator.Equal);
        services.AddOperatorAssertion();
        var provider = services.BuildServiceProvider();

        var resolvers = provider.GetRequiredService<IEnumerable<AutoTest.Assertion.Db.IField>>();
        var op = provider.GetRequiredService<IOperator>();

        var assertion = new AutoTest.Assertion.Db.DbAssertion(
            Guid.NewGuid(),
            DbAssertionField.RowValue,
            expected: "alice",
            resolvers,
            op,
            rowIndex: 0,
            columnName: "Name");

        var execution = new DbExecutionResult(true, "ok")
        {
            Rows = new List<Dictionary<string, object>> { new() { ["Name"] = "alice" } }
        };

        var r = await assertion.EvaluateAsync(execution);
        r.IsSuccess.Should().BeTrue();
        r.Target.Should().Be("RowValue[0].Name");
    }

    [Fact]
    public async Task DbAssertion_ShouldFail_WhenNoResolver()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(AssertionOperator), AssertionOperator.Equal);
        services.AddOperatorAssertion();
        var provider = services.BuildServiceProvider();

        var op = provider.GetRequiredService<IOperator>();

        var assertion = new AutoTest.Assertion.Db.DbAssertion(
            Guid.NewGuid(),
            DbAssertionField.AffectedRows,
            expected: "1",
            Enumerable.Empty<AutoTest.Assertion.Db.IField>(),
            op);

        var execution = new DbExecutionResult(true, "ok") { AffectedRows = 1 };

        var r = await assertion.EvaluateAsync(execution);
        r.IsSuccess.Should().BeFalse();
        r.Message.Should().Contain("No resolver found");
    }
}

