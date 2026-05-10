using System.Text.Json;
using System.Text.RegularExpressions;
using AutoTest.Core.Dsl;

namespace AutoTest.Dsl;

internal class DslParser : IDslParser
{
    private static readonly Regex VarPattern = new(@"\{\{(\w+)(?::([^}]*))?\}\}", RegexOptions.Compiled);

    public Task<StepSequence> ParseAsync(string templateJson, Dictionary<string, string> variables)
    {
        var root = JsonDocument.Parse(templateJson).RootElement.Clone();

        DslSchemaValidator.Validate(root);

        var resolved = ResolveVariables(root, variables);

        var dag = new StepSequence();

        if (resolved.TryGetProperty("steps", out var steps))
            dag.Steps = ParseSteps(steps);

        if (resolved.TryGetProperty("parallel", out var parallel))
            dag.ParallelGroups = ParseParallelGroups(parallel);

        // 构建统一有序 Items 列表（步骤在前，并行组在后，保持向后兼容）
        dag.Items.AddRange(dag.Steps);
        dag.Items.AddRange(dag.ParallelGroups);

        if (resolved.TryGetProperty("timeout", out var timeout))
            dag.GlobalTimeout = TimeSpan.FromSeconds(timeout.GetInt32());

        return Task.FromResult(dag);
    }

    private List<StepDefinition> ParseSteps(JsonElement steps)
    {
        var result = new List<StepDefinition>();
        int i = 0;
        foreach (var step in steps.EnumerateArray())
        {
            result.Add(ParseStep(step, i));
            i++;
        }
        return result;
    }

    private StepDefinition ParseStep(JsonElement step, int index)
    {
        var name = step.GetProperty("name").GetString()!;
        var type = step.GetProperty("type").GetString()!;
        var input = step.GetProperty("input").Clone();

        var def = new StepDefinition
        {
            Name = name,
            Type = type,
            Input = input
        };

        if (step.TryGetProperty("retry", out var retry))
        {
            def.Retry = new RetryPolicy
            {
                Count = retry.TryGetProperty("count", out var c) ? c.GetInt32() : 0,
                DelayMs = retry.TryGetProperty("delayMs", out var d) ? d.GetInt32() : 1000,
                Backoff = retry.TryGetProperty("backoff", out var b) && b.GetString() == "exponential"
                    ? BackoffMode.Exponential : BackoffMode.Fixed,
                RetryableCodes = retry.TryGetProperty("retryableCodes", out var codes)
                    ? codes.EnumerateArray().Select(x => x.GetString()!).ToList() : null
            };
        }

        if (step.TryGetProperty("timeout", out var timeout))
        {
            var val = timeout.GetString() ?? timeout.GetRawText();
            if (val.EndsWith('s') && int.TryParse(val.TrimEnd('s'), out var sec))
                def.Timeout = TimeSpan.FromSeconds(sec);
            else if (int.TryParse(val, out sec))
                def.Timeout = TimeSpan.FromSeconds(sec);
        }

        if (step.TryGetProperty("onFailure", out var of))
            def.OnFailure = of.GetString() switch
            {
                "skip" => FailureStrategy.Skip,
                "ignore" => FailureStrategy.Ignore,
                _ => FailureStrategy.Stop
            };

        if (step.TryGetProperty("extract", out var extract))
        {
            def.Extract = extract.EnumerateArray().Select(e => new ValueExtractor
            {
                Name = e.GetProperty("name").GetString()!,
                Source = e.GetProperty("source").GetString() == "Header" ? ExtractSource.Header : ExtractSource.Body,
                Method = e.GetProperty("method").GetString() switch
                {
                    "Regex" => ExtractMethod.Regex,
                    "Plain" => ExtractMethod.Plain,
                    _ => ExtractMethod.JsonPath
                },
                Expression = e.GetProperty("expression").GetString()!
            }).ToList();
        }

        if (step.TryGetProperty("assertions", out var assertions))
        {
            def.Assertions = assertions.EnumerateArray().Select(a =>
            {
                var ad = new AssertionDef
                {
                    Field = a.GetProperty("field").GetString()!,
                    Operator = a.GetProperty("operator").GetString()!,
                    Expected = a.GetProperty("expected").GetString()!
                };
                if (a.TryGetProperty("headerKey", out var hk))
                    ad.HeaderKey = hk.GetString();
                return ad;
            }).ToList();
        }

        return def;
    }

    private List<ParallelGroup> ParseParallelGroups(JsonElement parallel)
    {
        var groups = new List<ParallelGroup>();
        foreach (var group in parallel.EnumerateArray())
        {
            var g = new ParallelGroup
            {
                Name = group.TryGetProperty("name", out var n) ? n.GetString()! : "",
                Steps = ParseSteps(group.GetProperty("steps")),
                Mode = group.TryGetProperty("mode", out var m) && m.GetString() == "any"
                    ? ParallelMode.Any : ParallelMode.All
            };
            if (group.TryGetProperty("timeout", out var t) && t.GetString() is string ts && ts.EndsWith('s')
                && int.TryParse(ts.TrimEnd('s'), out var sec))
                g.Timeout = TimeSpan.FromSeconds(sec);
            groups.Add(g);
        }
        return groups;
    }

    private JsonElement ResolveVariables(JsonElement element, Dictionary<string, string> variables)
    {
        var json = element.GetRawText();
        var resolved = VarPattern.Replace(json, match =>
        {
            var name = match.Groups[1].Value;
            var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : null;

            if (variables.TryGetValue(name, out var value))
                return JsonEncodedText.Encode(value).ToString();

            if (defaultValue != null)
                return JsonEncodedText.Encode(defaultValue).ToString();

            throw new InvalidOperationException($"模板变量 '{name}' 未提供值且无默认值");
        });

        return JsonDocument.Parse(resolved).RootElement.Clone();
    }
}
