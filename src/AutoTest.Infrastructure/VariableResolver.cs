using System.Text.Json;
using AutoTest.Core.Dsl;

namespace AutoTest.Infrastructure;

internal class VariableResolver : IVariableResolver
{
    private static readonly System.Text.RegularExpressions.Regex VarPattern =
        new(@"\{\{(\w+)(?::([^}]*))?\}\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    public string ReplaceJson(string json, Dictionary<string, string> variables)
    {
        return VarPattern.Replace(json, match =>
        {
            var name = match.Groups[1].Value;
            var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : null;

            if (variables.TryGetValue(name, out var value))
                return JsonEncodedText.Encode(value).ToString();

            if (defaultValue != null)
                return JsonEncodedText.Encode(defaultValue).ToString();

            throw new InvalidOperationException($"模板变量 '{name}' 未提供值且无默认值");
        });
    }
}
