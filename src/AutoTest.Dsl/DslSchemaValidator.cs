using System.Text.Json;

namespace AutoTest.Dsl;

internal static class DslSchemaValidator
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
        { "http", "tcp", "db", "python" };

    public static void Validate(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("DSL 根节点必须是一个 JSON 对象");

        var hasSteps = root.TryGetProperty("steps", out var steps);
        if (!hasSteps)
            throw new InvalidOperationException("DSL 必须包含 'steps' 字段");

        ValidateStepsArray(steps);
    }

    private static void ValidateStepsArray(JsonElement steps)
    {
        if (steps.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("'steps' 必须是数组");

        int i = 0;
        foreach (var step in steps.EnumerateArray())
        {
            var path = $"steps[{i}]";

            if (!step.TryGetProperty("name", out var nameEl) || nameEl.GetString() is not { Length: > 0 } name)
                throw new InvalidOperationException($"{path} 缺少必填字段 'name'");

            if (!step.TryGetProperty("type", out var typeEl))
                throw new InvalidOperationException($"{path} 步骤 '{name}' 缺少必填字段 'type'");

            var type = typeEl.GetString() ?? "";
            if (!ValidTypes.Contains(type))
                throw new InvalidOperationException($"{path} 步骤 '{name}' 的 type 无效: '{type}'，允许的值: {string.Join("/", ValidTypes)}");

            if (!step.TryGetProperty("input", out var inputEl))
                throw new InvalidOperationException($"{path} 步骤 '{name}' 缺少必填字段 'input'");

            if (step.TryGetProperty("extract", out var extract) && extract.ValueKind == JsonValueKind.Array)
            {
                int ei = 0;
                foreach (var e in extract.EnumerateArray())
                {
                    if (!e.TryGetProperty("name", out _))
                        throw new InvalidOperationException($"{path}.extract[{ei}] 缺少必填字段 'name'");
                    if (!e.TryGetProperty("source", out _))
                        throw new InvalidOperationException($"{path}.extract[{ei}] 缺少必填字段 'source'");
                    if (!e.TryGetProperty("expression", out _))
                        throw new InvalidOperationException($"{path}.extract[{ei}] 缺少必填字段 'expression'");
                    ei++;
                }
            }

            if (step.TryGetProperty("parallel", out _))
                throw new InvalidOperationException($"{path} 步骤 '{name}' 中 'parallel' 是顶级字段，不能在步骤内使用");

            if (step.TryGetProperty("assertions", out var assertions) && assertions.ValueKind == JsonValueKind.Array)
            {
                int ai = 0;
                foreach (var a in assertions.EnumerateArray())
                {
                    if (!a.TryGetProperty("field", out _))
                        throw new InvalidOperationException($"{path}.assertions[{ai}] 缺少必填字段 'field'");
                    if (!a.TryGetProperty("operator", out _))
                        throw new InvalidOperationException($"{path}.assertions[{ai}] 缺少必填字段 'operator'");
                    if (!a.TryGetProperty("expected", out _))
                        throw new InvalidOperationException($"{path}.assertions[{ai}] 缺少必填字段 'expected'");
                    ai++;
                }
            }

            i++;
        }
    }
}
