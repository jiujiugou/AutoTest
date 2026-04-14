using Microsoft.Data.SqlClient;

namespace AutoTest.Tests;

public sealed class SqlServerTestDatabase : IAsyncDisposable
{
    public SqlServerTestDatabase(string databaseName, string connectionString)
    {
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string DatabaseName { get; }
    public string ConnectionString { get; }

    public static async Task<SqlServerTestDatabase> CreateAsync()
    {
        var dbName = $"AutoTestTest_{Guid.NewGuid():N}";
        var master = "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

        await using (var conn = new SqlConnection(master))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{dbName}]";
            await cmd.ExecuteNonQueryAsync();
        }

        var cs = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        return new SqlServerTestDatabase(dbName, cs);
    }

    public async ValueTask DisposeAsync()
    {
        var master = "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        await using var conn = new SqlConnection(master);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
                          ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                          DROP DATABASE [{DatabaseName}];
                          """;
        await cmd.ExecuteNonQueryAsync();
    }
}
