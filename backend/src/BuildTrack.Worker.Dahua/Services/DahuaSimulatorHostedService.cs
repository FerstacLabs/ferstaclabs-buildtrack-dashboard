using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Worker.Dahua.Services;

public sealed class DahuaSimulatorHostedService(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<DahuaSimulatorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bool.TryParse(configuration["DAHUA_SIMULATOR_ENABLED"], out var enabled) || !enabled)
        {
            logger.LogInformation("Dahua simulator worker is disabled. Set DAHUA_SIMULATOR_ENABLED=true to generate test events.");
            return;
        }

        logger.LogWarning("Dahua simulator worker is enabled. Fake access events will be inserted for test devices.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateOneEventAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dahua simulator event generation failed");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task GenerateOneEventAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BuildTrackDbContext>();
        var ingestion = scope.ServiceProvider.GetRequiredService<IAttendanceIngestionService>();

        var device = await db.Devices
            .Where(x => x.Mode == DeviceMode.Simulator || x.Mode == DeviceMode.ActiveRegister)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (device is null) return;

        var worker = await db.Workers.Where(x => x.SiteId == device.SiteId && x.Status == WorkerStatus.Active).OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var recNo = now.ToUnixTimeMilliseconds();
        var record = new DahuaAccessRecord
        {
            RecNo = recNo,
            CreateTime = now,
            UserId = worker?.ExternalWorkerCode ?? "1",
            CardName = worker?.FullName ?? "Simulator Worker",
            StatusRaw = "1",
            MethodRaw = "15",
            Type = "Entry",
            RawFields = new Dictionary<string, string?>
            {
                ["RecNo"] = recNo.ToString(),
                ["CreateTime"] = now.ToString("O"),
                ["UserID"] = worker?.ExternalWorkerCode ?? "1",
                ["CardName"] = worker?.FullName ?? "Simulator Worker",
                ["Status"] = "1",
                ["Method"] = "15",
                ["Type"] = "Entry",
            },
        };
        await ingestion.IngestDahuaRecordAsync(device.Id, record, "simulator-worker", device.RegisterPort, cancellationToken);
    }
}
