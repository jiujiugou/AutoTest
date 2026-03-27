using System.Text.Json;
using AutoTest.Application.Dto;
using AutoTest.Assertions.Http;
using AutoTest.Core.Assertion;

namespace AutoTest.Application.Builder.AssertionBuilder;

public class HttpAssertionBuilder : IAssertionBuilder
{
    public string Type => "HTTP";  // 负责 HTTP 类型的 Assertion

    public AssertionRule Build(string json)
    {
        // 将 json 反序列化成 DTO
        var dto = JsonSerializer.Deserialize<HttpAssertionDto>(json)!;

        return new AssertionRule(
            dto.Id,
            Type,                              // "HTTP"
            JsonSerializer.Serialize(dto)      // 保存 DTO 原始 JSON 配置
        );
    }

}
