using System.Text.Json;
using AutoTest.Application.Dto;
using AutoTest.Core.Assertion;

namespace AutoTest.Application.Builder.AssertionBuilder;

public class AssertionRuleMap : IAssertionRuleMap
{
    public string Type => throw new NotImplementedException();

    public AssertionRule Map(string json)
    {
        // 将 json 反序列化成 DTO
        var dto = JsonSerializer.Deserialize<HttpAssertionDto>(json)!;
        return new AssertionRule(
            dto.Id,
            Type, // "HTTP"
            JsonSerializer.Serialize(dto) // 保存 DTO 原始 JSON 配置
        );
    }

}
