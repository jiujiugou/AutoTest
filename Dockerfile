FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY src/AutoTest.Webapi/AutoTest.Webapi.csproj src/AutoTest.Webapi/
COPY src/AutoTest.AI/AutoTest.AI.csproj src/AutoTest.AI/
COPY src/AutoTest.Application/AutoTest.Application.csproj src/AutoTest.Application/
COPY src/AutoTest.Core/AutoTest.Core.csproj src/AutoTest.Core/
COPY src/AutoTest.Infrastructure/AutoTest.Infrastructure.csproj src/AutoTest.Infrastructure/
COPY src/AutoTest.Execution/AutoTest.Execution.csproj src/AutoTest.Execution/
COPY src/AutoTest.Assertions/AutoTest.Assertions.csproj src/AutoTest.Assertions/
COPY src/AutoTest.Migrations/AutoTest.Migrations.csproj src/AutoTest.Migrations/
COPY src/AutoTest.Dsl/AutoTest.Dsl.csproj src/AutoTest.Dsl/
COPY src/AutoTest.Orchestration/AutoTest.Orchestration.csproj src/AutoTest.Orchestration/
COPY src/Auth/Auth.csproj src/Auth/
COPY src/common/CacheCommons/CacheCommons.csproj src/common/CacheCommons/
COPY src/common/LockCommons/LockCommons.csproj src/common/LockCommons/

RUN dotnet restore src/AutoTest.Webapi/AutoTest.Webapi.csproj

COPY . .

WORKDIR /app/src/AutoTest.Webapi
RUN dotnet publish AutoTest.Webapi.csproj -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./

ENV ASPNETCORE_URLS=http://0.0.0.0:80
EXPOSE 80
ENTRYPOINT ["dotnet", "AutoTest.Webapi.dll"]
