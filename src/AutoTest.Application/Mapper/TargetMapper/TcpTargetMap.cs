using System.Text.Json;
using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Target;

namespace AutoTest.Application.Mapper.TargetBuilder;

public class TcpTargetMap : ITargetMap
{
    public string Type => "TCP";

    public MonitorTarget Map(string json)
    {
        // 将 json 反序列化成 DTO
        var dto = JsonSerializer.Deserialize<TcpTargetDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        // 根据 DTO 构建实际的领域对象 TcpTarget
        return new TcpTarget(
            dto.Host,
            dto.Port,
            dto.Timeout
        );
    }

}
