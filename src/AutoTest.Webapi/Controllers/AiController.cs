using AutoTest.Webapi.Ai;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Webapi.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IAiChatService _chat;

    public AiController(IAiChatService chat)
    {
        _chat = chat;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<AiChatResponse>> Chat([FromBody] AiChatRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("message is required");

        try
        {
            var r = await _chat.ChatAsync(request.Message, request.System, cancellationToken);
            return Ok(new AiChatResponse(r.Reply));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}

public sealed record AiChatRequest(string Message, string? System = null);

public sealed record AiChatResponse(string Reply);

