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
using Microsoft.AspNetCore.HttpOverrides;
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
builder.Services.AddMyLogging(o =>
{
    o.ElasticNodes = builder.Configuration["Logging:ElasticNodes"] ?? "http://localhost:9200";
    o.EnableElasticsearch = builder.Configuration.GetValue("Logging:EnableElasticsearch", true);
});
builder.Services.AddAutoTestApplication(); // 注册应用层服务
builder.Services.AddAutoTestInfrastructure(builder.Configuration); // 注册基础设施服务
builder.Services.AddHostedService<DatabaseWarmupHostedService>();

builder.Services.AddScoped<IDbConnection>(_ =>
{
    var provider = builder.Configuration["Database:Provider"] ?? "SqlServer";
    var cs = builder.Configuration.GetConnectionString("DefaultConnection")
             ?? "Server=.;Database=AutoTestDb;Trusted_Connection=True;TrustServerCertificate=True;";
    return new SqlConnection(cs);
});

// ✅ 修改 1：修复跨域配置，支持 SignalR 的 Credentials 要求
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.SetIsOriginAllowed(_ => true) // 👈 允许任何来源，但不用通配符
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials(); // 👈 必须允许凭据，SignalR 才能工作
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
                    .AllowAnyHeader()
                    .AllowCredentials(); // 👈 生产环境也必须允许凭据
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            // 合并判断逻辑，更清晰
            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/hubs/logs") || path.StartsWithSegments("/hubs/monitor")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddDapperPermissionAuthorization();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<JwtTokenIssuer>();
builder.Services.AddSingleton<ITokenIssuer>(sp => sp.GetRequiredService<JwtTokenIssuer>());
builder.Services.AddDapperAuth();
builder.Services.AddRbac();

var app = builder.Build();

SignalRLogSink.ServiceProvider = app.Services;

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();

if (app.Configuration.GetValue("App:EnableHttpsRedirection", true))
{
    app.UseHttpsRedirection();
}
app.UseDefaultFiles();
app.UseStaticFiles();

// 使用端点路由（虽然 .NET 6+ 默认隐式使用，但为了中间件顺序清晰，显式调用更好）
app.UseRouting();

// ✅ 修改 2：UseWebSockets 必须放在鉴权和路由之前/之间，绝不能放在最后！
app.UseWebSockets();

if (app.Environment.IsDevelopment() || app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 })
{
    app.UseCors();
}

app.UseAuthentication();
app.UseAuthorization();

// 使用 UseEndpoints 映射路由，这是更标准的写法，配合 UseRouting 使用
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<MonitorHub>("/hubs/monitor");
    endpoints.MapHub<LogHub>("/hubs/logs");
});

// 在启动时自动迁移
using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp(); // 执行所有未执行的迁移
}

app.Run();
