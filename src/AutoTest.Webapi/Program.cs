using AutoTest.Application;
using AutoTest.Assertion;
using AutoTest.Assertion.Db;
using AutoTest.Assertions;
using AutoTest.Assertions.Http;
using AutoTest.Execution.Db;
using AutoTest.Execution.Http;
using AutoTest.Execution.Python;
using AutoTest.Execution.Tcp;
using AutoTest.Infrastructure;
using Auth;
using AutoTest.Migrations;
using AutoTest.Webapi;
using AutoTest.Webapi.FluentValidation;
using AutoTest.Webapi.JWT;
using CacheCommons;
using FluentMigrator.Runner;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.IdentityModel.Tokens;
using OpenAI.Chat;
using System.Data;
using System.Text;
using AutoTest.Infrastructure.Hubs;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddAutoTestMigrations(builder.Configuration); // 注册迁移服务
builder.Services.AddMemoryCache();
builder.Services.AddCacheService();
builder.Services.AddHttpAssertion();
builder.Services.AddHttpExecution();
builder.Services.AddTcpExecution();
builder.Services.AddPythonExecution();
builder.Services.AddExecutionDb();
builder.Services.AddDbAssertion();
builder.Services.AddSingleton(typeof(AssertionOperator), AssertionOperator.Equal);
builder.Services.AddOperatorAssertion();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, ClaimUserIdProvider>();
builder.Services.AddValidatorsFromAssemblyContaining<AssertionDtoBaseValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMyLogging();
builder.Services.AddAutoTestApplication(); // 注册应用层服务
builder.Services.AddAutoTestInfrastructure(builder.Configuration); // 注册基础设施服务
builder.Services.AddHostedService<DatabaseWarmupHostedService>();


builder.Services.AddScoped<IDbConnection>(_ =>
{
    var provider = builder.Configuration["Database:Provider"] ?? "SqlServer";
    var cs = builder.Configuration.GetConnectionString("DefaultConnection")
             ?? "Server=.;Database=AutoTestDb;Trusted_Connection=True;TrustServerCertificate=True;";
    if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        return new SqliteConnection(cs);
    return new SqlConnection(cs);
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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    var signingKey = builder.Configuration["Jwt:SigningKey"]
                     ?? builder.Configuration["Jwt:Key"]
                     ?? "your-secret-key-123456";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(signingKey))
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/monitor"))
                context.Token = accessToken;
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddDapperPermissionAuthorization();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<JwtTokenIssuer>();
builder.Services.AddSingleton<ITokenIssuer>(sp => sp.GetRequiredService<JwtTokenIssuer>());
builder.Services.AddDapperAuth();
builder.Services.AddRbac();
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MonitorHub>("/hubs/monitor");
app.MapHub<LogHub>("/hubs/logs");
// 在启动时自动迁移
using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp(); // 执行所有未执行的迁移
}
app.Run();

