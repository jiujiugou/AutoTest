namespace AutoTest.Webapi.Ai;

public interface IAiChatService
{
    Task<AiChatResult> ChatAsync(string userMessage, string? systemMessage, CancellationToken cancellationToken);
}

public sealed record AiChatResult(string Reply);

public sealed class MissingAiChatService : IAiChatService
{
    private readonly string _message;

    public MissingAiChatService(string message)
    {
        _message = message;
    }

    public Task<AiChatResult> ChatAsync(string userMessage, string? systemMessage, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(_message);
    }
}
