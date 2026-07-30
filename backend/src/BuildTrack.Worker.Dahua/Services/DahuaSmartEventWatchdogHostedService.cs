using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Worker.Dahua.Services;

public sealed class DahuaSmartEventWatchdogHostedService(
    IConfiguration configuration,
    IDahuaActiveRegisterSdk dahuaSdk,
    ILogger<DahuaSmartEventWatchdogHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = DahuaSmartEventWatchdogPolicy.FromConfiguration(configuration);
        logger.LogInformation(
            "Dahua Smart Event watchdog config. Enabled={Enabled}, StaleMinutes={StaleMinutes}, PeriodicResubscribeMinutes={PeriodicMinutes}, CooldownSeconds={CooldownSeconds}",
            options.Enabled,
            options.StaleThreshold.TotalMinutes,
            options.PeriodicResubscribeInterval.TotalMinutes,
            options.ResubscribeCooldown.TotalSeconds);

        if (!options.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var snapshots = await dahuaSdk.GetSmartEventSubscriptionsAsync(stoppingToken);
                foreach (var snapshot in snapshots)
                {
                    var decision = DahuaSmartEventWatchdogPolicy.Evaluate(snapshot, options, now);
                    if (decision.StaleSmartEventDetected)
                    {
                        logger.LogWarning(
                            "Smart event subscription stale detected. DeviceId={DeviceId}, RegisterDeviceId={RegisterDeviceId}, LastServiceCallbackAt={LastServiceCallbackAt}, LastSmartEventAt={LastSmartEventAt}, CooldownActive={CooldownActive}",
                            snapshot.DeviceId,
                            snapshot.RegisterDeviceId,
                            snapshot.LastServiceCallbackAt,
                            snapshot.LastSmartEventAt,
                            decision.CooldownActive);
                    }

                    if (!decision.ShouldResubscribe || decision.Reason is null)
                    {
                        continue;
                    }

                    var result = await dahuaSdk.ResubscribeSmartEventsAsync(snapshot.DeviceId, decision.Reason, stoppingToken);
                    if (result.Success)
                    {
                        logger.LogInformation(
                            "RealLoadPictureEx resubscribe success. DeviceId={DeviceId}, RegisterDeviceId={RegisterDeviceId}, Reason={Reason}, Generation={Generation}",
                            snapshot.DeviceId,
                            snapshot.RegisterDeviceId,
                            result.Reason,
                            result.SubscriptionGeneration);
                    }
                    else
                    {
                        logger.LogWarning(
                            "RealLoadPictureEx resubscribe failure. DeviceId={DeviceId}, RegisterDeviceId={RegisterDeviceId}, Reason={Reason}, Error={Error}",
                            snapshot.DeviceId,
                            snapshot.RegisterDeviceId,
                            result.Reason,
                            result.Error);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dahua Smart Event watchdog iteration failed. Worker continues running.");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
