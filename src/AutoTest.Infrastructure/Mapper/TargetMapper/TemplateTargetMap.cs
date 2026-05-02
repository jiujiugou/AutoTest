using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Target.Template;

namespace AutoTest.Infrastructure.Mapper.TargetMapper;

public sealed class TemplateTargetMap : ITargetMap
{
    public string Type => "TEMPLATE";

    public MonitorTarget Map(string json)
    {
        var dto = JsonSerializer.Deserialize<TemplateTargetDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        return new TemplateTarget(dto.DslJson);
    }
}
