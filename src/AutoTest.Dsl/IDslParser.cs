using AutoTest.Core.Dsl;

namespace AutoTest.Dsl;

public interface IDslParser
{
    Task<StepSequence> ParseAsync(string templateJson, Dictionary<string, string> variables);
}
