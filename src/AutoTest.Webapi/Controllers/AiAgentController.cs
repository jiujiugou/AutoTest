using AutoTest.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;

namespace AutoTest.Webapi.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize] // 如果你开发阶段想裸奔，可以把这个注释掉
public class AiAgentController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IMonitorService _monitorService;
    private readonly IWorkflowScheduler _workflowScheduler;
    private readonly ILogger<AiAgentController> _logger;

    public AiAgentController(
        IConfiguration configuration,
        IMonitorService monitorService,
        IWorkflowScheduler workflowScheduler,
        ILogger<AiAgentController> logger)
    {
        _configuration = configuration;
        _monitorService = monitorService;
        _workflowScheduler = workflowScheduler;
        _logger = logger;
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        // 如果需要多轮对话，前端可以把历史记录发过来，这里为了MVP从简，只接收最新的 message
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Message)) return BadRequest("消息不能为空");

        try
        {
            var endpoint = _configuration["AI:Endpoint"];
            var apiKey = _configuration["AI:ApiKey"];
            var modelId = _configuration["AI:ModelId"];

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_DOUBAO_API_KEY")
                return Ok(new { text = "提示：请在 appsettings.json 中配置 AI 节点（填写豆包的 ApiKey 和 ModelId）。" });
            if (string.IsNullOrWhiteSpace(endpoint))
                return Ok(new { text = "提示：请在 appsettings.json 中配置 AI:Endpoint（豆包 OpenAI 兼容接口地址）。" });
            if (string.IsNullOrWhiteSpace(modelId))
                return Ok(new { text = "提示：请在 appsettings.json 中配置 AI:ModelId（豆包模型ID）。" });

            // 1. 初始化 Kernel Builder
            var builder = Kernel.CreateBuilder();

            // 2. 豆包兼容 OpenAI 接口规范，所以我们可以直接使用 AddOpenAIChatCompletion
            // 但需要把 HttpClient 指向豆包的 API (https://ark.cn-beijing.volces.com/api/v3)
            // 注意：AddOpenAIChatCompletion 默认会访问 api.openai.com，我们用 DelegatingHandler 将请求重写到豆包域名
            builder.AddOpenAIChatCompletion(
                modelId: modelId,
                apiKey: apiKey,
                httpClient: new HttpClient(new DoubaoDelegatingHandler(endpoint))
            );

            var kernel = builder.Build();

            // 3. 注册我们写好的 Plugin 工具
            kernel.Plugins.AddFromObject(new AI.AutoTestPlugin(_monitorService, _workflowScheduler), "AutoTestOps");

            // 4. 获取聊天服务
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            // 5. 设置系统提示词（System Prompt），定义 Agent 的人设
            var chatHistory = new ChatHistory("你是一个名为 AutoTest Pro 的智能监控运维助手。你的任务是帮助用户管理监控任务、查看运行状态并执行操作。请使用友好的语气回答，在回答前一定要尝试调用可用工具获取真实数据，不要胡编乱造。");
            chatHistory.AddUserMessage(req.Message);

            // 6. 开启自动工具调用 (Function Calling)
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0.2 // 让回答偏向严谨事实
            };

            // 7. 发送给大模型并获取结果
            var result = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: executionSettings,
                kernel: kernel);

            return Ok(new { text = result.Content });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Agent 调用失败");
            return StatusCode(500, new { text = "AI 服务调用失败: " + ex.Message });
        }
    }
}

/// <summary>
/// 这是一个用于重定向 OpenAI SDK 请求到豆包 API 的 Handler
/// </summary>
public class DoubaoDelegatingHandler : HttpClientHandler
{
    private readonly string _doubaoEndpoint;

    public DoubaoDelegatingHandler(string doubaoEndpoint)
    {
        _doubaoEndpoint = NormalizeBaseEndpoint(doubaoEndpoint);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri != null && request.RequestUri.Host.Contains("api.openai.com"))
        {
            var pathAndQuery = request.RequestUri.PathAndQuery;

            if (pathAndQuery.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
                pathAndQuery = pathAndQuery.Substring(3);

            request.RequestUri = new Uri($"{_doubaoEndpoint}{pathAndQuery}");
        }
        return base.SendAsync(request, cancellationToken);
    }

    private static string NormalizeBaseEndpoint(string endpoint)
    {
        var e = (endpoint ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(e)) return "";

        var suffixes = new[]
        {
            "/chat/completions",
            "/v1/chat/completions",
            "/api/v3/chat/completions"
        };

        foreach (var s in suffixes)
        {
            if (e.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                e = e.Substring(0, e.Length - s.Length);
        }

        if (!e.EndsWith("/api/v3", StringComparison.OrdinalIgnoreCase))
            e += "/api/v3";

        return e;
    }
}
