using System.Text.Json;

namespace AutoTest.Core.Target.Template;

public class TemplateTarget : MonitorTarget
{
    public override string Type => "TEMPLATE";

    public string DslJson { get; private set; }

    public TemplateTarget(string dslJson)
    {
        DslJson = dslJson;
    }

    public override string ToJson()
    {
        return DslJson;
    }
}
