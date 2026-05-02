using AutoTest.Application;
using AutoTest.Core.AI;
using Microsoft.SemanticKernel;

namespace AutoTest.AI
{
    public static class KernelFactory
    {
        public static Kernel Create(ILogService logService, AiOptions options)
        {
            var builder = Kernel.CreateBuilder();

            if (!string.IsNullOrWhiteSpace(options.Endpoint))
            {
                builder.AddOpenAIChatCompletion(
                    modelId: options.ModelId,
                    apiKey: options.ApiKey,
                    endpoint: new Uri(options.Endpoint)
                );
            }
            else
            {
                builder.AddOpenAIChatCompletion(
                    modelId: options.ModelId,
                    apiKey: options.ApiKey
                );
            }

            return builder.Build();
        }
    }
}
