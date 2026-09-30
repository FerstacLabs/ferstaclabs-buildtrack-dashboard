FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/BuildTrack.sln backend/BuildTrack.sln
COPY backend/src/BuildTrack.Domain/BuildTrack.Domain.csproj backend/src/BuildTrack.Domain/
COPY backend/src/BuildTrack.Infrastructure/BuildTrack.Infrastructure.csproj backend/src/BuildTrack.Infrastructure/
COPY backend/src/BuildTrack.Api/BuildTrack.Api.csproj backend/src/BuildTrack.Api/
COPY backend/src/BuildTrack.Worker.Dahua/BuildTrack.Worker.Dahua.csproj backend/src/BuildTrack.Worker.Dahua/
COPY backend/tests/BuildTrack.Tests/BuildTrack.Tests.csproj backend/tests/BuildTrack.Tests/
RUN dotnet restore backend/BuildTrack.sln
COPY backend/ backend/
RUN dotnet publish backend/src/BuildTrack.Api/BuildTrack.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /app/data && chown app:app /app/data
USER app
HEALTHCHECK --interval=30s --timeout=5s --start-period=180s --retries=5 CMD curl --fail --silent http://127.0.0.1:8080/api/health > /dev/null || exit 1
EXPOSE 8080
ENTRYPOINT ["dotnet", "BuildTrack.Api.dll"]
