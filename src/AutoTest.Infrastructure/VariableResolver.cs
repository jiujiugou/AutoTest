using System.Text.Json;
using AutoTest.Core.Dsl;

namespace AutoTest.Infrastructure;

/// <summary>
/// 正则变量替换实现：将 <c>{{name}}</c> 或 <c>{{name:default}}</c> 替换为实际值。
/// 无默认值的缺失变量直接抛异常，避免静默通过。
/// </summary>
public class VariableResolver : IVariableResolver
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
