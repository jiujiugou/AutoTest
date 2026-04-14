using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Target;

namespace AutoTest.Infrastructure.Mapper.TargetMapper;

/// <summary>
/// TCP 目标映射器：将前端提交的 JSON 配置反序列化为 <see cref="TcpTarget"/>。
/// </summary>
public sealed class TcpTargetMap : ITargetMap
{
    /// <summary>
    /// 映射器支持的目标类型标识。
    /// </summary>
    public string Type => "TCP";

    /// <summary>
    /// 将 JSON 配置映射为领域目标对象。
    /// </summary>
    /// <param name="json">目标配置 JSON。</param>
    /// <returns>目标对象。</returns>
    public MonitorTarget Map(string json)
    {
        var dto = JsonSerializer.Deserialize<TcpTargetDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        return new TcpTarget(
            dto.Host,
            dto.Port,
            dto.Timeout,
            dto.Messages
        );
    }
}
