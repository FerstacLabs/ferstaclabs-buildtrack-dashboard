using BuildTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Worker.Dahua.Services;

public sealed class WorkerHealthHostedService(IServiceScopeFactory scopes, ILogger<WorkerHealthHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BuildTrackDbContext>();
                if (await db.Database.CanConnectAsync(stoppingToken))
                    await File.WriteAllTextAsync("/tmp/buildtrack-worker-health", DateTimeOffset.UtcNow.ToString("O"), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Worker database health probe failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
