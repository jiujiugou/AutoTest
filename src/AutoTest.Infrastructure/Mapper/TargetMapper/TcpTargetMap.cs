using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Target;

namespace AutoTest.Infrastructure.Mapper.TargetMapper;

public sealed class TcpTargetMap : ITargetMap
{
    public string Type => "TCP";

    public MonitorTarget Map(string json)
    {
        var dto = JsonSerializer.Deserialize<TcpTargetDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        var fallbackMs = dto.Timeout * 1000;

        return new TcpTarget(
            host: dto.Host,
            port: dto.Port,
            timeout: dto.Timeout,
            messages: dto.Messages,
            useTls: dto.UseTls,
            ignoreSslErrors: dto.IgnoreSslErrors,
            connectTimeoutMs: dto.ConnectTimeoutMs > 0 ? dto.ConnectTimeoutMs : fallbackMs,
            readTimeoutMs: dto.ReadTimeoutMs > 0 ? dto.ReadTimeoutMs : fallbackMs,
            writeTimeoutMs: dto.WriteTimeoutMs > 0 ? dto.WriteTimeoutMs : fallbackMs,
            enableRetry: dto.EnableRetry,
            retryCount: dto.RetryCount,
            retryDelayMs: dto.RetryDelayMs
        );
    }
}
