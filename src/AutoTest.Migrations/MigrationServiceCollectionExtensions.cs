using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Migrations;

public static class MigrationServiceCollectionExtensions
{
    public static IServiceCollection AddAutoTestMigrations(this IServiceCollection services)
    {
        services.AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddSQLite() // 指定 SQLite
                .WithGlobalConnectionString("Data Source=AutoTestDb.sqlite")
                .ScanIn(typeof(MigrationServiceCollectionExtensions).Assembly) // 扫描本类库的所有迁移
                    .For.Migrations()
            )
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        return services;
    }
}
