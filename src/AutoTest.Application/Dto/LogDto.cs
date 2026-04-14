namespace AutoTest.Application.Dto;

public sealed record LogQueryDto(
    int Take = 200,
    string? Level = null,
    string? Module = null,
    string? Keyword = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Before = null);

public sealed record LogItemDto(
    string Cursor,
    string Timestamp,
    string Level,
    string Module,
    string Message);

public sealed record LogPageDto(
    IReadOnlyList<LogItemDto> Items,
    string? Next,
    bool HasMore);

