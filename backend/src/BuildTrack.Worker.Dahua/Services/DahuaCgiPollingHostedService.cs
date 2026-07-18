using System.Net;
using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Worker.Dahua.Services;

public sealed class DahuaCgiPollingHostedService(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<DahuaCgiPollingHostedService> logger) : BackgroundService
{
    private const string Source = "dahua_cgi_polling";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled(configuration["DAHUA_CGI_POLLING_ENABLED"]))
        {
            logger.LogInformation("Dahua CGI polling worker is disabled. This is a local/demo fallback only; set DAHUA_CGI_POLLING_ENABLED=true to enable it.");
            return;
        }

        var host = configuration["DAHUA_CGI_HOST"] ?? "192.168.31.174";
        var username = configuration["DAHUA_CGI_USERNAME"] ?? "admin";
        var password = configuration["DAHUA_CGI_PASSWORD"] ?? string.Empty;
        var intervalSeconds = ParsePositiveInt(configuration["DAHUA_CGI_POLL_INTERVAL_SECONDS"], 3);
        var debounceSeconds = ParsePositiveInt(configuration["DAHUA_CGI_DEBOUNCE_SECONDS"], 60);
        var unknownFaceDebounceSeconds = ParsePositiveInt(configuration["DAHUA_UNKNOWN_FACE_DEBOUNCE_SECONDS"], 30);
        var fetchSettings = DahuaCgiPollingPlanner.CreateSettings(
            configuration["DAHUA_CGI_INITIAL_FETCH_COUNT"],
            configuration["DAHUA_CGI_MAX_FETCH_COUNT"],
            configuration["DAHUA_CGI_FETCH_GROWTH_FACTOR"],
            configuration["DAHUA_CGI_FETCH_LOOKAHEAD"]);
        var deviceTimeZone = ResolveTimeZone(configuration["DAHUA_CGI_DEVICE_TIMEZONE"] ?? "Asia/Baku");
        var attendanceTimeZone = ResolveTimeZone(configuration["DAHUA_ATTENDANCE_TIMEZONE"] ?? "Asia/Baku");
        var minCheckoutGap = TimeSpan.FromMinutes(ParsePositiveInt(configuration["DAHUA_ATTENDANCE_MIN_CHECKOUT_AFTER_MINUTES"], 15));
        logger.LogWarning("Dahua CGI polling started for {Host}. Local/demo fallback only; not final cloud Active Register architecture.", host);
        logger.LogInformation("Dahua CGI polling config: host={Host}, interval={IntervalSeconds}, initialFetchCount={InitialFetchCount}, maxFetchCount={MaxFetchCount}, growthFactor={GrowthFactor}, fetchLookahead={FetchLookahead}, debounceSeconds={DebounceSeconds}, unknownFaceDebounceSeconds={UnknownFaceDebounceSeconds}, deviceTimezone={DeviceTimeZone}", host, intervalSeconds, fetchSettings.InitialFetchCount, fetchSettings.MaxFetchCount, fetchSettings.GrowthFactor, fetchSettings.FetchLookahead, debounceSeconds, unknownFaceDebounceSeconds, deviceTimeZone.Id);

        using var httpClient = CreateHttpClient(username, password);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(httpClient, host, fetchSettings, debounceSeconds, unknownFaceDebounceSeconds, deviceTimeZone, attendanceTimeZone, minCheckoutGap, stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dahua CGI polling cycle failed");
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(intervalSeconds, 3)), stoppingToken);
            }
        }
    }

    private async Task PollOnceAsync(HttpClient httpClient, string host, DahuaCgiFetchSettings fetchSettings, int debounceSeconds, int unknownFaceDebounceSeconds, TimeZoneInfo deviceTimeZone, TimeZoneInfo attendanceTimeZone, TimeSpan minCheckoutGap, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BuildTrackDbContext>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IDahuaAccessRecordIngestionPipeline>();

        var device = await ResolveDeviceAsync(db, cancellationToken);
        if (device is null)
        {
            logger.LogWarning("Dahua CGI polling skipped because no device exists. Create a Dahua device first.");
            return;
        }

        if (device.LastRecNo is > DahuaCgiCursorPolicy.PollutedRecNoThreshold)
        {
            logger.LogWarning("Dahua CGI polling legacy LastRecNo was polluted by LastRecNo={PollutedLastRecNo}. Resetting legacy cursor so real CGI records can be processed.", device.LastRecNo);
            device.LastRecNo = DahuaCgiCursorPolicy.IsSafeCgiRecNo(device.CgiLastRecNo) ? device.CgiLastRecNo : null;
            device.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        var cursor = DahuaCgiCursorPolicy.Resolve(device.CgiLastRecNo, device.LastRecNo);
        if (cursor.WasPolluted)
        {
            logger.LogWarning("Dahua CGI polling cursor was polluted by {SourceField}={PollutedLastRecNo}. Ignoring/resetting cursor so real CGI records can be processed.", cursor.SourceField, cursor.SourceField == "CgiLastRecNo" ? device.CgiLastRecNo : device.LastRecNo);
            if (cursor.SourceField == "CgiLastRecNo") device.CgiLastRecNo = null;
            if (cursor.SourceField == "LastRecNo") device.LastRecNo = null;
            device.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        var lastRecNo = cursor.LastRecNo;
        var fetchResult = await FetchRecordsAdaptiveAsync(httpClient, host, lastRecNo, fetchSettings, deviceTimeZone, cancellationToken);
        var records = fetchResult.Records;
        var processableRecords = DahuaCgiPollingPlanner.SelectProcessableRecords(records, lastRecNo).ToList();

        var inserted = 0;
        long maxProcessedRecNo = lastRecNo;
        var debounceWindow = TimeSpan.FromSeconds(debounceSeconds);
        var debounced = 0;
        var securityCreated = 0;
        var securityDebounced = 0;
        foreach (var record in processableRecords)
        {
            if (DahuaUnknownFacePolicy.IsUnknownFace(record))
            {
                await pipeline.IngestAsync(device.Id, record, DahuaEventSource.CgiPolling, cancellationToken);
                securityCreated++;
                if (record.RecNo is not null && record.RecNo > maxProcessedRecNo) maxProcessedRecNo = record.RecNo.Value;
                continue;
            }

            if (!DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(record))
            {
                if (record.RecNo is not null && record.RecNo > maxProcessedRecNo) maxProcessedRecNo = record.RecNo.Value;
                continue;
            }

            var hasDebouncedRawEvent = await HasDebouncedEventAsync(db, device.Id, record, debounceWindow, cancellationToken);
            var openSession = hasDebouncedRawEvent
                ? await FindOpenSessionAsync(db, device.Id, record, attendanceTimeZone, cancellationToken)
                : null;
            var debounceDecision = DahuaCgiDebouncePolicy.Decide(hasDebouncedRawEvent, openSession, record.CreateTime, minCheckoutGap);
            if (debounceDecision.ShouldSkip)
            {
                debounced++;
                logger.LogInformation(
                    "Skipped CGI event by debounce. Reason {Reason}. Device {DeviceId}, WorkerExternalId {WorkerExternalId}, Direction {Direction}, RecNo {RecNo}, EventTime {EventTime}, DebounceSeconds {DebounceSeconds}",
                    debounceDecision.Reason,
                    device.Id,
                    record.UserId,
                    record.NormalizedDirection,
                    record.RecNo,
                    record.CreateTime,
                    debounceSeconds);
                if (record.RecNo is not null && record.RecNo > maxProcessedRecNo) maxProcessedRecNo = record.RecNo.Value;
                continue;
            }

            if (hasDebouncedRawEvent)
            {
                logger.LogInformation(
                    "Bypassed CGI debounce because event can change attendance session state. Reason {Reason}. Device {DeviceId}, WorkerExternalId {WorkerExternalId}, RecNo {RecNo}, EventTime {EventTime}",
                    debounceDecision.Reason,
                    device.Id,
                    record.UserId,
                    record.RecNo,
                    record.CreateTime);
            }
            await pipeline.IngestAsync(device.Id, record, DahuaEventSource.CgiPolling, cancellationToken);
            inserted++;
            logger.LogInformation("Submitted CGI event to shared ingestion pipeline. Device {DeviceId}, WorkerExternalId {WorkerExternalId}, Direction {Direction}, RecNo {RecNo}, EventTime {EventTime}", device.Id, record.UserId, record.NormalizedDirection, record.RecNo, record.CreateTime);
            if (record.RecNo is not null && record.RecNo > maxProcessedRecNo) maxProcessedRecNo = record.RecNo.Value;
        }

        if (maxProcessedRecNo > lastRecNo)
        {
            device.CgiLastRecNo = maxProcessedRecNo;
            device.LastRecNo = maxProcessedRecNo;
            device.LastKnownIp = host;
            device.LastSeenAt = DateTimeOffset.UtcNow;
            device.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Dahua CGI LastRecNo updated. Device {DeviceId}, LastRecNo {LastRecNo}", device.Id, maxProcessedRecNo);
        }

        logger.LogInformation("Dahua CGI poll summary. Device {DeviceId}, CgiLastRecNo {CgiLastRecNo}, TargetFetchCount {TargetFetchCount}, CurrentFetchCount {CurrentFetchCount}, Fetched {FetchedCount}, MaxRecNoInResponse {MaxRecNoInResponse}, AdaptiveRetryHappened {AdaptiveRetryHappened}, CandidateCount {CandidateCount}, Inserted {InsertedCount}, Debounced {DebouncedCount}, SecurityCreated {SecurityCreated}, SecurityDebounced {SecurityDebounced}", device.Id, lastRecNo, fetchResult.TargetFetchCount, fetchResult.FinalFetchCount, records.Count, fetchResult.MaxRecNoInResponse, fetchResult.AdaptiveRetryHappened, processableRecords.Count, inserted, debounced, securityCreated, securityDebounced);
    }

    private async Task<DahuaCgiAdaptiveFetchResult> FetchRecordsAdaptiveAsync(HttpClient httpClient, string host, long lastRecNo, DahuaCgiFetchSettings fetchSettings, TimeZoneInfo deviceTimeZone, CancellationToken cancellationToken)
    {
        var currentFetchCount = fetchSettings.InitialFetchCount;
        var adaptiveRetryHappened = false;

        while (true)
        {
            var uri = DahuaCgiPollingPlanner.BuildRecordFinderUri(host, currentFetchCount);
            var text = await httpClient.GetStringAsync(uri, cancellationToken);
            var records = DahuaCgiRecordParser.ParseKeyValueResponse(text, deviceTimeZone);
            var analysis = DahuaCgiPollingPlanner.AnalyzeFetch(records, lastRecNo, currentFetchCount, fetchSettings);
            logger.LogInformation(
                "Dahua CGI adaptive fetch attempt. CgiLastRecNo {CgiLastRecNo}, TargetFetchCount {TargetFetchCount}, CurrentFetchCount {CurrentFetchCount}, Fetched {FetchedCount}, MaxRecNoInResponse {MaxRecNoInResponse}, ShouldRetry {ShouldRetry}, NextFetchCount {NextFetchCount}",
                analysis.LastRecNo,
                analysis.TargetFetchCount,
                analysis.CurrentFetchCount,
                analysis.FetchedCount,
                analysis.MaxRecNoInResponse,
                analysis.ShouldRetry,
                analysis.NextFetchCount);

            if (analysis.ShouldRetry)
            {
                adaptiveRetryHappened = true;
                logger.LogInformation("Dahua CGI adaptive retry. CgiLastRecNo {CgiLastRecNo}, TargetFetchCount {TargetFetchCount}, CurrentFetchCount {CurrentFetchCount}, NextFetchCount {NextFetchCount}, MaxRecNoInResponse {MaxRecNoInResponse}", lastRecNo, analysis.TargetFetchCount, currentFetchCount, analysis.NextFetchCount, analysis.MaxRecNoInResponse);
                currentFetchCount = analysis.NextFetchCount;
                continue;
            }

            if (analysis.MaxFetchReachedWithoutNewerRecords)
            {
                logger.LogWarning("Dahua CGI polling reached target/max fetch count without seeing newer records. Camera may have newer records hidden behind count limit and may require larger DAHUA_CGI_FETCH_LOOKAHEAD, pagination/time-window query, or log cleanup. TargetFetchCount {TargetFetchCount}, MaxFetchCount {MaxFetchCount}, CgiLastRecNo {CgiLastRecNo}, MaxRecNoInResponse {MaxRecNoInResponse}", analysis.TargetFetchCount, fetchSettings.MaxFetchCount, lastRecNo, analysis.MaxRecNoInResponse);
            }

            return new DahuaCgiAdaptiveFetchResult(records, currentFetchCount, analysis.TargetFetchCount, analysis.MaxRecNoInResponse, adaptiveRetryHappened, analysis.MaxFetchReachedWithoutNewerRecords);
        }
    }
    private static async Task<bool> HasDebouncedEventAsync(BuildTrackDbContext db, Guid deviceId, BuildTrack.Domain.Dahua.DahuaAccessRecord record, TimeSpan debounceWindow, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.UserId)) return false;
        var windowStart = record.CreateTime - debounceWindow;
        return await db.AttendanceEvents.AnyAsync(
            x => x.DeviceId == deviceId
                 && x.WorkerExternalId == record.UserId
                 && x.Direction == record.NormalizedDirection
                 && x.EventTime >= windowStart
                 && x.EventTime <= record.CreateTime,
            cancellationToken);
    }

    private static async Task<AttendanceSession?> FindOpenSessionAsync(BuildTrackDbContext db, Guid deviceId, BuildTrack.Domain.Dahua.DahuaAccessRecord record, TimeZoneInfo attendanceTimeZone, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.UserId)) return null;

        var workDate = AttendanceSessionPlanner.CalculateWorkDate(record.CreateTime, attendanceTimeZone);
        return await db.AttendanceSessions
            .Where(x => x.DeviceId == deviceId
                        && x.WorkerExternalId == record.UserId
                        && x.WorkDate == workDate
                        && x.Status == AttendanceSessionStatus.Open)
            .OrderByDescending(x => x.CheckInTime)
            .FirstOrDefaultAsync(cancellationToken);
    }
    private async Task<Device?> ResolveDeviceAsync(BuildTrackDbContext db, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(configuration["DAHUA_CGI_DEVICE_ID"], out var configuredDeviceId))
        {
            return await db.Devices.FirstOrDefaultAsync(x => x.Id == configuredDeviceId, cancellationToken);
        }

        var configuredRegisterId = configuration["DAHUA_CGI_REGISTER_DEVICE_ID"];
        if (!string.IsNullOrWhiteSpace(configuredRegisterId))
        {
            return await db.Devices.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(x => x.RegisterDeviceId == configuredRegisterId, cancellationToken);
        }

        return await db.Devices
            .OrderByDescending(x => x.RegisterDeviceId == "BT-API-TEST-001")
            .ThenByDescending(x => x.Mode == DeviceMode.CgiPollingFallback)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static HttpClient CreateHttpClient(string username, string password)
    {
        var handler = new HttpClientHandler
        {
            Credentials = new NetworkCredential(username, password),
            PreAuthenticate = true,
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }


    private static bool IsEnabled(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static int ParsePositiveInt(string? value, int defaultValue) =>
        int.TryParse(value, out var seconds) && seconds > 0 ? seconds : defaultValue;

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (timeZoneId.Equals("Asia/Baku", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        }
        catch (InvalidTimeZoneException) when (timeZoneId.Equals("Asia/Baku", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        }
    }
}

public sealed record DahuaCgiAdaptiveFetchResult(
    IReadOnlyList<BuildTrack.Domain.Dahua.DahuaAccessRecord> Records,
    int FinalFetchCount,
    int TargetFetchCount,
    long MaxRecNoInResponse,
    bool AdaptiveRetryHappened,
    bool MaxFetchReachedWithoutNewerRecords);












