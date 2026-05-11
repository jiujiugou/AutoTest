using System.Data;
using AutoTest.Application;
using AutoTest.Application.Builder;
using AutoTest.Application.Execution;
using AutoTest.Application.Pipelines;
using AutoTest.Application.Step;
using AutoTest.Assertion;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Dsl;
using AutoTest.Core.Execution;
using AutoTest.Core.ExecutionPipeline;
using AutoTest.Core.Repositories;
using AutoTest.Execution;
using AutoTest.Infrastructure;
using AutoTest.Workflow;
using AutoTest.Infrastructure.Mapper.AssertionMapper;
using AutoTest.Infrastructure.Mapper.AssertionRuleMapper;
using AutoTest.Infrastructure.Mapper.TargetMapper;
using CacheCommons;
using Dapper;
using LockCommons;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Cli;

public static class CliServiceCollectionExtensions
{
    /// <summary>
    /// CLI 专用 DI 注册：只注册执行路径所需的服务，跳过 Hangfire/SignalR/Elasticsearch/AI 等服务器组件。
    /// </summary>
    public static IServiceCollection AddAutoTestCli(this IServiceCollection services, IConfiguration configuration)
    {
        SqlMapper.AddTypeHandler(new CliGuidTypeHandler());

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["Database:ConnectionString"]
            ?? configuration["ConnectionStrings:DefaultConnection"]
            ?? throw new InvalidOperationException("Missing connection string");

        // DB
        services.AddScoped<IDbConnection>(_ => new SqlConnection(connectionString));

        // Unit of work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMonitorRepository, MonitorRepository>();
        services.AddScoped<IExecutionRecordRepository, ExecutionRecordRepository>();
        services.AddScoped<IOutboxRepository, NullOutboxRepository>();
        services.AddScoped<ITestPlanRepository, TestPlanRepository>();
        services.AddScoped<IDashboardService, DashboardService>();

        // Target & Assertion mappers
        services.AddScoped<ITargetMap, HttpTargetMap>();
        services.AddScoped<ITargetMap, TcpTargetMap>();
        services.AddScoped<ITargetMap, DbTargetMap>();
        services.AddScoped<ITargetMap, PythonTargetMap>();
        services.AddScoped<ITargetMap, TemplateTargetMap>();
        services.AddScoped<IAssertionMap, HttpAssertionMap>();
        services.AddScoped<IAssertionMap, TcpAssertionMap>();
        services.AddScoped<IAssertionMap, DbAssertionMap>();
        services.AddScoped<IAssertionMap, PythonAssertionMap>();
        services.AddScoped<IAssertionRuleMap, HttpAssertionRuleMap>();
        services.AddScoped<IAssertionRuleMap, TcpAssertionRuleMap>();
        services.AddScoped<IAssertionRuleMap, DbAssertionRuleMap>();
        services.AddScoped<IAssertionRuleMap, PythonAssertionRuleMap>();

        // Application services
        services.AddScoped<IMonitorService, MonitorService>();
        services.AddScoped<IOrchestrator, Orchestrator>();
        services.AddScoped<ITestPlanService, TestPlanService>();
        services.AddScoped<ITestReportService, TestReportService>();
        services.AddScoped<ExecutionEngineResolver>();
        services.AddScoped<AssertionEngine>();
        services.AddScoped<ExecutionStep>();
        services.AddScoped<AssertionStep>();
        services.AddScoped<DefaultPipeline>();
        services.AddScoped<TemplatePipeline>();

        // Pipeline selector (inline — PipelineSelector is internal in Application)
        services.AddScoped<IPipeline>(sp =>
        {
            var @default = sp.GetRequiredService<DefaultPipeline>();
            var template = sp.GetRequiredService<TemplatePipeline>();
            return new CliPipelineSelector(@default, template);
        });

        // Execution layer
        services.AddAutoTestExecution();

        // Assertions
        services.AddOperatorAssertion();

        // Workflow (without Redis)
        services.AddScoped<IDslParser, DslParser>();
        services.AddScoped<TemplateResolutionStep>();
        services.AddScoped<WorkflowExecutionStep>();
        services.AddSingleton<CircuitBreaker>();
        services.AddScoped<IProgressStore, NullProgressStore>();
        services.AddSingleton<IDistributedLock, InProcessLock>();
        services.AddScoped<IVariableResolver, Infrastructure.VariableResolver>();
        services.AddScoped<IResponseValueExtractor, Infrastructure.ResponseValueExtractor>();

        // Cache
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();

        // DslRunner
        services.AddScoped<DslRunner>();

        return services;
    }
}

/// <summary>
/// 管道选择器（替代 Application 中 internal 的 PipelineSelector）。
/// </summary>
internal class CliPipelineSelector : IPipeline
{
    private readonly DefaultPipeline _default;
    private readonly TemplatePipeline _template;

    public CliPipelineSelector(DefaultPipeline @default, TemplatePipeline template)
    {
        _default = @default;
        _template = template;
    }

    public async Task ExecuteAsync(PipelineContext context)
    {
        var pipeline = context.Monitor.IsTemplate ? (IPipeline)_template : _default;
        await pipeline.ExecuteAsync(context);
    }
}

/// <summary>
/// GUID 类型处理器（Dapper），与 Infrastructure 中的实现一致。
/// </summary>
internal class CliGuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.DbType = DbType.Guid;
        parameter.Value = value;
    }

    public override Guid Parse(object value)
    {
        return value switch
        {
            Guid g => g,
            string s => Guid.Parse(s),
            byte[] b => new Guid(b),
            _ => Guid.Parse(value.ToString()!)
        };
    }
}
