using AutoTest.Application.Dto;
using AutoTest.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AutoTest.Application.Mapper.TargetMapper
{
    internal class PythonTargetMap : ITargetMap
    {
        public string Type => "python";

        public MonitorTarget Map(string json)
        {
           var dto= JsonSerializer.Deserialize<PythonTargetDto>(json);
            return new Core.Target.Python.PythonTarget
            {
                ScriptPath = dto.ScriptPath,
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
}
