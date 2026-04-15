using Auth;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace AutoTest.Migrations;

public static class MigrationServiceCollectionExtensions
{
    public static IServiceCollection AddAutoTestMigrations(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var cs = configuration.GetConnectionString("DefaultConnection")
                 ?? configuration["ConnectionStrings:DefaultConnection"]
                 ?? throw new InvalidOperationException("Missing connection string: DefaultConnection");
                 
        services.AddFluentMigratorCore()
            .ConfigureRunner(rb =>
            {
                var runner = string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase)
                    ? rb.AddSQLite()
                    : rb.AddSqlServer();

                runner.WithGlobalConnectionString(cs)
                      .ScanIn(typeof(MigrationServiceCollectionExtensions).Assembly) // 扫描本类库的所有迁移
                      .ScanIn(typeof(AddRbacExtensions).Assembly)
                      .For.Migrations();
            })
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        return services;
    }
}
