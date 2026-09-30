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
RUN dotnet publish backend/src/BuildTrack.Worker.Dahua/BuildTrack.Worker.Dahua.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
ENV LD_LIBRARY_PATH=/app/vendor/dahua-netsdk/linux-x64
COPY --from=build /app/publish .
RUN mkdir -p /app/data /app/vendor/dahua-netsdk/linux-x64 && chown app:app /app/data
USER app
HEALTHCHECK --interval=30s --timeout=5s --start-period=180s --retries=5 CMD test -f /tmp/buildtrack-worker-health && test "$(( $(date +%s) - $(stat -c %Y /tmp/buildtrack-worker-health) ))" -lt 120
EXPOSE 9500/tcp
EXPOSE 7000/tcp
ENTRYPOINT ["dotnet", "BuildTrack.Worker.Dahua.dll"]

