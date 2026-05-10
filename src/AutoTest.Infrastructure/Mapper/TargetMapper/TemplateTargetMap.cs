using AutoTest.Application;
using AutoTest.Core;
using AutoTest.Core.Target.Template;

namespace AutoTest.Infrastructure.Mapper.TargetMapper;

public sealed class TemplateTargetMap : ITargetMap
{
    public string Type => "TEMPLATE";

    public MonitorTarget Map(string json)
    {
        // TargetConfig 就是原始 DSL JSON，直接传入
        return new TemplateTarget(json);
    }
}
