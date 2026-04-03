using FluentMigrator.Runner;
using AutoTest.Migrations;
using AutoTest.Infrastructure;
using AutoTest.Application;
using CacheCommons;
using AutoTest.Execution.Http;
using AutoTest.Execution.Tcp;
using AutoTest.Assertions.Http;
using AutoTest.Webapi;
using FluentValidation.AspNetCore;
using FluentValidation;
using AutoTest.Webapi.FluentValidation;
using Microsoft.Data.Sqlite;
using System.Data;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddAutoTestMigrations(); // 注册迁移服务
builder.Services.AddMemoryCache();
builder.Services.AddCacheService();
builder.Services.AddHttpAssertion();
builder.Services.AddHttpExecution();
builder.Services.AddTcpExecution();
builder.Services.AddControllers();
builder.Services.AddValidatorsFromAssemblyContaining<AssertionDtoBaseValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMyLogging();
builder.Services.AddAutoTestApplication(); // 注册应用层服务
builder.Services.AddAutoTestInfrastructure(); // 注册基础设施服务
builder.Services.AddScoped<IDbConnection>(_ =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection")
             ?? "Data Source=AutoTestDb.sqlite";
    return new SqliteConnection(cs);
});
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });
}
else
{
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (corsOrigins.Length > 0)
    {
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(corsOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
    }
}
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
if (app.Environment.IsDevelopment() || app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 })
{
    app.UseCors();
}
app.UseAuthorization();
app.MapControllers();
// 在启动时自动迁移
using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp(); // 执行所有未执行的迁移
}
app.Run();

