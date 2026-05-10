using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoTest.Core.Target.Db;

public class DbTarget : MonitorTarget
{
    public string ConnectionString { get; set; } = null!;
    public string Sql { get; set; } = null!;
    public string DbType { get; set; } = null!;
    public int Rows { get; set; }
    public int AffectedRows { get; set; }
    public int EffectedRows { get => AffectedRows; set => AffectedRows = value; }
    public SqlCommandType CommandType { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableRetry { get; set; }
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 500;

    public DbTarget() { }

    [JsonConstructor]
    public DbTarget(
        string connectionString,
        string sql,
        string dbType,
        int rows = 0,
        int affectedRows = 0,
        SqlCommandType commandType = SqlCommandType.Query,
        int timeoutSeconds = 30,
        bool enableRetry = false,
        int retryCount = 2,
        int retryDelayMs = 500)
    {
        ConnectionString = connectionString;
        Sql = sql;
        DbType = dbType;
        Rows = rows;
        AffectedRows = affectedRows;
        CommandType = commandType;
        TimeoutSeconds = timeoutSeconds;
        EnableRetry = enableRetry;
        RetryCount = retryCount;
        RetryDelayMs = retryDelayMs;
    }

    public override string Type => "DB";

    public override string ToJson() => JsonSerializer.Serialize(this);
}

public enum SqlCommandType
{
    Query,
    NonQuery,
    Scalar
}
