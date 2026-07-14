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
ENV LD_LIBRARY_PATH=/app/vendor/dahua-netsdk/linux-x64:$LD_LIBRARY_PATH
COPY --from=build /app/publish .
COPY backend/vendor/dahua-netsdk ./vendor/dahua-netsdk
EXPOSE 9500/tcp
EXPOSE 7000/tcp
ENTRYPOINT ["dotnet", "BuildTrack.Worker.Dahua.dll"]

