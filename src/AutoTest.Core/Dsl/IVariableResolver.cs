using System.Text.Json;

namespace AutoTest.Core.Dsl;

public interface IVariableResolver
{
    string ReplaceJson(string json, Dictionary<string, string> variables);
}

public interface IResponseValueExtractor
{
    Task<Dictionary<string, string>> ExtractAsync(
        string body,
        IReadOnlyDictionary<string, string[]> headers,
        List<ValueExtractor> extractors);
}
