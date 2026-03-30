using FluentMigrator.Runner;
using AutoTest.Migrations;
using AutoTest.Infrastructure;
using AutoTest.Application;
using CacheCommons;
using AutoTest.Execution.Http;
using AutoTest.Assertions.Http;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddAutoTestMigrations(); // 注册迁移服务
builder.Services.AddCacheService();
builder.Services.AddHttpAssertion();
builder.Services.AddHttpExecution();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAutoTestApplication(); // 注册应用层服务
builder.Services.AddAutoTestInfrastructure(); // 注册基础设施服务
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();
// 在启动时自动迁移
using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp(); // 执行所有未执行的迁移
}
app.Run();

