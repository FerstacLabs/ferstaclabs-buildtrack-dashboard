using Microsoft.Extensions.Configuration;

namespace BuildTrack.Infrastructure.Dahua;

public sealed record DahuaSmartEventSubscriptionSnapshot(
    Guid DeviceId,
    string? RegisterDeviceId,
    long? LoginHandle,
    long? SmartEventAttachHandle,
    string? RemoteIp,
    int? RemotePort,
    DateTimeOffset? SubscribedAt,
    DateTimeOffset? LastSmartEventAt,
    DateTimeOffset? LastServiceCallbackAt,
    int SubscriptionGeneration,
    DateTimeOffset? LastResubscribeAt,
    string? LastResubscribeReason,
    bool? LastResubscribeSuccess,
    string? LastResubscribeError,
    bool SmartEventEnabled,
    bool? SmartEventSubscriptionSuccess);

public sealed record DahuaSmartEventResubscribeResult(
    Guid DeviceId,
    bool Success,
    string Reason,
    string? Error,
    int SubscriptionGeneration,
    DateTimeOffset AttemptedAt);

public sealed record DahuaSmartEventWatchdogOptions(
    bool Enabled,
    TimeSpan StaleThreshold,
    TimeSpan PeriodicResubscribeInterval,
    TimeSpan ResubscribeCooldown);

public sealed record DahuaSmartEventWatchdogDecision(
    bool ShouldResubscribe,
    string? Reason,
    bool StaleSmartEventDetected,
    bool CooldownActive);

public static class DahuaSmartEventWatchdogPolicy
{
    public static DahuaSmartEventWatchdogOptions FromConfiguration(IConfiguration configuration) =>
        new(
            IsEnabled(configuration["DAHUA_SMART_EVENT_WATCHDOG_ENABLED"], defaultValue: true),
            TimeSpan.FromMinutes(ParsePositiveInt(configuration["DAHUA_SMART_EVENT_STALE_MINUTES"], 10)),
            TimeSpan.FromMinutes(ParsePositiveInt(configuration["DAHUA_SMART_EVENT_PERIODIC_RESUBSCRIBE_MINUTES"], 360)),
            TimeSpan.FromSeconds(ParsePositiveInt(configuration["DAHUA_SMART_EVENT_RESUBSCRIBE_COOLDOWN_SECONDS"], 60)));

    public static DahuaSmartEventWatchdogDecision Evaluate(
        DahuaSmartEventSubscriptionSnapshot snapshot,
        DahuaSmartEventWatchdogOptions options,
        DateTimeOffset now)
    {
        if (!options.Enabled)
        {
            return new DahuaSmartEventWatchdogDecision(false, null, false, false);
        }

        if (!snapshot.SmartEventEnabled || snapshot.SmartEventSubscriptionSuccess != true)
        {
            return new DahuaSmartEventWatchdogDecision(false, null, false, false);
        }

        var cooldownActive = snapshot.LastResubscribeAt is not null
                             && now - snapshot.LastResubscribeAt.Value < options.ResubscribeCooldown;

        var serviceCallbackRecent = snapshot.LastServiceCallbackAt is not null
                                    && now - snapshot.LastServiceCallbackAt.Value <= options.StaleThreshold;
        var smartEventStale = serviceCallbackRecent
                              && (snapshot.LastSmartEventAt is null
                                  || now - snapshot.LastSmartEventAt.Value > options.StaleThreshold);

        if (cooldownActive)
        {
            return new DahuaSmartEventWatchdogDecision(false, null, smartEventStale, true);
        }

        if (smartEventStale)
        {
            return new DahuaSmartEventWatchdogDecision(true, "StaleSmartEventSubscription", true, false);
        }

        var periodicDue = snapshot.SubscribedAt is not null
                          && now - snapshot.SubscribedAt.Value >= options.PeriodicResubscribeInterval;

        return periodicDue
            ? new DahuaSmartEventWatchdogDecision(true, "PeriodicSmartEventResubscribe", false, false)
            : new DahuaSmartEventWatchdogDecision(false, null, false, false);
    }

    private static bool IsEnabled(string? value, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParsePositiveInt(string? value, int defaultValue) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
}

public static class DahuaSmartEventSubscriptionEndpoint
{
    public static bool HasChanged(string? previousIp, int? previousPort, string? currentIp, int? currentPort, bool hasActiveSession) =>
        hasActiveSession
        && (!string.Equals(previousIp, currentIp, StringComparison.OrdinalIgnoreCase)
            || previousPort != currentPort);
}
