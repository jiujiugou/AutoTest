using AutoTest.Core;
using AutoTest.Core.Dsl;
using AutoTest.Core.ExecutionPipeline;
using AutoTest.Core.Target;
using AutoTest.Core.Target.Template;
using AutoTest.Dsl;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoTest.Tests.Dsl;

public class DslTests
{
    // ========================
    // DslSchemaValidator (indirectly via DslParser)
    // ========================

    private static IDslParser CreateParser()
    {
        var services = new ServiceCollection();
        services.AddAutoTestDsl();
        return services.BuildServiceProvider().GetRequiredService<IDslParser>();
    }

    [Fact]
    public async Task ParseAsync_ShouldThrow_WhenRootIsNotObject()
    {
        var parser = CreateParser();
        var act = async () => await parser.ParseAsync("[1,2,3]", new());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*根节点必须是一个 JSON 对象*");
    }

    [Fact]
    public async Task ParseAsync_ShouldThrow_WhenMissingSteps()
    {
        var parser = CreateParser();
        var act = async () => await parser.ParseAsync("""{ "name": "test" }""", new());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*必须包含 'steps' 字段*");
    }

    [Fact]
    public async Task ParseAsync_ShouldThrow_WhenStepsNotArray()
    {
        var parser = CreateParser();
        var act = async () => await parser.ParseAsync("""{ "steps": "bad" }""", new());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*必须是数组*");
    }

    [Fact]
    public async Task ParseAsync_ShouldThrow_WhenStepMissingName()
    {
        var parser = CreateParser();
        var act = async () => await parser.ParseAsync("""{ "steps": [{ "type": "http", "input": {} }] }""", new());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*缺少必填字段 'name'*");
    }

    [Fact]
    public async Task ParseAsync_ShouldThrow_WhenStepMissingType()
    {
        var parser = CreateParser();
        var act = async () => await parser.ParseAsync("""{ "steps": [{ "name": "s1", "input": {} }] }""", new());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*缺少必填字段 'type'*");
    }

    [Fact]
    public async Task ParseAsync_ShouldThrow_WhenStepMissingInput()
    {
        var parser = CreateParser();
        var act = async () => await parser.ParseAsync("""{ "steps": [{ "name": "s1", "type": "http" }] }""", new());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*缺少必填字段 'input'*");
    }

    [Fact]
    public async Task ParseAsync_ShouldThrow_WhenStepTypeInvalid()
    {
        var parser = CreateParser();
        var act = async () => await parser.ParseAsync(
            """{ "steps": [{ "name": "s1", "type": "websocket", "input": {} }] }""", new());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*无效*websocket*");
    }

    [Fact]
    public async Task ParseAsync_ShouldAccept_AllFourValidTypes()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                { "name": "httpStep", "type": "http", "input": { "url": "http://a.com" } },
                { "name": "tcpStep", "type": "tcp", "input": { "host": "127.0.0.1", "port": 80 } },
                { "name": "dbStep", "type": "db", "input": { "connectionString": "...", "sql": "select 1" } },
                { "name": "pyStep", "type": "python", "input": { "scriptPath": "a.py" } }
            ]
        }
        """;

        var dag = await parser.ParseAsync(json, new());
        dag.Steps.Should().HaveCount(4);
    }

    [Fact]
    public async Task ParseAsync_ShouldThrow_WhenNestedParallel()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                { "name": "s1", "type": "http", "input": { "url": "http://a.com" }, "parallel": [] }
            ]
        }
        """;
        var act = async () => await parser.ParseAsync(json, new());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*parallel*顶级字段*");
    }

    // ========================
    // DslParser — 正常解析
    // ========================

    [Fact]
    public async Task ParseAsync_ShouldParseSingleStep()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                { "name": "checkHealth", "type": "http", "input": { "url": "http://example.com/api" } }
            ]
        }
        """;

        var dag = await parser.ParseAsync(json, new());

        dag.Steps.Should().HaveCount(1);
        dag.Steps[0].Name.Should().Be("checkHealth");
        dag.Steps[0].Type.Should().Be("http");
        dag.Steps[0].Input.GetProperty("url").GetString().Should().Be("http://example.com/api");
    }

    [Fact]
    public async Task ParseAsync_ShouldParseMultipleSteps()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                { "name": "login", "type": "http", "input": { "url": "/api/login" } },
                { "name": "verify", "type": "tcp", "input": { "host": "db", "port": 3306 } }
            ]
        }
        """;

        var dag = await parser.ParseAsync(json, new());

        dag.Steps.Should().HaveCount(2);
        dag.Steps[0].Name.Should().Be("login");
        dag.Steps[1].Type.Should().Be("tcp");
    }

    [Fact]
    public async Task ParseAsync_ShouldResolveVariables()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                { "name": "check", "type": "http", "input": { "url": "{{host}}/api/health" } }
            ]
        }
        """;

        var dag = await parser.ParseAsync(json, new() { ["host"] = "https://example.com" });

        dag.Steps[0].Input.GetProperty("url").GetString().Should().Be("https://example.com/api/health");
    }

    [Fact]
    public async Task ParseAsync_ShouldUseDefaultValue_WhenVariableMissing()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                { "name": "check", "type": "http", "input": { "url": "{{host:localhost}}/api" } }
            ]
        }
        """;

        var dag = await parser.ParseAsync(json, new());

        dag.Steps[0].Input.GetProperty("url").GetString().Should().Be("localhost/api");
    }

    [Fact]
    public async Task ParseAsync_ShouldThrow_WhenVariableMissingAndNoDefault()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                { "name": "check", "type": "http", "input": { "url": "{{host}}/api" } }
            ]
        }
        """;

        var act = async () => await parser.ParseAsync(json, new());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*未提供值*");
    }

    [Fact]
    public async Task ParseAsync_ShouldParseRetryConfig()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                {
                    "name": "s1", "type": "http",
                    "input": { "url": "http://a.com" },
                    "retry": { "count": 3, "delayMs": 500, "backoff": "exponential" }
                }
            ]
        }
        """;

        var dag = await parser.ParseAsync(json, new());

        dag.Steps[0].Retry!.Count.Should().Be(3);
        dag.Steps[0].Retry.DelayMs.Should().Be(500);
        dag.Steps[0].Retry.Backoff.Should().Be(BackoffMode.Exponential);
    }

    [Fact]
    public async Task ParseAsync_ShouldParseTimeout()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                { "name": "s1", "type": "http", "input": { "url": "http://a.com" }, "timeout": "30s" }
            ]
        }
        """;

        var dag = await parser.ParseAsync(json, new());

        dag.Steps[0].Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ParseAsync_ShouldParseOnFailure()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                { "name": "s1", "type": "http", "input": { "url": "http://a.com" }, "onFailure": "skip" }
            ]
        }
        """;

        var dag = await parser.ParseAsync(json, new());

        dag.Steps[0].OnFailure.Should().Be(FailureStrategy.Skip);
    }

    [Fact]
    public async Task ParseAsync_ShouldParseParallelGroups()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                { "name": "serial", "type": "http", "input": { "url": "http://main/api" } }
            ],
            "parallel": [
                {
                    "name": "group1",
                    "mode": "all",
                    "timeout": "60s",
                    "steps": [
                        { "name": "p1", "type": "http", "input": { "url": "http://svc1/api" } },
                        { "name": "p2", "type": "http", "input": { "url": "http://svc2/api" } }
                    ]
                }
            ]
        }
        """;

        var dag = await parser.ParseAsync(json, new());

        dag.ParallelGroups.Should().HaveCount(1);
        dag.ParallelGroups[0].Name.Should().Be("group1");
        dag.ParallelGroups[0].Mode.Should().Be(ParallelMode.All);
        dag.ParallelGroups[0].Steps.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseAsync_ShouldParseGlobalTimeout()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                { "name": "s1", "type": "http", "input": { "url": "http://a.com" } }
            ],
            "timeout": 120
        }
        """;

        var dag = await parser.ParseAsync(json, new());

        dag.GlobalTimeout.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public async Task ParseAsync_ShouldParseInlineAssertions()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                {
                    "name": "s1", "type": "http",
                    "input": { "url": "http://a.com" },
                    "assertions": [
                        { "field": "StatusCode", "operator": "Equal", "expected": "200" },
                        { "field": "Body", "operator": "Contains", "expected": "ok" }
                    ]
                }
            ]
        }
        """;

        var dag = await parser.ParseAsync(json, new());

        dag.Steps[0].Assertions.Should().HaveCount(2);
        dag.Steps[0].Assertions![1].Field.Should().Be("Body");
        dag.Steps[0].Assertions![1].Operator.Should().Be("Contains");
    }

    [Fact]
    public async Task ParseAsync_ShouldResolveMultipleVariablesInOneInput()
    {
        var parser = CreateParser();
        var json = """
        {
            "steps": [
                { "name": "check", "type": "http", "input": { "url": "{{host}}/api/{{version}}" } }
            ]
        }
        """;

        var dag = await parser.ParseAsync(json, new() { ["host"] = "https://a.com", ["version"] = "v2" });

        dag.Steps[0].Input.GetProperty("url").GetString().Should().Be("https://a.com/api/v2");
    }

    // ========================
    // TemplateResolutionStep
    // ========================

    [Fact]
    public async Task InvokeAsync_ShouldSkip_WhenNotTemplate()
    {
        var services = new ServiceCollection();
        services.AddAutoTestDsl();
        var provider = services.BuildServiceProvider();
        var step = provider.GetRequiredService<IPipelineStep>();

        var monitor = new MonitorEntity(
            Guid.NewGuid(), "test",
            new TcpTarget("127.0.0.1", 80),
            MonitorStatus.Pending, null);

        var context = new PipelineContext(monitor);
        bool nextCalled = false;

        await step.InvokeAsync(context, () => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.Should().BeTrue();
        context.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ShouldParseDsl_WhenTemplate()
    {
        var services = new ServiceCollection();
        services.AddAutoTestDsl();
        var provider = services.BuildServiceProvider();
        var step = provider.GetRequiredService<IPipelineStep>();

        var monitor = new MonitorEntity(
            Guid.NewGuid(), "template-test",
            new TemplateTarget("""{ "steps": [{ "name": "s1", "type": "http", "input": { "url": "http://a.com/{{var}}" } }] }"""),
            MonitorStatus.Pending, null);
        monitor.SetTemplateVariables("""{ "var": "hello" }""");

        var context = new PipelineContext(monitor);
        bool nextCalled = false;

        await step.InvokeAsync(context, () => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.Should().BeTrue();
        context.Items.Should().ContainKey(typeof(DslPipelineContext).FullName!);
        var dslCtx = context.Items[typeof(DslPipelineContext).FullName!] as DslPipelineContext;
        dslCtx.Should().NotBeNull();
        dslCtx!.Dag.Steps.Should().HaveCount(1);
    }

    // ========================
    // TemplateTargetMap
    // ========================

    [Fact]
    public void TemplateTargetMap_ShouldReturnTemplateTarget_WithRawDsl()
    {
        var map = new AutoTest.Infrastructure.Mapper.TargetMapper.TemplateTargetMap();
        const string dsl = """{"steps":[{"name":"s1","type":"http","input":{"url":"http://a.com"}}]}""";

        var target = map.Map(dsl);

        target.Should().BeOfType<TemplateTarget>();
        ((TemplateTarget)target).DslJson.Should().Be(dsl);
        target.Type.Should().Be("TEMPLATE");
        target.ToJson().Should().Be(dsl);
    }
}
