namespace AutoTest.Core.Assertion;

public class AssertionRule
{
    public Guid Id { get; private set; }
    public readonly string Type;
    public readonly string ConfigJson;

    public AssertionRule(Guid id, string type, string configJson)
    {
        Id = id;
        Type = type;
        ConfigJson = configJson;
    }
}
