using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Target.Python;

namespace AutoTest.Infrastructure.Mapper.TargetMapper;

/// <summary>
/// Python 目标映射器：将前端提交的 JSON 配置反序列化为 <see cref="PythonTarget"/>。
/// </summary>
public sealed class PythonTargetMap : ITargetMap
{
    /// <summary>
    /// 映射器支持的目标类型标识。
    /// </summary>
    public string Type => "PYTHON";

    /// <summary>
    /// 将 JSON 配置映射为领域目标对象。
    /// </summary>
    /// <param name="json">目标配置 JSON。</param>
    /// <returns>目标对象。</returns>
    public MonitorTarget Map(string json)
    {
        var dto = JsonSerializer.Deserialize<PythonTargetDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (dto == null)
            throw new InvalidOperationException("Invalid Python target config JSON");

        return new PythonTarget
        {
            ScriptPath = dto.ScriptPath,
            ScriptContent = dto.ScriptContent,
            Args = dto.Args,
            WorkingDirectory = dto.WorkingDirectory,
            PythonExecutable = dto.PythonExecutable,
            TimeoutSeconds = dto.TimeoutSeconds,
            EnableRetry = dto.EnableRetry,
            RetryCount = dto.RetryCount,
            RetryDelayMs = dto.RetryDelayMs,
            EnableRateLimit = dto.EnableRateLimit,
            MaxConcurrency = dto.MaxConcurrency,
            Env = dto.Env,
            SuccessExitCodes = dto.SuccessExitCodes
        };
    }
}
