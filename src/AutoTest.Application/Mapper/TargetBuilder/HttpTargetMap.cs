using System.Text.Json;
using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Target.Http;

namespace AutoTest.Application.Builder.TargetBuilder;

public class HttpTargetMap : ITargetMap
{
    public string Type => "HTTP";  // 负责 HTTP 类型的 Target

    public MonitorTarget Map(string json)
    {
        // 将 json 反序列化成 DTO
        var dto = JsonSerializer.Deserialize<HttpTargetDto>(json)!;

        // 根据 DTO 构建实际的领域对象 HttpTarget
        return new HttpTarget(
            dto.Method,
            dto.Url,
            dto.Body,
            dto.Headers,
            dto.Query,
            dto.Timeout
        );
    }
}
