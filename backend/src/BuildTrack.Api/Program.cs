using System.Text.Json;
using System.Text.Json.Serialization;
using BuildTrack.Api.Contracts;
using BuildTrack.Api.Options;
using BuildTrack.Api.Services;
using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure;
using BuildTrack.Infrastructure.Dahua;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Security;
using BuildTrack.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
builder.Services.AddCors(options =>
{
    var allowedOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod();
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
        else
        {
            policy.AllowAnyOrigin();
        }
    });
});
builder.Services.AddBuildTrackInfrastructure(builder.Configuration);
var aiOptions = BuildAiOptions(builder.Configuration);
builder.Services.AddSingleton(aiOptions);
builder.Services.AddHttpClient<IOpenAiProjectAssistantService, OpenAiProjectAssistantService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<AiOptions>();
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

await EnsureDatabaseWithRetryAsync(app.Services, app.Logger, app.Lifetime.ApplicationStopping);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "BuildTrack.Api", time = DateTimeOffset.UtcNow }));

app.MapGet("/api/ai/project-assistant/status", (AiOptions options) =>
    Results.Ok(new ProjectAssistantStatusResponse(
        options.Enabled,
        !string.IsNullOrWhiteSpace(options.ApiKey),
        options.Model,
        options.TtsEnabled,
        options.TtsEnabled && !string.IsNullOrWhiteSpace(options.ApiKey),
        options.TtsModel,
        options.TtsVoice)));

app.MapPost("/api/ai/project-assistant/chat", async (
    ProjectAssistantChatRequest request,
    IOpenAiProjectAssistantService assistantService,
    CancellationToken ct) =>
{
    var response = await assistantService.GetAnswerAsync(request, ct);
    return Results.Ok(response);
});

app.MapPost("/api/ai/tts", async (
    ProjectAssistantTtsRequest request,
    IOpenAiProjectAssistantService assistantService,
    CancellationToken ct) =>
{
    var response = await assistantService.CreateSpeechAsync(request, ct);
    if (response.Success)
    {
        return Results.File(response.Audio, response.ContentType);
    }

    return Results.Json(new { error = response.Error }, statusCode: response.StatusCode);
});

app.MapGet("/api/sites", async (BuildTrackDbContext db, CancellationToken ct) =>
    await db.Sites.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));

app.MapPost("/api/sites", async (CreateSiteRequest request, BuildTrackDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { error = "Site name is required" });
    var site = new Site
    {
        Name = request.Name.Trim(),
        Address = request.Address?.Trim() ?? string.Empty,
        TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Asia/Baku" : request.TimeZone.Trim(),
    };
    db.Sites.Add(site);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/sites/{site.Id}", site);
});

app.MapGet("/api/workers", async (Guid? siteId, BuildTrackDbContext db, CancellationToken ct) =>
{
    var query = db.Workers.AsNoTracking();
    if (siteId is not null) query = query.Where(x => x.SiteId == siteId);
    return await query.OrderBy(x => x.FullName).ToListAsync(ct);
});

app.MapPost("/api/workers", async (CreateWorkerRequest request, BuildTrackDbContext db, CancellationToken ct) =>
{
    if (!await db.Sites.AnyAsync(x => x.Id == request.SiteId, ct)) return Results.BadRequest(new { error = "Site was not found" });
    if (string.IsNullOrWhiteSpace(request.ExternalWorkerCode) || string.IsNullOrWhiteSpace(request.FullName))
    {
        return Results.BadRequest(new { error = "External worker code and full name are required" });
    }

    var worker = new Worker
    {
        SiteId = request.SiteId,
        ExternalWorkerCode = request.ExternalWorkerCode.Trim(),
        FullName = request.FullName.Trim(),
        Status = request.Status,
    };
    db.Workers.Add(worker);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/workers/{worker.Id}", worker);
});

app.MapGet("/api/devices", async (BuildTrackDbContext db, IDahuaActiveRegisterSdk sdk, CancellationToken ct) =>
{
    var devices = await db.Devices
        .AsNoTracking()
        .OrderBy(device => device.Name)
        .ToListAsync(ct);

    if (devices.Count == 0) return Array.Empty<DeviceResponse>();

    var deviceIds = devices.Select(device => device.Id).ToArray();
    var latestEvents = await db.AttendanceEvents
        .AsNoTracking()
        .Where(attendanceEvent => deviceIds.Contains(attendanceEvent.DeviceId))
        .GroupBy(attendanceEvent => attendanceEvent.DeviceId)
        .Select(group => group
            .OrderByDescending(attendanceEvent => attendanceEvent.EventTime)
            .ThenByDescending(attendanceEvent => attendanceEvent.CreatedAt)
            .First())
        .ToListAsync(ct);

    var latestEventByDeviceId = latestEvents.ToDictionary(attendanceEvent => attendanceEvent.DeviceId);
    var netSdkStatus = await GetPersistedNetSdkStatusAsync(db, sdk, ct);
    return devices
        .Select(device => ApiResponseMapper.ToDeviceResponse(
            device,
            latestEventByDeviceId.GetValueOrDefault(device.Id), netSdkStatus))
        .ToArray();
});

app.MapGet("/api/devices/{id:guid}", async (Guid id, BuildTrackDbContext db, IDahuaActiveRegisterSdk sdk, CancellationToken ct) =>
{

    var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    if (device is null) return Results.NotFound();

    var lastEvent = await db.AttendanceEvents
        .AsNoTracking()
        .Where(x => x.DeviceId == device.Id)
        .OrderByDescending(x => x.EventTime)
        .ThenByDescending(x => x.CreatedAt)
        .FirstOrDefaultAsync(ct);
    var netSdkStatus = await GetPersistedNetSdkStatusAsync(db, sdk, ct);
    return Results.Ok(ApiResponseMapper.ToDeviceResponse(device, lastEvent, netSdkStatus));
});

app.MapPost("/api/devices", async (
    CreateDeviceRequest request,
    BuildTrackDbContext db,
    IPasswordProtector passwordProtector,
    CancellationToken ct) =>
{
    if (!await db.Sites.AnyAsync(x => x.Id == request.SiteId, ct)) return Results.BadRequest(new { error = "Site was not found" });
    if (string.IsNullOrWhiteSpace(request.RegisterDeviceId)) return Results.BadRequest(new { error = "RegisterDeviceId is required" });

    var device = new Device
    {
        SiteId = request.SiteId,
        Name = string.IsNullOrWhiteSpace(request.Name) ? request.RegisterDeviceId : request.Name.Trim(),
        Vendor = string.IsNullOrWhiteSpace(request.Vendor) ? "dahua" : request.Vendor.Trim().ToLowerInvariant(),
        Model = string.IsNullOrWhiteSpace(request.Model) ? "DHI-ASI6213J-MW" : request.Model.Trim(),
        Mode = request.Mode,
        RegisterDeviceId = request.RegisterDeviceId.Trim(),
        RegisterPort = request.RegisterPort <= 0 ? 9500 : request.RegisterPort,
        Username = request.Username.Trim(),
        EncryptedPassword = passwordProtector.Protect(request.Password),
        Status = DeviceStatus.Pending,
    };
    db.Devices.Add(device);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/devices/{device.Id}", ApiResponseMapper.ToDeviceResponse(device, null));
});

app.MapPost("/api/devices/{id:guid}/test-config", async (Guid id, BuildTrackDbContext db, IDahuaNativeLibraryProbe probe, CancellationToken ct) =>
{

    var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    if (device is null) return Results.NotFound();
    return Results.Ok(new
    {
        device.Id,
        device.RegisterDeviceId,
        device.RegisterPort,
        device.Mode,
        activeRegisterReady = device.Mode is DeviceMode.ActiveRegister or DeviceMode.Simulator,
        nativeSdkFound = probe.HasNativeSdk,
        warning = probe.HasNativeSdk ? null : "Active Register TCP listener iР•СџlР™в„ўyir. Dahua NetSDK native fayllarР”В± olmadР”В±Р”СџР”В± Р“СР“В§Р“Сn real face/access event decode hР™в„ўlР™в„ў aktiv deyil. SDK fayllarР”В±nР”В± backend/vendor/dahua-netsdk/{win-x64|linux-x64} altР”В±na yerlР™в„ўР•Сџdirin."
    });
});

app.MapPost("/api/devices/{id:guid}/mark-active-register-ready", async (
    Guid id,
    BuildTrackDbContext db,
    IDeviceConnectionLogger connectionLogger,
    CancellationToken ct) =>
{
    var device = await db.Devices.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (device is null) return Results.NotFound();
    device.Status = DeviceStatus.Pending;
    device.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct);
    await connectionLogger.LogAsync(device.Id, device.RegisterDeviceId, null, device.RegisterPort, "ready", "Device marked ready for Dahua Active Register", null, ct);
    return Results.Ok(ApiResponseMapper.ToDeviceResponse(device, null));
});

app.MapGet("/api/devices/{id:guid}/logs", async (Guid id, BuildTrackDbContext db, CancellationToken ct) =>
    await db.DeviceConnectionLogs.AsNoTracking().Where(x => x.DeviceId == id).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct));

app.MapGet("/api/attendance-events", async (Guid? siteId, Guid? deviceId, int? limit, BuildTrackDbContext db, CancellationToken ct) =>
{
    var query = db.AttendanceEvents.AsNoTracking();
    if (siteId is not null) query = query.Where(x => x.SiteId == siteId);
    if (deviceId is not null) query = query.Where(x => x.DeviceId == deviceId);

    var events = await query
        .OrderByDescending(x => x.EventTime)
        .Take(Math.Clamp(limit ?? 200, 1, 1000))
        .ToListAsync(ct);
    return await MapAttendanceEventsAsync(db, events, ct);
});

app.MapGet("/api/sites/{siteId:guid}/attendance-live", async (Guid siteId, int? limit, BuildTrackDbContext db, CancellationToken ct) =>
{
    var events = await db.AttendanceEvents.AsNoTracking()
        .Where(x => x.SiteId == siteId)
        .OrderByDescending(x => x.EventTime)
        .Take(Math.Clamp(limit ?? 100, 1, 500))
        .ToListAsync(ct);
    return await MapAttendanceEventsAsync(db, events, ct);
});


app.MapGet("/api/sites/{siteId:guid}/attendance/live-status", async (Guid siteId, BuildTrackDbContext db, CancellationToken ct) =>
{
    var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Id == siteId, ct);
    if (site is null) return Results.NotFound();

    var timeZone = ResolveApiTimeZone(site.TimeZone);
    var now = DateTimeOffset.UtcNow;
    var workDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
    var sessions = await db.AttendanceSessions.AsNoTracking()
        .Where(x => x.SiteId == siteId && x.WorkDate == workDate && x.Status == AttendanceSessionStatus.Open)
        .OrderBy(x => x.CheckInTime)
        .ToListAsync(ct);
    var staleOpenSessionsCount = await db.AttendanceSessions.AsNoTracking()
        .CountAsync(x => x.SiteId == siteId && x.WorkDate < workDate && x.Status == AttendanceSessionStatus.Open, ct);

    var workers = AttendanceSessionPlanner.SelectCurrentOpenSessions(sessions, workDate)
        .GroupBy(session => new { session.DeviceId, session.WorkerExternalId, session.WorkDate })
        .Select(group => group.OrderBy(session => session.CheckInTime).First())
        .OrderBy(session => session.CheckInTime)
        .Select(session =>
        {
            var lastSeenTime = session.LastSeenTime ?? session.CheckInTime;
            return new AttendanceLiveWorkerResponse(
                session.WorkerExternalId,
                session.WorkerName,
                session.CheckInTime,
                FormatLocalTime(session.CheckInTime, timeZone),
                lastSeenTime,
                FormatLocalTime(lastSeenTime, timeZone),
                session.CheckOutTime,
                session.CheckOutTime is null ? null : FormatLocalTime(session.CheckOutTime.Value, timeZone),
                session.CloseReason,
                AttendanceSessionPlanner.BuildDisplayStatus(session.Status, session.CloseReason, lastSeenTime, now),
                IsCheckoutConfirmed(session),
                Math.Max(0, (int)Math.Floor((lastSeenTime - session.CheckInTime).TotalMinutes)),
                session.Status);
        })
        .ToArray();

    return Results.Ok(new AttendanceLiveStatusResponse(workDate, workers.Length, workers, staleOpenSessionsCount));
});

app.MapGet("/api/sites/{siteId:guid}/attendance/daily", async (Guid siteId, string? date, BuildTrackDbContext db, CancellationToken ct) =>
{
    var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Id == siteId, ct);
    if (site is null) return Results.NotFound();

    var timeZone = ResolveApiTimeZone(site.TimeZone);
    var workDate = DateOnly.TryParse(date, out var parsedDate)
        ? parsedDate
        : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);

    var sessions = await db.AttendanceSessions.AsNoTracking()
        .Where(x => x.SiteId == siteId && x.WorkDate == workDate)
        .OrderBy(x => x.CheckInTime)
        .ToListAsync(ct);

    var now = DateTimeOffset.UtcNow;
    var dailySessions = sessions
        .GroupBy(session => new { session.SiteId, session.DeviceId, session.WorkerExternalId, session.WorkDate })
        .Select(group =>
        {
            var ordered = group.OrderBy(session => session.CheckInTime).ToArray();
            var first = ordered[0];
            var hasOpen = ordered.Any(session => session.Status == AttendanceSessionStatus.Open);
            var latestCheckout = ordered
                .Where(session => session.CheckOutTime is not null)
                .OrderByDescending(session => session.CheckOutTime)
                .FirstOrDefault();
            return new
            {
                Session = first,
                CheckInTime = ordered.Min(session => session.CheckInTime),
                CheckOutTime = hasOpen ? null : latestCheckout?.CheckOutTime,
                LastSeenTime = ordered.Select(session => session.LastSeenTime ?? session.CheckInTime).Max(),
                CloseReason = latestCheckout?.CloseReason,
                Status = hasOpen ? AttendanceSessionStatus.Open : AttendanceSessionStatus.Closed,
                WorkerName = ordered.LastOrDefault(session => !string.IsNullOrWhiteSpace(session.WorkerName))?.WorkerName ?? first.WorkerName,
                Source = latestCheckout?.Source ?? first.Source,
            };
        })
        .OrderBy(row => row.CheckInTime)
        .ToArray();

    var sessionRows = dailySessions.Select(row =>
    {
        var confirmedCheckoutTime = IsCheckoutConfirmed(row.Session) ? row.CheckOutTime : null;
        var lastSeenTime = row.LastSeenTime;
        var effectiveEnd = confirmedCheckoutTime ?? lastSeenTime;
        var workedMinutes = Math.Max(0, (int)Math.Floor((effectiveEnd - row.CheckInTime).TotalMinutes));
        return new AttendanceSessionResponse(
            row.Session.Id,
            row.Session.WorkerExternalId,
            row.WorkerName,
            row.CheckInTime,
            confirmedCheckoutTime,
            FormatLocalTime(row.CheckInTime, timeZone),
            confirmedCheckoutTime is null ? null : FormatLocalTime(confirmedCheckoutTime.Value, timeZone),
            lastSeenTime,
            FormatLocalTime(lastSeenTime, timeZone),
            confirmedCheckoutTime,
            confirmedCheckoutTime is null ? null : FormatLocalTime(confirmedCheckoutTime.Value, timeZone),
            row.CloseReason,
            AttendanceSessionPlanner.BuildDisplayStatus(row.Status, row.CloseReason, lastSeenTime, now),
            confirmedCheckoutTime is not null,
            workedMinutes,
            row.Status,
            row.Source);
    }).ToArray();

    return Results.Ok(new AttendanceDailyResponse(
        workDate,
        dailySessions.Length,
        dailySessions.Count(x => x.Status == AttendanceSessionStatus.Open),
        dailySessions.Count(x => x.Status == AttendanceSessionStatus.Closed),
        Math.Round(sessionRows.Sum(x => x.WorkedMinutes) / 60d, 2),
        sessionRows));
});

app.MapGet("/api/sites/{siteId:guid}/security-events", async (Guid siteId, string? date, BuildTrackDbContext db, CancellationToken ct) =>
{
    var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Id == siteId, ct);
    if (site is null) return Results.NotFound();

    var timeZone = ResolveApiTimeZone(site.TimeZone);
    var eventDate = DateOnly.TryParse(date, out var parsedDate)
        ? parsedDate
        : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);

    var events = await db.SecurityEvents.AsNoTracking()
        .Where(x => x.SiteId == siteId && x.EventDate == eventDate)
        .OrderByDescending(x => x.EventTime)
        .Take(300)
        .ToListAsync(ct);

    var deviceIds = events.Select(x => x.DeviceId).Distinct().ToArray();
    var deviceNames = await db.Devices.AsNoTracking()
        .Where(x => deviceIds.Contains(x.Id))
        .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

    return Results.Ok(events.Select(securityEvent => new SecurityEventResponse(
        securityEvent.Id,
        securityEvent.EventTime,
        FormatLocalTime(securityEvent.EventTime, timeZone),
        securityEvent.EventType,
        securityEvent.Severity,
        securityEvent.Status,
        deviceNames.GetValueOrDefault(securityEvent.DeviceId),
        site.Name,
        securityEvent.SnapshotPath,
        $"/api/security-events/{securityEvent.Id}/snapshot",
        securityEvent.SnapshotDownloadStatus,
        securityEvent.SnapshotDownloadError,
        securityEvent.SnapshotSource,
        securityEvent.Message,
        securityEvent.RawRecNo)).ToArray());
});

app.MapPatch("/api/security-events/{eventId:guid}/review", async (Guid eventId, ReviewSecurityEventRequest request, BuildTrackDbContext db, CancellationToken ct) =>
{
    if (request.Status is not (SecurityEventStatus.Reviewed or SecurityEventStatus.Ignored))
    {
        return Results.BadRequest(new { error = "Status must be Reviewed or Ignored" });
    }

    var securityEvent = await db.SecurityEvents.FirstOrDefaultAsync(x => x.Id == eventId, ct);
    if (securityEvent is null) return Results.NotFound();

    securityEvent.Status = request.Status;
    securityEvent.ReviewNote = string.IsNullOrWhiteSpace(request.ReviewNote) ? null : request.ReviewNote.Trim();
    securityEvent.ReviewedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { securityEvent.Id, securityEvent.Status, securityEvent.ReviewedAt });
});

app.MapGet("/api/security-events/{eventId:guid}/snapshot", async (Guid eventId, BuildTrackDbContext db, CancellationToken ct) =>
{
    var securityEvent = await db.SecurityEvents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == eventId, ct);
    if (securityEvent is null) return Results.NotFound();

    var localSnapshot = await SecuritySnapshotFileReader.TryReadAsync(securityEvent, ct);
    if (!localSnapshot.Exists || localSnapshot.Bytes is null) return Results.NotFound();
    return Results.File(localSnapshot.Bytes, localSnapshot.ContentType);
});

app.MapGet("/api/dahua/active-register/status", async (BuildTrackDbContext db, IDahuaActiveRegisterSdk sdk, IConfiguration configuration, CancellationToken ct) =>
{
    var diagnostics = await db.NetSdkRuntimeDiagnostics.AsNoTracking().FirstOrDefaultAsync(x => x.Id == "dahua-netsdk-runtime", ct);
    var lastRawEvent = await db.DahuaActiveRegisterRawEvents.AsNoTracking().OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
    var rawEventCount = await db.DahuaActiveRegisterRawEvents.AsNoTracking().CountAsync(ct);
    var decodedEventCount = await db.DahuaActiveRegisterRawEvents.AsNoTracking().CountAsync(x => x.DecodeStatus.StartsWith("Decoded") || x.DecodeStatus == "Ingested", ct);
    var ingestedEventCount = await db.DahuaActiveRegisterRawEvents.AsNoTracking().CountAsync(x => x.DecodeStatus == "Ingested", ct);
    var apiPorts = ParsePorts(configuration["DAHUA_ACTIVE_REGISTER_PORTS"]);
    var workerPorts = ParseDiagnosticsPorts(diagnostics?.ListenerPortsJson);
    var effectivePorts = workerPorts.Length > 0 ? workerPorts : apiPorts;
    var apiEnabled = IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_ENABLED"]);
    var apiIngestionEnabled = IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_INGESTION_ENABLED"]);
    var diagnosticsRecent = diagnostics?.UpdatedAt >= DateTimeOffset.UtcNow.AddMinutes(-5);
    var rawEventRecent = lastRawEvent?.CreatedAt >= DateTimeOffset.UtcNow.AddMinutes(-10);
    var workerListenerActive = sdk.IsSdkListenerActive || (diagnostics?.SdkInitialized == true && effectivePorts.Length > 0 && (diagnosticsRecent || rawEventRecent || lastRawEvent is not null));
    var workerIngestionObserved = diagnostics?.LoginStrategy is not null || diagnostics?.StartListenExCalled == true || diagnostics?.StartListenExSuccess == true;

    return Results.Ok(new
    {
        enabled = apiEnabled || workerListenerActive,
        listenerActive = workerListenerActive,
        ports = effectivePorts,
        lastCallbackTime = lastRawEvent?.CreatedAt,
        lastCommand = lastRawEvent?.CallbackCommandName ?? diagnostics?.LastServiceEventType,
        lastPayloadBytes = lastRawEvent?.PayloadBytes ?? diagnostics?.LastServicePayloadBytes ?? 0,
        lastPayloadFirst256Hex = lastRawEvent?.PayloadFirstBytesHex ?? diagnostics?.LastServicePayloadFirst256Hex,
        rawEventCount,
        decodedEventCount,
        ingestedEventCount,
        ingestionEnabled = apiIngestionEnabled || workerIngestionObserved,
        diagnosticsEnabled = IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_DIAGNOSTICS_ENABLED"], defaultValue: true),
        decodeStatus = diagnostics?.NetSdkDecodeStatus ?? sdk.DecodeStatus,
        warning = sdk.StartupWarning,
        apiConfig = new
        {
            enabled = apiEnabled,
            ingestionEnabled = apiIngestionEnabled,
            netsdkRecordQueryDiagnosticMode = IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_NETSDK_RECORD_QUERY_DIAGNOSTIC_MODE"]),
            ports = apiPorts,
        },
        worker = diagnostics is null ? null : new
        {
            diagnosticsPresent = true,
            listenerActive = workerListenerActive,
            sdkLoaded = diagnostics.SdkLoaded,
            sdkInitialized = diagnostics.SdkInitialized,
            ports = workerPorts,
            diagnostics.UpdatedAt,
            diagnostics.NetSdkDecodeStatus,
            diagnostics.LastServiceCommand,
            diagnostics.LastServiceEventType,
            diagnostics.LastServicePayloadBytes,
            diagnostics.LastServicePayloadFirst256Hex,
            diagnostics.LastRegisterDeviceId,
            diagnostics.LastParsedRegisterDeviceIdOffset,
            diagnostics.LastParsedRegisterDeviceId,
            diagnostics.LastParsedSerialOffset,
            diagnostics.LastParsedSerial,
            diagnostics.LastParsedRemoteIp,
            diagnostics.LastParsedRemotePort,
            diagnostics.LastPossibleSessionHandlesJson,
            diagnostics.LastPayloadStructLayout,
            diagnostics.ResponseDevRegCalled,
            diagnostics.ResponseDevRegSuccess,
            diagnostics.ResponseDevRegErrorSigned,
            diagnostics.ResponseDevRegErrorHex,
            diagnostics.ResponseDevRegDevSerial,
            diagnostics.ResponseDevRegIp,
            diagnostics.ResponseDevRegPort,
            diagnostics.LoginStrategy,
            diagnostics.LoginHandle,
            diagnostics.LoginSucceeded,
            diagnostics.LoginErrorSigned,
            diagnostics.LoginErrorHex,
            diagnostics.LoginNativeErrorSigned,
            diagnostics.LoginNativeErrorHex,
            diagnostics.LoginPossibleMarshallingWarning,
            diagnostics.StartListenExCalled,
            diagnostics.StartListenExSuccess,
            diagnostics.StartListenExErrorSigned,
            diagnostics.StartListenExErrorHex,
            diagnostics.ExperimentalServiceHandleSubscribeEnabled,
            diagnostics.LastExperimentalSubscribeJson,
            diagnostics.LastAlarmCommand,
            diagnostics.LastAlarmCommandName,
            diagnostics.LastAlarmPayloadFirst256Hex,
            diagnostics.LastAlarmDecodeStatus,
            diagnostics.LastDecodedAlarmJson,
            diagnostics.NetSdkRecordQueryEnabled,
            diagnostics.NetSdkRecordQueryDiagnosticMode,
            diagnostics.LastRecordQueryAt,
            diagnostics.LastRecordQuerySuccess,
            diagnostics.LastRecordQueryError,
            diagnostics.LastRecordQueryCount,
            diagnostics.LastRecordQueryLastRecNo,
            diagnostics.LastDecodeError,
        },
        workerDiagnosticsPresent = diagnostics is not null,
        workerListenerActive,
        lastDecodeStatus = diagnostics?.NetSdkDecodeStatus,
        lastLoginStrategy = diagnostics?.LoginStrategy,
        lastLoginSucceeded = diagnostics?.LoginSucceeded,
        lastLoginErrorSigned = diagnostics?.LoginErrorSigned,
        lastLoginErrorHex = diagnostics?.LoginErrorHex,
        lastLoginNativeErrorSigned = diagnostics?.LoginNativeErrorSigned,
        lastLoginNativeErrorHex = diagnostics?.LoginNativeErrorHex,
        loginPossibleMarshallingWarning = diagnostics?.LoginPossibleMarshallingWarning ?? false,
        startListenExSucceeded = diagnostics?.StartListenExSuccess,
        startListenExErrorHex = diagnostics?.StartListenExErrorHex,
        netsdkRecordQueryEnabled = diagnostics?.NetSdkRecordQueryEnabled ?? IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_NETSDK_RECORD_QUERY_ENABLED"]),
        netsdkRecordQueryDiagnosticMode = diagnostics?.NetSdkRecordQueryDiagnosticMode ?? IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_NETSDK_RECORD_QUERY_DIAGNOSTIC_MODE"]),
        lastRecordQueryAt = diagnostics?.LastRecordQueryAt,
        lastRecordQuerySuccess = diagnostics?.LastRecordQuerySuccess,
        lastRecordQueryError = diagnostics?.LastRecordQueryError,
        lastRecordQueryCount = diagnostics?.LastRecordQueryCount ?? 0,
        lastRecordQueryLastRecNo = diagnostics?.LastRecordQueryLastRecNo,
    });
});
app.MapGet("/api/dahua/active-register/record-query-test", async (
    Guid deviceId,
    int? maxRecords,
    IDahuaActiveRegisterSdk sdk,
    CancellationToken ct) =>
{
    var result = await sdk.RunRecordQueryDiagnosticAsync(deviceId, Math.Clamp(maxRecords ?? 20, 1, 200), ct);
    return Results.Ok(result);
});
app.MapGet("/api/dahua/active-register/latest-record-query-diagnostic", async (
    Guid? deviceId,
    BuildTrackDbContext db,
    CancellationToken ct) =>
{
    var query = db.DahuaActiveRegisterRawEvents
        .AsNoTracking()
        .Where(x => x.CallbackCommandName != null && x.CallbackCommandName.StartsWith("NETSDK_RECORD_QUERY_"));

    if (deviceId is not null)
    {
        query = query.Where(x => x.DeviceId == deviceId);
    }

    var latest = await query
        .OrderByDescending(x => x.CreatedAt)
        .Select(x => new
        {
            x.Id,
            x.DeviceId,
            x.RegisterDeviceId,
            x.CallbackCommand,
            x.CallbackCommandName,
            x.DecodeStatus,
            x.DecodedJson,
            x.CreatedAt,
        })
        .FirstOrDefaultAsync(ct);

    return latest is null
        ? Results.NotFound(new { error = "No persisted Dahua NetSDK record-query diagnostic was found", deviceId })
        : Results.Ok(latest);
});
app.MapGet("/api/dahua/active-register/raw-events", async (int? limit, BuildTrackDbContext db, CancellationToken ct) =>
{
    var take = Math.Clamp(limit ?? 100, 1, 500);
    return await db.DahuaActiveRegisterRawEvents
        .AsNoTracking()
        .OrderByDescending(x => x.CreatedAt)
        .Take(take)
        .Select(x => new
        {
            x.Id,
            x.DeviceId,
            x.RegisterDeviceId,
            x.RemoteIp,
            x.RemotePort,
            x.ListenerPort,
            x.CallbackCommand,
            x.CallbackCommandName,
            x.PayloadBytes,
            x.PayloadFirstBytesHex,
            x.DecodeStatus,
            x.DecodedJson,
            x.CreatedAt,
        })
        .ToListAsync(ct);
});
app.MapGet("/api/dahua/listener/status", (IConfiguration configuration, IDahuaActiveRegisterSdk sdk) =>
{
    var ports = ParsePorts(configuration["DAHUA_ACTIVE_REGISTER_PORTS"]);
    return Results.Ok(new
    {
        ports,
        defaultPorts = new[] { 7000, 9500 },
        realSdkAvailable = sdk.IsRealSdkAvailable,
        decodeStatus = sdk.DecodeStatus,
        simulatorEnabled = bool.TryParse(configuration["DAHUA_SIMULATOR_ENABLED"], out var enabled) && enabled,
        warning = string.IsNullOrWhiteSpace(sdk.StartupWarning) ? null : sdk.StartupWarning,
    });
});

app.MapGet("/api/dahua/netsdk/diagnostics", async (BuildTrackDbContext db, IDahuaActiveRegisterSdk sdk, CancellationToken ct) =>
{
    var persisted = await db.NetSdkRuntimeDiagnostics.AsNoTracking().FirstOrDefaultAsync(x => x.Id == "dahua-netsdk-runtime", ct);
    if (persisted is null) return Results.Ok(sdk.Diagnostics);

    return Results.Ok(new
    {
        sdkLoaded = persisted.SdkLoaded,
        sdkInitialized = persisted.SdkInitialized,
        listenerPorts = JsonSerializer.Deserialize<int[]>(persisted.ListenerPortsJson) ?? [],
        alarmCallbackConfigured = persisted.AlarmCallbackConfigured,
        activeRegisterServiceMode = persisted.ActiveRegisterServiceMode,
        experimentalStartServiceEnabled = persisted.ExperimentalStartServiceEnabled,
        experimentalStartServiceStarted = persisted.ExperimentalStartServiceStarted,
        experimentalStartServiceHandle = persisted.ExperimentalStartServiceHandle,
        experimentalStartServiceLastCommand = persisted.ExperimentalStartServiceLastCommand,
        experimentalStartServiceLastPayloadBytes = persisted.ExperimentalStartServiceLastPayloadBytes,
        experimentalStartServiceLastDecodeStatus = persisted.ExperimentalStartServiceLastDecodeStatus,
        experimentalStartServiceErrorSigned = persisted.ExperimentalStartServiceErrorSigned,
        experimentalStartServiceErrorHex = persisted.ExperimentalStartServiceErrorHex,
        lastServiceCommand = persisted.LastServiceCommand,
        lastServiceEventType = persisted.LastServiceEventType,
        lastServicePayloadBytes = persisted.LastServicePayloadBytes,
        lastServicePayloadFirst256Hex = persisted.LastServicePayloadFirst256Hex,
        lastRegisterDeviceId = persisted.LastRegisterDeviceId,
        lastParsedRegisterDeviceIdOffset = persisted.LastParsedRegisterDeviceIdOffset,
        lastParsedRegisterDeviceId = persisted.LastParsedRegisterDeviceId,
        lastParsedSerialOffset = persisted.LastParsedSerialOffset,
        lastParsedSerial = persisted.LastParsedSerial,
        lastParsedRemoteIp = persisted.LastParsedRemoteIp,
        lastParsedRemotePort = persisted.LastParsedRemotePort,
        lastPossibleSessionHandlesJson = persisted.LastPossibleSessionHandlesJson,
        lastPayloadStructLayout = persisted.LastPayloadStructLayout,
        responseDevRegCalled = persisted.ResponseDevRegCalled,
        responseDevRegSuccess = persisted.ResponseDevRegSuccess,
        responseDevRegErrorSigned = persisted.ResponseDevRegErrorSigned,
        responseDevRegErrorHex = persisted.ResponseDevRegErrorHex,
        responseDevRegDevSerial = persisted.ResponseDevRegDevSerial,
        responseDevRegDevSerialLength = persisted.ResponseDevRegDevSerialLength,
        responseDevRegIp = persisted.ResponseDevRegIp,
        responseDevRegPort = persisted.ResponseDevRegPort,
        responseDevRegAccept = persisted.ResponseDevRegAccept,
        responseDevRegCommandSource = persisted.ResponseDevRegCommandSource,
        lastServiceCallbackHandle = persisted.LastServiceCallbackHandle,
        lastServiceCallbackHandleNonZero = persisted.LastServiceCallbackHandleNonZero,
        activeRegisterSessionHandleFound = persisted.ActiveRegisterSessionHandleFound,
        activeRegisterSessionHandleValueNonZero = persisted.ActiveRegisterSessionHandleValueNonZero,
        activeRegisterSessionHandleValue = persisted.ActiveRegisterSessionHandleValue,
        activeRegisterSessionHandleSource = persisted.ActiveRegisterSessionHandleSource,
        strategyResult = persisted.ActiveRegisterSessionHandleStrategyResult,
        startListenExCalled = persisted.StartListenExCalled,
        startListenExSuccess = persisted.StartListenExSuccess,
        startListenExErrorSigned = persisted.StartListenExErrorSigned,
        startListenExErrorHex = persisted.StartListenExErrorHex,
        experimentalServiceHandleSubscribeEnabled = persisted.ExperimentalServiceHandleSubscribeEnabled,
        lastExperimentalSubscribeJson = persisted.LastExperimentalSubscribeJson,
        lastAlarmCommand = persisted.LastAlarmCommand,
        lastAlarmCommandName = persisted.LastAlarmCommandName,
        lastAlarmPayloadFirst256Hex = persisted.LastAlarmPayloadFirst256Hex,
        lastAlarmDecodeStatus = persisted.LastAlarmDecodeStatus,
        lastDecodedAlarmJson = persisted.LastDecodedAlarmJson,
        netsdkRecordQueryEnabled = persisted.NetSdkRecordQueryEnabled,
        netsdkRecordQueryDiagnosticMode = persisted.NetSdkRecordQueryDiagnosticMode,
        lastRecordQueryAt = persisted.LastRecordQueryAt,
        lastRecordQuerySuccess = persisted.LastRecordQuerySuccess,
        lastRecordQueryError = persisted.LastRecordQueryError,
        lastRecordQueryCount = persisted.LastRecordQueryCount,
        lastRecordQueryLastRecNo = persisted.LastRecordQueryLastRecNo,
        lastDecodeError = persisted.LastDecodeError,
        netSdkDecodeStatus = persisted.NetSdkDecodeStatus,
        updatedAt = persisted.UpdatedAt,
    });
});

app.MapPost("/api/devices/{id:guid}/simulate-active-register", async (
    Guid id,
    BuildTrackDbContext db,
    IDeviceConnectionLogger connectionLogger,
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    CancellationToken ct) =>
{
    if (!DahuaDevSimulatorPolicy.IsEnabled(configuration))
    {
        loggerFactory.CreateLogger("DahuaDevSimulator").LogWarning("Dahua dev simulator action blocked because simulator actions are disabled");
        return Results.Json(new { error = "Dahua dev simulator actions are disabled" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var device = await db.Devices.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (device is null) return Results.NotFound();
    device.Status = DeviceStatus.Online;
    device.LastSeenAt = DateTimeOffset.UtcNow;
    device.LastKnownIp = "simulator";
    device.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct);
    await connectionLogger.LogAsync(device.Id, device.RegisterDeviceId, "simulator", device.RegisterPort, "register", "Simulator marked device online", null, ct);
    return Results.Ok(ApiResponseMapper.ToDeviceResponse(device, null));
});

app.MapPost("/api/devices/{id:guid}/simulate-event", async (
    Guid id,
    SimulateEventRequest request,
    BuildTrackDbContext db,
    IAttendanceIngestionService ingestion,
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    CancellationToken ct) =>
{
    if (!DahuaDevSimulatorPolicy.IsEnabled(configuration))
    {
        loggerFactory.CreateLogger("DahuaDevSimulator").LogWarning("Dahua dev simulator action blocked because simulator actions are disabled");
        return Results.Json(new { error = "Dahua dev simulator actions are disabled" }, statusCode: StatusCodes.Status403Forbidden);
    }

    var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    if (device is null) return Results.NotFound();

    var fallbackWorker = await db.Workers.AsNoTracking().FirstOrDefaultAsync(x => x.SiteId == device.SiteId, ct);
    var record = new DahuaAccessRecord
    {
        RecNo = request.RecNo ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        CreateTime = request.EventTime ?? DateTimeOffset.UtcNow,
        UserId = request.WorkerExternalId ?? fallbackWorker?.ExternalWorkerCode ?? "1",
        CardName = request.WorkerName ?? fallbackWorker?.FullName ?? "Simulator Worker",
        StatusRaw = request.Status,
        MethodRaw = request.Method,
        Type = request.Direction,
        RawFields = new Dictionary<string, string?>
        {
            ["RecNo"] = (request.RecNo ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString(),
            ["CreateTime"] = (request.EventTime ?? DateTimeOffset.UtcNow).ToString("O"),
            ["UserID"] = request.WorkerExternalId ?? fallbackWorker?.ExternalWorkerCode ?? "1",
            ["CardName"] = request.WorkerName ?? fallbackWorker?.FullName ?? "Simulator Worker",
            ["Status"] = request.Status,
            ["Method"] = request.Method,
            ["Type"] = request.Direction,
        },
    };

    var inserted = await ingestion.IngestDahuaRecordAsync(device.Id, record, "simulator", device.RegisterPort, ct);
    if (inserted is null) return Results.Conflict(new { message = "Duplicate event ignored" });

    var siteName = await db.Sites.AsNoTracking().Where(x => x.Id == inserted.SiteId).Select(x => x.Name).FirstOrDefaultAsync(ct);
    return Results.Ok(ApiResponseMapper.ToAttendanceEventResponse(inserted, siteName, device.Name));
});

app.Run();

static AiOptions BuildAiOptions(IConfiguration configuration)
{
    var apiKey = configuration["OPENAI_API_KEY"] ?? configuration["Ai:ApiKey"] ?? string.Empty;
    return new AiOptions
    {
        Enabled = IsEnabled(configuration["OPENAI_ASSISTANT_ENABLED"] ?? configuration["Ai:Enabled"], false),
        ApiKey = apiKey,
        Model = string.IsNullOrWhiteSpace(configuration["OPENAI_MODEL"] ?? configuration["Ai:Model"])
            ? "gpt-4o-mini"
            : (configuration["OPENAI_MODEL"] ?? configuration["Ai:Model"] ?? "gpt-4o-mini").Trim(),
        TimeoutSeconds = int.TryParse(configuration["OPENAI_TIMEOUT_SECONDS"] ?? configuration["Ai:TimeoutSeconds"], out var timeout)
            ? Math.Clamp(timeout, 5, 60)
            : 30,
        TtsEnabled = IsEnabled(configuration["OPENAI_TTS_ENABLED"] ?? configuration["Ai:TtsEnabled"], !string.IsNullOrWhiteSpace(apiKey)),
        TtsModel = string.IsNullOrWhiteSpace(configuration["OPENAI_TTS_MODEL"] ?? configuration["Ai:TtsModel"])
            ? "gpt-4o-mini-tts"
            : (configuration["OPENAI_TTS_MODEL"] ?? configuration["Ai:TtsModel"] ?? "gpt-4o-mini-tts").Trim(),
        TtsVoice = string.IsNullOrWhiteSpace(configuration["OPENAI_TTS_VOICE"] ?? configuration["Ai:TtsVoice"])
            ? "alloy"
            : (configuration["OPENAI_TTS_VOICE"] ?? configuration["Ai:TtsVoice"] ?? "alloy").Trim(),
        TtsFormat = string.IsNullOrWhiteSpace(configuration["OPENAI_TTS_FORMAT"] ?? configuration["Ai:TtsFormat"])
            ? "mp3"
            : (configuration["OPENAI_TTS_FORMAT"] ?? configuration["Ai:TtsFormat"] ?? "mp3").Trim(),
    };
}

static TimeZoneInfo ResolveApiTimeZone(string? timeZoneId)
{
    var candidate = string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Baku" : timeZoneId;
    try
    {
        return TimeZoneInfo.FindSystemTimeZoneById(candidate);
    }
    catch (TimeZoneNotFoundException) when (candidate.Equals("Asia/Baku", StringComparison.OrdinalIgnoreCase))
    {
        return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
    }
    catch (InvalidTimeZoneException) when (candidate.Equals("Asia/Baku", StringComparison.OrdinalIgnoreCase))
    {
        return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
    }
}

static bool IsCheckoutConfirmed(AttendanceSession session) => session.CheckOutTime is not null && session.CloseReason is not null && new[] { "Manual", "AutoEndOfDay", "ExitDevice", "DeviceDirection" }.Contains(session.CloseReason);
static string FormatLocalTime(DateTimeOffset value, TimeZoneInfo timeZone) =>
    TimeZoneInfo.ConvertTime(value, timeZone).ToString("yyyy-MM-dd HH:mm:ss");
static async Task<AttendanceEventResponse[]> MapAttendanceEventsAsync(
    BuildTrackDbContext db,
    IReadOnlyCollection<AttendanceEvent> events,
    CancellationToken ct)
{
    if (events.Count == 0) return [];

    var siteIds = events.Select(x => x.SiteId).Distinct().ToArray();
    var deviceIds = events.Select(x => x.DeviceId).Distinct().ToArray();
    var siteNames = await db.Sites.AsNoTracking()
        .Where(x => siteIds.Contains(x.Id))
        .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
    var deviceNames = await db.Devices.AsNoTracking()
        .Where(x => deviceIds.Contains(x.Id))
        .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

    return events
        .Select(attendanceEvent => ApiResponseMapper.ToAttendanceEventResponse(
            attendanceEvent,
            siteNames.GetValueOrDefault(attendanceEvent.SiteId),
            deviceNames.GetValueOrDefault(attendanceEvent.DeviceId)))
        .ToArray();
}
static async Task<string> GetPersistedNetSdkStatusAsync(BuildTrackDbContext db, IDahuaActiveRegisterSdk sdk, CancellationToken ct)
{
    var status = await db.NetSdkRuntimeDiagnostics
        .AsNoTracking()
        .Where(x => x.Id == "dahua-netsdk-runtime")
        .Select(x => x.NetSdkDecodeStatus)
        .FirstOrDefaultAsync(ct);
    return string.IsNullOrWhiteSpace(status) ? sdk.DecodeStatus : status;
}
static bool IsEnabled(string? value, bool defaultValue = false)
{
    if (string.IsNullOrWhiteSpace(value)) return defaultValue;
    return value.Equals("true", StringComparison.OrdinalIgnoreCase)
           || value.Equals("1", StringComparison.OrdinalIgnoreCase)
           || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
}

static int[] ParseDiagnosticsPorts(string? json)
{
    if (string.IsNullOrWhiteSpace(json)) return [];
    try
    {
        return JsonSerializer.Deserialize<int[]>(json) ?? [];
    }
    catch
    {
        return [];
    }
}
static int[] ParsePorts(string? raw) => (string.IsNullOrWhiteSpace(raw) ? "7000,9500" : raw)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(value => int.TryParse(value, out var port) ? port : 0)
    .Where(port => port > 0)
    .Distinct()
    .DefaultIfEmpty(9500)
    .ToArray();

static async Task EnsureDatabaseWithRetryAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
{
    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BuildTrackDbContext>();
            await DbInitializer.EnsureDatabaseAsync(db, cancellationToken);
            return;
        }
        catch (Exception ex) when (attempt < 10)
        {
            logger.LogWarning(ex, "Database is not ready. Retry {Attempt}/10", attempt);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}






























