using FluentMigrator.Runner;
using AutoTest.Migrations;
using AutoTest.Infrastructure;
using AutoTest.Application;
using AutoTest.Assertion;
using CacheCommons;
using AutoTest.Execution.Http;
using AutoTest.Execution.Tcp;
using AutoTest.Execution.Db;
using AutoTest.Assertions.Http;
using AutoTest.Assertion.Db;
using AutoTest.Assertions;
using AutoTest.Webapi;
using AutoTest.Webapi.Ai;
using FluentValidation.AspNetCore;
using FluentValidation;
using AutoTest.Webapi.FluentValidation;
using Microsoft.Data.Sqlite;
using System.Data;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddAutoTestMigrations(builder.Configuration); // 注册迁移服务
builder.Services.AddMemoryCache();
builder.Services.AddCacheService();
builder.Services.AddHttpAssertion();
builder.Services.AddHttpExecution();
builder.Services.AddTcpExecution();
builder.Services.AddExecutionDb();
builder.Services.AddDbAssertion();
builder.Services.AddSingleton(typeof(AssertionOperator), AssertionOperator.Equal);
builder.Services.AddOperatorAssertion();
builder.Services.AddControllers();
builder.Services.AddValidatorsFromAssemblyContaining<AssertionDtoBaseValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMyLogging();
builder.Services.AddAutoTestApplication(); // 注册应用层服务
builder.Services.AddAutoTestInfrastructure(builder.Configuration); // 注册基础设施服务

var openAiApiKey = builder.Configuration["AI:OpenAI:ApiKey"] ?? builder.Configuration["OPENAI_API_KEY"];
if (!string.IsNullOrWhiteSpace(openAiApiKey))
{
    var openAiModel = builder.Configuration["AI:OpenAI:Model"] ?? "gpt-4o-mini";
    builder.Services.AddChatClient(_ => new ChatClient(openAiModel, openAiApiKey).AsIChatClient());
    builder.Services.AddSingleton<IAiChatService, OpenAiChatService>();
}
else
{
    builder.Services.AddSingleton<IAiChatService>(new MissingAiChatService(
        "OpenAI API key is missing. Set AI:OpenAI:ApiKey or OPENAI_API_KEY."
    ));
}
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
app.UseDefaultFiles();
app.UseStaticFiles();
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

