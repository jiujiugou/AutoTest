using Microsoft.Extensions.AI;

namespace AutoTest.Webapi.Ai;

public sealed class OpenAiChatService : IAiChatService
{
    private readonly IChatClient _client;

    public OpenAiChatService(IChatClient client)
    {
        _client = client;
    }

    public async Task<AiChatResult> ChatAsync(string userMessage, string? systemMessage, CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(systemMessage))
            messages.Add(new ChatMessage(ChatRole.System, systemMessage));

        messages.Add(new ChatMessage(ChatRole.User, userMessage));

        var response = await _client.GetResponseAsync(messages, cancellationToken: cancellationToken);
        var text = response.Messages.LastOrDefault()?.Text ?? string.Empty;
        return new AiChatResult(text);
    }
}
