namespace AutoTest.Application.Dto;

public class DbTargetDto
{
    public string ConnectionString { get; set; } = null!;
    public string Sql { get; set; } = null!;
    public string DbType { get; set; } = null!;
    public int TimeoutSeconds { get; set; } = 30;
    public string CommandType { get; set; } = "Query";
    public bool EnableRetry { get; set; }
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 500;
}
