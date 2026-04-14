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
        var cs = configuration.GetConnectionString("DefaultConnection");
                 
        services.AddFluentMigratorCore()
            .ConfigureRunner(rb =>
            {
                var runner =rb.AddSqlServer();

                runner.WithGlobalConnectionString(cs)
                      .ScanIn(typeof(MigrationServiceCollectionExtensions).Assembly) // 扫描本类库的所有迁移
                      .ScanIn(typeof(AddRbacExtensions).Assembly)
                      .For.Migrations();
            })
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        return services;
    }
}
