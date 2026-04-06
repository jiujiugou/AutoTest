using AutoTest.Core.Target.Db;
using AutoTest.Core.Target.Http;
using AutoTest.Execution.Db;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoTest.Tests.Execution.Db;

public class DbExecutionEngineTests
{
    [Fact]
    public void CanExecute_ShouldReturnTrue_ForDbTarget()
    {
        var engine = new DbExecutionEngine(NullLogger<DbExecutionEngine>.Instance);

        var target = new DbTarget(
            sqlstring: "Server=127.0.0.1;Connection Timeout=1;",
            sql: "select 1",
            dbtype: "sqlserver",
            rows: 0,
            effectedrows: 0,
            commandType: SqlCommandType.Query);

        engine.CanExecute(target).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_WhenTargetIsNotDbTarget()
    {
        var engine = new DbExecutionEngine(NullLogger<DbExecutionEngine>.Instance);

        var target = new HttpTarget();

        var act = async () => await engine.ExecuteAsync(target);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_NotSupported_WhenDbTypeUnknown()
    {
        var engine = new DbExecutionEngine(NullLogger<DbExecutionEngine>.Instance);

        var target = new DbTarget(
            sqlstring: "Server=127.0.0.1;Connection Timeout=1;",
            sql: "select 1",
            dbtype: "sqlite",
            rows: 0,
            effectedrows: 0,
            commandType: SqlCommandType.Query);

        var act = async () => await engine.ExecuteAsync(target);
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*不支持的数据库类型*");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_ForSqlServer_WhenConnectionInvalid()
    {
        var engine = new DbExecutionEngine(NullLogger<DbExecutionEngine>.Instance);

        var target = new DbTarget(
            sqlstring: "Server=127.0.0.1,65000;Database=master;User Id=sa;Password=Password!123;TrustServerCertificate=True;Connect Timeout=1;",
            sql: "select 1",
            dbtype: "sqlserver",
            rows: 0,
            effectedrows: 0,
            commandType: SqlCommandType.Query);

        var r = await engine.ExecuteAsync(target);
        r.IsExecutionSuccess.Should().BeFalse();
        r.ErrorMessage.Should().StartWith("DB 执行失败:");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_ForMySql_WhenConnectionInvalid()
    {
        var engine = new DbExecutionEngine(NullLogger<DbExecutionEngine>.Instance);

        var target = new DbTarget(
            sqlstring: "Server=127.0.0.1;Port=65000;Uid=root;Pwd=x;Database=mysql;ConnectionTimeout=1;Default Command Timeout=1;",
            sql: "select 1",
            dbtype: "mysql",
            rows: 0,
            effectedrows: 0,
            commandType: SqlCommandType.Query);

        var r = await engine.ExecuteAsync(target);
        r.IsExecutionSuccess.Should().BeFalse();
        r.ErrorMessage.Should().StartWith("DB 执行失败:");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_ForPostgreSql_WhenConnectionInvalid()
    {
        var engine = new DbExecutionEngine(NullLogger<DbExecutionEngine>.Instance);

        var target = new DbTarget(
            sqlstring: "Host=127.0.0.1;Port=65000;Username=x;Password=x;Database=postgres;Timeout=1;Command Timeout=1;",
            sql: "select 1",
            dbtype: "postgresql",
            rows: 0,
            effectedrows: 0,
            commandType: SqlCommandType.Query);

        var r = await engine.ExecuteAsync(target);
        r.IsExecutionSuccess.Should().BeFalse();
        r.ErrorMessage.Should().StartWith("DB 执行失败:");
    }
}
