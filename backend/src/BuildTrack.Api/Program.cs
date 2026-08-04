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
using BuildTrack.Infrastructure.Tenancy;
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
var jwtOptions = BuildJwtOptions(builder.Configuration);
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
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

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (!IsApiPath(path) || IsPublicApiPath(path))
    {
        await next();
        return;
    }

    var tokenService = context.RequestServices.GetRequiredService<IJwtTokenService>();
    var principal = tokenService.ValidateToken(ExtractBearerToken(context.Request));
    if (principal is null)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Authentication is required" });
        return;
    }

    var tenantContext = context.RequestServices.GetRequiredService<ITenantContext>();
    tenantContext.TenantId = principal.TenantId;
    tenantContext.UserId = principal.UserId;
    tenantContext.Role = principal.Role.ToString();

    if (IsLicenseExemptPath(path))
    {
        await next();
        return;
    }

    var db = context.RequestServices.GetRequiredService<BuildTrackDbContext>();
    var hasActiveLicense = await db.Licenses
        .AsNoTracking()
        .AnyAsync(x => x.TenantId == principal.TenantId
                       && x.Status == LicenseStatus.Active
                       && (x.ExpiresAt == null || x.ExpiresAt > DateTimeOffset.UtcNow));
    if (!hasActiveLicense)
    {
        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        await context.Response.WriteAsJsonAsync(new { error = "Active license is required" });
        return;
    }

    await next();
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "BuildTrack.Api", time = DateTimeOffset.UtcNow }));

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    BuildTrackDbContext db,
    IJwtTokenService tokenService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.CompanyName)
        || string.IsNullOrWhiteSpace(request.FullName)
        || string.IsNullOrWhiteSpace(request.Email)
        || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Company, full name, email and password are required" });
    }

    if (request.Password.Length < 8) return Results.BadRequest(new { error = "Password must be at least 8 characters" });
    var email = request.Email.Trim().ToLowerInvariant();
    if (await db.Users.AnyAsync(x => x.Email == email, ct)) return Results.Conflict(new { error = "Email is already registered" });

    var tenant = new Tenant
    {
        CompanyName = request.CompanyName.Trim(),
        Code = await GenerateTenantCodeAsync(db, request.CompanyName, ct),
        Status = TenantStatus.Active,
    };
    var user = new AppUser
    {
        TenantId = tenant.Id,
        FullName = request.FullName.Trim(),
        Email = email,
        PasswordHash = BuildTrackPasswordHasher.HashPassword(request.Password),
        Role = BuildTrackUserRole.Owner,
        Status = BuildTrackUserStatus.Active,
    };
    var license = new License
    {
        TenantId = tenant.Id,
        LicenseKeyHash = $"pending-{tenant.Id:N}",
        Plan = LicensePlan.Trial,
        Status = LicenseStatus.Pending,
        StartsAt = DateTimeOffset.UtcNow,
    };

    db.Tenants.Add(tenant);
    db.Users.Add(user);
    db.Licenses.Add(license);
    await db.SaveChangesAsync(ct);

    return CreateAuthResponseResult(tokenService, user, tenant, license);
});

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    BuildTrackDbContext db,
    IJwtTokenService tokenService,
    CancellationToken ct) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.Users
        .Include(x => x.Tenant)
        .FirstOrDefaultAsync(x => x.Email == email && x.Status == BuildTrackUserStatus.Active, ct);
    if (user is null || !BuildTrackPasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var license = await GetCurrentLicenseAsync(db, user.TenantId, ct);
    return CreateAuthResponseResult(tokenService, user, user.Tenant!, license);
});

app.MapGet("/api/auth/me", async (BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct) =>
{
    var userId = RequireUserId(tenantContext);
    var user = await db.Users.Include(x => x.Tenant).FirstOrDefaultAsync(x => x.Id == userId, ct);
    if (user is null) return Results.Unauthorized();
    var license = await GetCurrentLicenseAsync(db, user.TenantId, ct);
    return Results.Ok(new AuthMeResponse(ToAuthUserResponse(user), ToTenantResponse(user.Tenant!), license is null ? null : ToLicenseResponse(license)));
});

app.MapPost("/api/auth/logout", () => Results.Ok(new { ok = true }));

app.MapPost("/api/licenses/activate", async (
    ActivateLicenseRequest request,
    BuildTrackDbContext db,
    ITenantContext tenantContext,
    CancellationToken ct) =>
{
    var tenantId = RequireTenantId(tenantContext);
    if (string.IsNullOrWhiteSpace(request.LicenseKey)) return Results.BadRequest(new { error = "License key is required" });
    var hash = LicenseKeyHasher.HashLicenseKey(request.LicenseKey);
    var license = await db.Licenses.FirstOrDefaultAsync(x => x.LicenseKeyHash == hash, ct);
    if (license is null || license.TenantId != tenantId) return Results.BadRequest(new { error = "License key is not valid for this tenant" });
    if (license.Status is LicenseStatus.Revoked or LicenseStatus.Expired) return Results.BadRequest(new { error = "License cannot be activated" });

    license.Status = LicenseStatus.Active;
    license.ActivatedAt = DateTimeOffset.UtcNow;
    license.StartsAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct);
    return Results.Ok(ToLicenseResponse(license));
});

app.MapGet("/api/admin/licenses", async (
    BuildTrackDbContext db,
    ITenantContext tenantContext,
    CancellationToken ct) =>
{
    var currentTenantId = RequireTenantId(tenantContext);
    var currentTenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == currentTenantId, ct);
    if (currentTenant?.Code != "DEMO" || !IsAdminRole(tenantContext.Role)) return Results.Forbid();

    var tenants = await db.Tenants.AsNoTracking().OrderBy(x => x.CompanyName).ToListAsync(ct);
    var tenantIds = tenants.Select(x => x.Id).ToArray();
    var users = await db.Users.AsNoTracking()
        .Where(x => tenantIds.Contains(x.TenantId))
        .OrderBy(x => x.Role == BuildTrackUserRole.Owner ? 0 : 1)
        .ThenBy(x => x.CreatedAt)
        .ToListAsync(ct);
    var licenses = await db.Licenses.AsNoTracking()
        .Where(x => tenantIds.Contains(x.TenantId))
        .OrderByDescending(x => x.Status == LicenseStatus.Active)
        .ThenByDescending(x => x.ActivatedAt ?? x.CreatedAt)
        .ToListAsync(ct);

    var ownerEmailByTenant = users
        .GroupBy(x => x.TenantId)
        .ToDictionary(group => group.Key, group => group.FirstOrDefault()?.Email);
    var licenseByTenant = licenses
        .GroupBy(x => x.TenantId)
        .ToDictionary(group => group.Key, group => group.FirstOrDefault());

    return Results.Ok(tenants.Select(tenant =>
    {
        licenseByTenant.TryGetValue(tenant.Id, out var license);
        return new AdminTenantLicenseResponse(
            tenant.Id,
            tenant.CompanyName,
            ownerEmailByTenant.GetValueOrDefault(tenant.Id),
            tenant.Status,
            license?.Plan,
            license?.Status,
            license?.ExpiresAt,
            license?.MaxProjects,
            license?.MaxUsers,
            license?.MaxCameras,
            tenant.CreatedAt,
            license?.Id);
    }));
});

app.MapPost("/api/admin/licenses", async (
    CreateLicenseRequest request,
    BuildTrackDbContext db,
    ITenantContext tenantContext,
    CancellationToken ct) =>
{
    var currentTenantId = RequireTenantId(tenantContext);
    var currentTenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == currentTenantId, ct);
    if (currentTenant?.Code != "DEMO" || !IsAdminRole(tenantContext.Role)) return Results.Forbid();

    var tenantExists = await db.Tenants.AnyAsync(x => x.Id == request.TenantId, ct);
    if (!tenantExists) return Results.NotFound(new { error = "Tenant was not found" });

    string rawKey;
    string hash;
    do
    {
        rawKey = LicenseKeyHasher.GenerateRawLicenseKey();
        hash = LicenseKeyHasher.HashLicenseKey(rawKey);
    } while (await db.Licenses.AnyAsync(x => x.LicenseKeyHash == hash, ct));

    var license = new License
    {
        TenantId = request.TenantId,
        LicenseKeyHash = hash,
        Plan = request.Plan,
        Status = LicenseStatus.Pending,
        StartsAt = DateTimeOffset.UtcNow,
        ExpiresAt = request.ExpiresAt,
        MaxProjects = request.MaxProjects,
        MaxUsers = request.MaxUsers,
        MaxCameras = request.MaxCameras,
    };
    db.Licenses.Add(license);
    await db.SaveChangesAsync(ct);
    return Results.Ok(new CreateLicenseResponse(rawKey, ToLicenseResponse(license)));
});

app.MapPost("/api/admin/licenses/{tenantId:guid}/activate", async (
    Guid tenantId,
    AdminActivateTenantLicenseRequest request,
    BuildTrackDbContext db,
    ITenantContext tenantContext,
    CancellationToken ct) =>
{
    var currentTenantId = RequireTenantId(tenantContext);
    var currentTenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == currentTenantId, ct);
    if (currentTenant?.Code != "DEMO" || !IsAdminRole(tenantContext.Role)) return Results.Forbid();

    var tenantExists = await db.Tenants.AnyAsync(x => x.Id == tenantId, ct);
    if (!tenantExists) return Results.NotFound(new { error = "Tenant was not found" });

    var license = request.LicenseId is not null
        ? await db.Licenses.FirstOrDefaultAsync(x => x.Id == request.LicenseId && x.TenantId == tenantId, ct)
        : await db.Licenses
            .Where(x => x.TenantId == tenantId && x.Status != LicenseStatus.Revoked)
            .OrderByDescending(x => x.Status == LicenseStatus.Pending)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

    if (license is null) return Results.NotFound(new { error = "License was not found" });
    if (license.Status == LicenseStatus.Revoked) return Results.BadRequest(new { error = "License cannot be activated" });

    license.Status = LicenseStatus.Active;
    license.ActivatedAt = DateTimeOffset.UtcNow;
    license.StartsAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct);
    return Results.Ok(ToLicenseResponse(license));
});

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

app.MapPost("/api/sites", async (CreateSiteRequest request, BuildTrackDbContext db, ITenantContext tenantContext, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { error = "Site name is required" });
    var site = new Site
    {
        TenantId = RequireTenantId(tenantContext),
        Name = request.Name.Trim(),
        Address = request.Address?.Trim() ?? string.Empty,
        TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Asia/Baku" : request.TimeZone.Trim(),
    };
    db.Sites.Add(site);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/sites/{site.Id}", site);
});

app.MapGet("/api/workers", async (Guid? siteId, BuildTrackDbContext db, ILoggerFactory loggerFactory, CancellationToken ct) =>
{
    var workers = await db.Workers
        .AsNoTracking()
        .Include(x => x.CameraIdentities)
        .Include(x => x.SiteAssignments)
        .OrderBy(x => x.FullName)
        .ToListAsync(ct);
    if (siteId is not null)
    {
        var assignedWorkerIds = (await db.WorkerSiteAssignments
            .AsNoTracking()
            .Where(x => x.SiteId == siteId.Value && x.Status == WorkerSiteAssignmentStatus.Active)
            .Select(x => x.WorkerId)
            .ToListAsync(ct)).ToHashSet();
        var siteSessions = await db.AttendanceSessions
            .AsNoTracking()
            .Where(x => x.SiteId == siteId.Value)
            .ToListAsync(ct);
        workers = workers
            .Where(worker => assignedWorkerIds.Contains(worker.Id) || siteSessions.Any(session => SessionMatchesWorker(session, worker)))
            .ToList();
    }
    return Results.Ok(await MapWorkerResponsesAsync(db, workers, siteId, loggerFactory.CreateLogger("WorkerStats"), ct));
});

app.MapPost("/api/workers", async (CreateWorkerRequest request, BuildTrackDbContext db, ITenantContext tenantContext, IWorkerCameraIdentityResolver identityResolver, IWorkerSiteAssignmentService siteAssignmentService, CancellationToken ct) =>
{
    if (!await db.Sites.AnyAsync(x => x.Id == request.SiteId, ct)) return Results.BadRequest(new { error = "Site was not found" });
    if (string.IsNullOrWhiteSpace(request.ExternalWorkerCode) || string.IsNullOrWhiteSpace(request.FullName))
    {
        return Results.BadRequest(new { error = "External worker code and full name are required" });
    }

    var worker = new Worker
    {
        TenantId = RequireTenantId(tenantContext),
        SiteId = request.SiteId,
        ExternalWorkerCode = request.ExternalWorkerCode.Trim(),
        FullName = request.FullName.Trim(),
        Brigade = Clean(request.Brigade),
        Role = Clean(request.Role),
        HourlyRate = Math.Max(0, request.HourlyRate),
        PlannedDailyHours = request.PlannedDailyHours <= 0 ? 8 : request.PlannedDailyHours,
        AttendanceSource = NormalizeAttendanceSourceForWorker(request.AttendanceSource, request.CameraIdentity),
        RiskScore = Math.Clamp(request.RiskScore, 0, 100),
        Notes = Clean(request.Notes),
        Status = request.Status,
    };
    db.Workers.Add(worker);
    await db.SaveChangesAsync(ct);
    await SyncWorkerSiteAssignmentsFromRequestAsync(siteAssignmentService, worker.Id, request.SiteId, request.SiteAssignments, ct);
    if (request.CameraIdentity is not null && HasCameraIdentityValues(request.CameraIdentity))
    {
        var identity = await identityResolver.UpsertAsync(worker.Id, request.CameraIdentity.DeviceId, request.CameraIdentity.ExternalUserId, request.CameraIdentity.CardName, request.CameraIdentity.IsPrimary, ct);
        await identityResolver.RemapRecentAsync(worker.Id, identity.Id, ct);
    }

    var responseWorker = await db.Workers.AsNoTracking().Include(x => x.CameraIdentities).Include(x => x.SiteAssignments).FirstAsync(x => x.Id == worker.Id, ct);
    var response = (await MapWorkerResponsesAsync(db, [responseWorker], null, null, ct)).Single();
    return Results.Created($"/api/workers/{worker.Id}", response);
});

app.MapPut("/api/workers/{id:guid}", async (Guid id, UpdateWorkerRequest request, BuildTrackDbContext db, IWorkerCameraIdentityResolver identityResolver, IWorkerSiteAssignmentService siteAssignmentService, CancellationToken ct) =>
{
    var worker = await db.Workers.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (worker is null) return Results.NotFound();
    if (!await db.Sites.AnyAsync(x => x.Id == request.SiteId, ct)) return Results.BadRequest(new { error = "Site was not found" });
    if (string.IsNullOrWhiteSpace(request.ExternalWorkerCode) || string.IsNullOrWhiteSpace(request.FullName))
    {
        return Results.BadRequest(new { error = "External worker code and full name are required" });
    }

    worker.SiteId = request.SiteId;
    worker.ExternalWorkerCode = request.ExternalWorkerCode.Trim();
    worker.FullName = request.FullName.Trim();
    worker.Brigade = Clean(request.Brigade);
    worker.Role = Clean(request.Role);
    worker.HourlyRate = Math.Max(0, request.HourlyRate);
    worker.PlannedDailyHours = request.PlannedDailyHours <= 0 ? 8 : request.PlannedDailyHours;
    worker.AttendanceSource = NormalizeAttendanceSourceForWorker(request.AttendanceSource, request.CameraIdentity);
    worker.RiskScore = Math.Clamp(request.RiskScore, 0, 100);
    worker.Notes = Clean(request.Notes);
    worker.Status = request.Status;
    worker.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct);
    await SyncWorkerSiteAssignmentsFromRequestAsync(siteAssignmentService, worker.Id, request.SiteId, request.SiteAssignments, ct);

    if (request.CameraIdentity is not null && HasCameraIdentityValues(request.CameraIdentity))
    {
        var identity = await identityResolver.UpsertAsync(worker.Id, request.CameraIdentity.DeviceId, request.CameraIdentity.ExternalUserId, request.CameraIdentity.CardName, request.CameraIdentity.IsPrimary, ct);
        await identityResolver.RemapRecentAsync(worker.Id, identity.Id, ct);
    }

    var responseWorker = await db.Workers.AsNoTracking().Include(x => x.CameraIdentities).Include(x => x.SiteAssignments).FirstAsync(x => x.Id == worker.Id, ct);
    var response = (await MapWorkerResponsesAsync(db, [responseWorker], null, null, ct)).Single();
    return Results.Ok(response);
});

app.MapDelete("/api/workers/{id:guid}", async (Guid id, BuildTrackDbContext db, CancellationToken ct) =>
{
    var worker = await db.Workers.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (worker is null) return Results.NotFound();
    db.Workers.Remove(worker);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

app.MapPost("/api/workers/{id:guid}/camera-identities", async (Guid id, SaveWorkerCameraIdentityRequest request, IWorkerCameraIdentityResolver identityResolver, CancellationToken ct) =>
{
    var identity = await identityResolver.UpsertAsync(id, request.DeviceId, request.ExternalUserId, request.CardName, request.IsPrimary, ct);
    return Results.Ok(new { identity.Id, identity.WorkerId, identity.DeviceId, identity.ExternalUserId, identity.CardName, identity.NormalizedCardName, identity.IsPrimary });
});

app.MapPost("/api/workers/{id:guid}/camera-identities/test", async (Guid id, TestWorkerCameraIdentityRequest request, BuildTrackDbContext db, IWorkerCameraIdentityResolver identityResolver, CancellationToken ct) =>
{
    var worker = await db.Workers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    if (worker is null) return Results.NotFound();
    var device = request.DeviceId is not null
        ? await db.Devices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.DeviceId.Value, ct)
        : await db.Devices.AsNoTracking().Where(x => x.SiteId == worker.SiteId).OrderBy(x => x.Name).FirstOrDefaultAsync(ct);
    if (device is null) return Results.BadRequest(new { error = "Device was not found" });
    var record = new DahuaAccessRecord
    {
        UserId = Clean(request.ExternalUserId),
        CardName = Clean(request.CardName),
        StatusRaw = "1",
        MethodRaw = "15",
        Type = "Entry",
        RawFields = new Dictionary<string, string?>
        {
            ["UserID"] = Clean(request.ExternalUserId),
            ["CardName"] = Clean(request.CardName),
            ["ReceivedCardName"] = Clean(request.CardName),
            ["Status"] = "1",
            ["Method"] = "15",
            ["Type"] = "Entry",
        },
    };
    var resolution = await identityResolver.ResolveAsync(device, record, ct);
    return Results.Ok(new TestWorkerCameraIdentityResponse(
        resolution.Worker?.Id == worker.Id,
        resolution.Worker?.Id,
        resolution.Worker?.FullName,
        resolution.Worker?.ExternalWorkerCode,
        resolution.ResolvedBy,
        resolution.Status,
        resolution.Reason));
});

app.MapPost("/api/workers/{id:guid}/camera-identities/remap", async (Guid id, Guid? identityId, IWorkerCameraIdentityResolver identityResolver, CancellationToken ct) =>
{
    var result = await identityResolver.RemapRecentAsync(id, identityId, ct);
    return Results.Ok(new WorkerCameraIdentityRemapResponse(result.AttendanceEventsUpdated, result.AttendanceSessionsUpdated));
});

app.MapPost("/api/workers/{id:guid}/remap-camera-events", async (Guid id, Guid? identityId, IWorkerCameraIdentityResolver identityResolver, CancellationToken ct) =>
{
    var result = await identityResolver.RemapRecentAsync(id, identityId, ct);
    return Results.Ok(new WorkerCameraIdentityRemapResponse(result.AttendanceEventsUpdated, result.AttendanceSessionsUpdated));
});

app.MapGet("/api/worker-camera-identities", async (BuildTrackDbContext db, CancellationToken ct) =>
{
    var identities = await db.WorkerCameraIdentities
        .AsNoTracking()
        .Include(x => x.Worker)
        .Include(x => x.Device)
        .OrderBy(x => x.Worker!.FullName)
        .ThenBy(x => x.CardName)
        .ToListAsync(ct);

    return Results.Ok(identities.Select(identity => new WorkerCameraIdentityResponse(
        identity.Id,
        identity.WorkerId,
        identity.DeviceId,
        identity.Device?.Name,
        identity.Vendor,
        identity.ExternalUserId,
        identity.CardName,
        identity.NormalizedCardName,
        identity.IsPrimary,
        identity.CreatedAt,
        identity.UpdatedAt)).ToArray());
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
    ITenantContext tenantContext,
    CancellationToken ct) =>
{
    if (!await db.Sites.AnyAsync(x => x.Id == request.SiteId, ct)) return Results.BadRequest(new { error = "Site was not found" });
    var tenantId = RequireTenantId(tenantContext);
    var tenantCode = await db.Tenants.AsNoTracking()
        .Where(x => x.Id == tenantId)
        .Select(x => x.Code)
        .FirstOrDefaultAsync(ct) ?? "TENANT";
    var registerDeviceId = string.IsNullOrWhiteSpace(request.RegisterDeviceId)
        ? await GenerateRegisterDeviceIdAsync(db, tenantCode, ct)
        : request.RegisterDeviceId.Trim();

    var device = new Device
    {
        TenantId = tenantId,
        SiteId = request.SiteId,
        Name = string.IsNullOrWhiteSpace(request.Name) ? registerDeviceId : request.Name.Trim(),
        Vendor = string.IsNullOrWhiteSpace(request.Vendor) ? "dahua" : request.Vendor.Trim().ToLowerInvariant(),
        Model = string.IsNullOrWhiteSpace(request.Model) ? "DHI-ASI6213J-MW" : request.Model.Trim(),
        Mode = request.Mode,
        RegisterDeviceId = registerDeviceId,
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
    events = events.Where(IsRecognizedAttendancePayload).ToList();
    return await MapAttendanceEventsAsync(db, events, ct);
});

app.MapGet("/api/attendance-events/{eventId:guid}/snapshot", async (Guid eventId, BuildTrackDbContext db, IConfiguration configuration, CancellationToken ct) =>
{
    var attendanceEvent = await db.AttendanceEvents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == eventId, ct);
    if (attendanceEvent is null || string.IsNullOrWhiteSpace(attendanceEvent.SnapshotPath)) return Results.NotFound();

    var storageRoot = configuration["SECURITY_SNAPSHOT_STORAGE_PATH"];
    if (string.IsNullOrWhiteSpace(storageRoot)) storageRoot = "/app/data/security-snapshots";
    var root = Path.GetFullPath(storageRoot);
    var candidate = Path.GetFullPath(attendanceEvent.SnapshotPath);

    if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return Results.NotFound();
    if (!File.Exists(candidate)) return Results.NotFound();

    var bytes = await File.ReadAllBytesAsync(candidate, ct);
    return Results.File(bytes, "image/jpeg");
});

app.MapGet("/api/snapshots/{**relativePath}", async (string relativePath, BuildTrackDbContext db, IConfiguration configuration, ITenantContext tenantContext, CancellationToken ct) =>
{
    if (tenantContext.TenantId is null) return Results.Unauthorized();

    var storageRoot = configuration["SECURITY_SNAPSHOT_STORAGE_PATH"];
    if (string.IsNullOrWhiteSpace(storageRoot)) storageRoot = SnapshotPathPolicy.DefaultStorageRoot;
    if (!SnapshotPathPolicy.TryResolveLocalPath(storageRoot, relativePath, out var localPath)) return Results.NotFound();
    if (!File.Exists(localPath)) return Results.NotFound();

    var tenantOwnsSnapshot = await SnapshotReferenceExistsAsync(db, relativePath, tenantContext.TenantId.Value, ct);
    if (!tenantOwnsSnapshot)
    {
        var referencedByAnotherTenant = await SnapshotReferenceExistsAsync(db, relativePath, null, ct);
        return referencedByAnotherTenant ? Results.StatusCode(StatusCodes.Status403Forbidden) : Results.NotFound();
    }

    var bytes = await File.ReadAllBytesAsync(localPath, ct);
    return Results.File(bytes, "image/jpeg");
});

app.MapGet("/api/attendance-events/snapshots", async (Guid? siteId, Guid? deviceId, string? workerExternalId, string? date, BuildTrackDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(workerExternalId)) return Results.BadRequest(new { error = "workerExternalId is required" });
    if (string.IsNullOrWhiteSpace(date)) return Results.BadRequest(new { error = "date is required" });

    var site = siteId is null ? null : await db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Id == siteId.Value, ct);
    if (siteId is not null && site is null) return Results.NotFound(new { error = "site not found" });
    var timeZone = ResolveApiTimeZone(site?.TimeZone ?? "Asia/Baku");
    var workDate = DateOnly.TryParse(date, out var parsedDate)
        ? parsedDate
        : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
    var (dayStartUtc, dayEndUtc) = GetUtcRangeForWorkDate(workDate, timeZone);

    var mappedWorker = siteId is null
        ? await db.Workers.AsNoTracking().FirstOrDefaultAsync(x => x.ExternalWorkerCode == workerExternalId && x.Status == WorkerStatus.Active, ct)
        : await db.Workers.AsNoTracking().FirstOrDefaultAsync(x => x.SiteId == siteId.Value && x.ExternalWorkerCode == workerExternalId && x.Status == WorkerStatus.Active, ct);

    var query = db.AttendanceEvents.AsNoTracking()
        .Where(x => x.WorkerExternalId == workerExternalId
                    && x.CreatedAt >= dayStartUtc
                    && x.CreatedAt < dayEndUtc
                    && x.Status == AttendanceEventStatus.Ok
                    && x.Method == AttendanceMethod.Face
                    && x.Source == DahuaEventSourceExtensions.ActiveRegisterSource
                    && x.SnapshotPath != null);
    if (siteId is not null) query = query.Where(x => x.SiteId == siteId.Value);
    if (deviceId is not null) query = query.Where(x => x.DeviceId == deviceId.Value);

    var events = await query.OrderBy(x => x.CreatedAt).ToListAsync(ct);

    events = events
        .Where(IsRecognizedAttendancePayload)
        .Where(x => mappedWorker is null || string.Equals(x.WorkerName, mappedWorker.FullName, StringComparison.OrdinalIgnoreCase))
        .ToList();

    return Results.Ok(events.Select(attendanceEvent => new AttendanceSnapshotResponse(
        attendanceEvent.Id,
        AttendanceEventOperationalClock.Resolve(attendanceEvent),
        FormatLocalTime(AttendanceEventOperationalClock.Resolve(attendanceEvent), timeZone),
        BuildSnapshotUrl(attendanceEvent.SnapshotPath),
        attendanceEvent.Method,
        attendanceEvent.Source)).ToArray());
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
    sessions = await FilterVerifiedAttendanceSessionsAsync(db, sessions, ct);
    var staleOpenSessionsCount = await db.AttendanceSessions.AsNoTracking()
        .CountAsync(x => x.SiteId == siteId && x.WorkDate < workDate && x.Status == AttendanceSessionStatus.Open, ct);
    var liveWorkerCodes = sessions.Select(x => x.WorkerExternalId).Distinct().ToArray();
    var liveWorkersByCode = liveWorkerCodes.Length == 0
        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        : await db.Workers.AsNoTracking()
            .Where(x => x.SiteId == siteId && liveWorkerCodes.Contains(x.ExternalWorkerCode) && x.Status == WorkerStatus.Active)
            .ToDictionaryAsync(x => x.ExternalWorkerCode, x => x.FullName, StringComparer.OrdinalIgnoreCase, ct);

    var workers = AttendanceSessionPlanner.SelectCurrentOpenSessions(sessions, workDate)
        .GroupBy(session => new { session.DeviceId, session.WorkerExternalId, session.WorkDate })
        .Select(group => group.OrderBy(session => session.CheckInTime).First())
        .OrderBy(session => session.CheckInTime)
        .Select(session =>
        {
            var lastSeenTime = session.LastSeenTime ?? session.CheckInTime;
            return new AttendanceLiveWorkerResponse(
                session.WorkerExternalId,
                liveWorkersByCode.TryGetValue(session.WorkerExternalId, out var mappedName) ? mappedName : session.WorkerName,
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

    var sessionWorkerCodes = workers
        .Select(x => x.WorkerExternalId)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var (dayStartUtc, dayEndUtc) = GetUtcRangeForWorkDate(workDate, timeZone);
    var activeRegisterEvents = await db.AttendanceEvents.AsNoTracking()
        .Where(x => x.SiteId == siteId
                    && x.Source == DahuaEventSourceExtensions.ActiveRegisterSource
                    && x.CreatedAt >= dayStartUtc
                    && x.CreatedAt < dayEndUtc
                    && x.Status == AttendanceEventStatus.Ok
                    && x.Method == AttendanceMethod.Face
                    && x.WorkerExternalId != null)
        .OrderBy(x => x.CreatedAt)
        .ToListAsync(ct);
    activeRegisterEvents = activeRegisterEvents
        .Where(IsRecognizedAttendancePayload)
        .Where(x => !sessionWorkerCodes.Contains(x.WorkerExternalId!))
        .ToList();

    var activeRegisterWorkerCodes = activeRegisterEvents
        .Select(x => x.WorkerExternalId!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var activeRegisterWorkersByCode = activeRegisterWorkerCodes.Length == 0
        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        : await db.Workers.AsNoTracking()
            .Where(x => x.SiteId == siteId && activeRegisterWorkerCodes.Contains(x.ExternalWorkerCode) && x.Status == WorkerStatus.Active)
            .ToDictionaryAsync(x => x.ExternalWorkerCode, x => x.FullName, StringComparer.OrdinalIgnoreCase, ct);

    var activeRegisterWorkerRows = activeRegisterEvents
        .GroupBy(x => x.WorkerExternalId!, StringComparer.OrdinalIgnoreCase)
        .Select(group =>
        {
            var ordered = group.OrderBy(AttendanceEventOperationalClock.Resolve).ToArray();
            var first = ordered.First();
            var last = ordered.Last();
            var firstSeen = AttendanceEventOperationalClock.Resolve(first);
            var lastSeen = AttendanceEventOperationalClock.Resolve(last);
            return new AttendanceLiveWorkerResponse(
                first.WorkerExternalId ?? string.Empty,
                first.WorkerExternalId is not null && activeRegisterWorkersByCode.TryGetValue(first.WorkerExternalId, out var mappedName)
                    ? mappedName
                    : ordered.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.WorkerName))?.WorkerName ?? first.WorkerName,
                firstSeen,
                FormatLocalTime(firstSeen, timeZone),
                lastSeen,
                FormatLocalTime(lastSeen, timeZone),
                null,
                null,
                null,
                AttendanceSessionPlanner.BuildDisplayStatus(AttendanceSessionStatus.Open, null, lastSeen, now),
                false,
                Math.Max(0, (int)Math.Floor((lastSeen - firstSeen).TotalMinutes)),
                AttendanceSessionStatus.Open);
        })
        .ToArray();

    var combinedWorkers = workers.Concat(activeRegisterWorkerRows).OrderBy(x => x.CheckInTime).ToArray();

    return Results.Ok(new AttendanceLiveStatusResponse(workDate, combinedWorkers.Length, combinedWorkers, staleOpenSessionsCount));
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
    sessions = await FilterVerifiedAttendanceSessionsAsync(db, sessions, ct);

    if (sessions.Count == 0)
    {
        var (dayStartUtc, dayEndUtc) = GetUtcRangeForWorkDate(workDate, timeZone);
        var events = await db.AttendanceEvents.AsNoTracking()
            .Where(x => x.SiteId == siteId
                        && x.Status == AttendanceEventStatus.Ok
                        && x.WorkerExternalId != null
                        && ((x.Source == DahuaEventSourceExtensions.ActiveRegisterSource
                             && x.CreatedAt >= dayStartUtc
                             && x.CreatedAt < dayEndUtc)
                            || (x.Source != DahuaEventSourceExtensions.ActiveRegisterSource
                                && x.EventTime >= dayStartUtc
                                && x.EventTime < dayEndUtc)))
            .OrderBy(x => x.EventTime)
            .ToListAsync(ct);
        var fallbackWorkerCodes = events.Select(x => x.WorkerExternalId).Where(x => x is not null).Distinct().ToArray();
        var fallbackWorkers = fallbackWorkerCodes.Length == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : await db.Workers.AsNoTracking()
                .Where(x => x.SiteId == siteId && fallbackWorkerCodes.Contains(x.ExternalWorkerCode) && x.Status == WorkerStatus.Active)
                .ToDictionaryAsync(x => x.ExternalWorkerCode, x => x.FullName, StringComparer.OrdinalIgnoreCase, ct);
        events = events
            .Where(x => x.Source != DahuaEventSourceExtensions.ActiveRegisterSource || IsRecognizedAttendancePayload(x))
            .Where(x => x.Source != DahuaEventSourceExtensions.ActiveRegisterSource
                        || x.WorkerExternalId is null
                        || !fallbackWorkers.TryGetValue(x.WorkerExternalId, out var canonicalName)
                        || string.Equals(x.WorkerName, canonicalName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var fallbackRows = events
            .GroupBy(x => x.WorkerExternalId!)
            .Select(group =>
            {
                var ordered = group.OrderBy(AttendanceEventOperationalClock.Resolve).ToArray();
                var first = ordered.First();
                var last = ordered.Last();
                var firstSeen = AttendanceEventOperationalClock.Resolve(first);
                var lastSeen = AttendanceEventOperationalClock.Resolve(last);
                var workedMinutes = Math.Max(0, (int)Math.Floor((lastSeen - firstSeen).TotalMinutes));
                return new AttendanceSessionResponse(
                    first.Id,
                    first.WorkerExternalId ?? string.Empty,
                    first.WorkerExternalId is not null && fallbackWorkers.TryGetValue(first.WorkerExternalId, out var mappedName)
                        ? mappedName
                        : ordered.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.WorkerName))?.WorkerName ?? first.WorkerName,
                    firstSeen,
                    null,
                    FormatLocalTime(firstSeen, timeZone),
                    null,
                    lastSeen,
                    FormatLocalTime(lastSeen, timeZone),
                    null,
                    null,
                    null,
                    lastSeen >= DateTimeOffset.UtcNow.AddMinutes(-15) ? "Az əvvəl göründü" : "Bugün görünüb",
                    false,
                    workedMinutes,
                    AttendanceSessionStatus.Open,
                    first.Source,
                    first.Method,
                    PublicSnapshotPath(first.SnapshotPath),
                    BuildSnapshotUrl(first.SnapshotPath));
            })
            .OrderBy(x => x.CheckInTime)
            .ToArray();

        return Results.Ok(new AttendanceDailyResponse(
            workDate,
            fallbackRows.Length,
            fallbackRows.Length,
            0,
            Math.Round(fallbackRows.Sum(x => x.WorkedMinutes) / 60d, 2),
            fallbackRows));
    }

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
    var dailyWorkerCodes = dailySessions.Select(x => x.Session.WorkerExternalId).Distinct().ToArray();
    var dailyWorkersByCode = dailyWorkerCodes.Length == 0
        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        : await db.Workers.AsNoTracking()
            .Where(x => x.SiteId == siteId && dailyWorkerCodes.Contains(x.ExternalWorkerCode) && x.Status == WorkerStatus.Active)
            .ToDictionaryAsync(x => x.ExternalWorkerCode, x => x.FullName, StringComparer.OrdinalIgnoreCase, ct);

    var eventIds = dailySessions
        .SelectMany(row => new[] { row.Session.LastSeenEventId, row.Session.CheckOutEventId, row.Session.CheckInEventId })
        .Where(id => id is not null)
        .Select(id => id!.Value)
        .Distinct()
        .ToArray();
    var eventsById = eventIds.Length == 0
        ? new Dictionary<Guid, AttendanceEvent>()
        : await db.AttendanceEvents.AsNoTracking().Where(x => eventIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

    var sessionRows = dailySessions.Select(row =>
    {
        var confirmedCheckoutTime = IsCheckoutConfirmed(row.Session) ? row.CheckOutTime : null;
        var lastSeenTime = row.LastSeenTime;
        var effectiveEnd = confirmedCheckoutTime ?? lastSeenTime;
        var workedMinutes = Math.Max(0, (int)Math.Floor((effectiveEnd - row.CheckInTime).TotalMinutes));
        var snapshotEvent = eventsById.GetValueOrDefault(row.Session.CheckInEventId)
            ?? (row.Session.CheckOutEventId is not null && eventsById.TryGetValue(row.Session.CheckOutEventId.Value, out var checkoutEvent) ? checkoutEvent : null)
            ?? (row.Session.LastSeenEventId is not null && eventsById.TryGetValue(row.Session.LastSeenEventId.Value, out var lastSeenEvent) ? lastSeenEvent : null);
        return new AttendanceSessionResponse(
            row.Session.Id,
            row.Session.WorkerExternalId,
            dailyWorkersByCode.TryGetValue(row.Session.WorkerExternalId, out var mappedName) ? mappedName : row.WorkerName,
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
            row.Source,
            snapshotEvent?.Method,
            snapshotEvent is null ? null : PublicSnapshotPath(snapshotEvent.SnapshotPath),
            snapshotEvent is null ? null : BuildSnapshotUrl(snapshotEvent.SnapshotPath));
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

    return Results.Ok(events.Select(securityEvent =>
    {
        var (cameraUserId, cameraCardName) = ExtractCameraIdentity(securityEvent.RawPayloadJson);
        return new SecurityEventResponse(
            securityEvent.Id,
            securityEvent.EventTime,
            FormatLocalTime(securityEvent.EventTime, timeZone),
            securityEvent.EventType,
            securityEvent.Severity,
            securityEvent.Status,
            deviceNames.GetValueOrDefault(securityEvent.DeviceId),
            site.Name,
            PublicSnapshotPath(securityEvent.StoredSnapshotPath) ?? PublicSnapshotPath(securityEvent.SnapshotPath),
            BuildSnapshotUrl(securityEvent.StoredSnapshotPath) ?? BuildSnapshotUrl(securityEvent.SnapshotPath),
            securityEvent.SnapshotDownloadStatus,
            securityEvent.SnapshotDownloadError,
            securityEvent.SnapshotSource,
            securityEvent.Message,
            ShouldExposeSecurityRecNo(securityEvent) ? securityEvent.RawRecNo : null,
            cameraUserId,
            cameraCardName);
    }).ToArray());
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

app.MapPost("/api/security-events/{eventId:guid}/link-worker", async (
    Guid eventId,
    LinkSecurityEventToWorkerRequest request,
    BuildTrackDbContext db,
    IWorkerCameraIdentityResolver identityResolver,
    CancellationToken ct) =>
{
    var securityEvent = await db.SecurityEvents.FirstOrDefaultAsync(x => x.Id == eventId, ct);
    if (securityEvent is null) return Results.NotFound();
    var worker = await db.Workers.FirstOrDefaultAsync(x => x.Id == request.WorkerId, ct);
    if (worker is null) return Results.BadRequest(new { error = "Worker was not found" });
    if (worker.TenantId != securityEvent.TenantId || worker.SiteId != securityEvent.SiteId)
    {
        return Results.BadRequest(new { error = "Worker belongs to another tenant or site" });
    }

    var (cameraUserId, cameraCardName) = ExtractCameraIdentity(securityEvent.RawPayloadJson);
    var identity = await identityResolver.UpsertAsync(
        worker.Id,
        request.DeviceId ?? securityEvent.DeviceId,
        cameraUserId,
        cameraCardName,
        true,
        ct);
    var remap = request.RemapRecent
        ? await identityResolver.RemapRecentAsync(worker.Id, identity.Id, ct)
        : new WorkerCameraIdentityRemapResult(0, 0);

    securityEvent.Status = SecurityEventStatus.Reviewed;
    securityEvent.ReviewedAt = DateTimeOffset.UtcNow;
    securityEvent.ReviewNote = string.IsNullOrWhiteSpace(request.ReviewNote)
        ? $"Isciye baglandi: {worker.FullName} / {worker.ExternalWorkerCode}"
        : request.ReviewNote.Trim();
    await db.SaveChangesAsync(ct);

    return Results.Ok(new
    {
        securityEvent.Id,
        securityEvent.Status,
        workerId = worker.Id,
        worker.ExternalWorkerCode,
        worker.FullName,
        identityId = identity.Id,
        remap.AttendanceEventsUpdated,
        remap.AttendanceSessionsUpdated,
    });
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
    var now = DateTimeOffset.UtcNow;
    int? lastSmartEventAgeSeconds = diagnostics?.LastSmartEventAt is null ? null : Math.Max(0, (int)(now - diagnostics.LastSmartEventAt.Value).TotalSeconds);
    int? lastServiceCallbackAgeSeconds = diagnostics?.LastServiceCallbackAt is null ? null : Math.Max(0, (int)(now - diagnostics.LastServiceCallbackAt.Value).TotalSeconds);

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
            diagnostics.SmartEventEnabled,
            diagnostics.SmartEventNeedPicture,
            diagnostics.SmartEventChannel,
            diagnostics.SmartEventSubscriptionAttempted,
            diagnostics.SmartEventSubscriptionSuccess,
            diagnostics.SmartEventAttachHandle,
            diagnostics.SmartEventErrorSigned,
            diagnostics.SmartEventErrorHex,
            diagnostics.SmartEventSubscriptionGeneration,
            diagnostics.SmartEventSubscribedAt,
            diagnostics.SmartEventRemoteIp,
            diagnostics.SmartEventRemotePort,
            diagnostics.LastServiceCallbackAt,
            diagnostics.LastSmartEventAt,
            lastSmartEventAgeSeconds,
            lastServiceCallbackAgeSeconds,
            diagnostics.LastSmartEventResubscribeAt,
            diagnostics.LastSmartEventResubscribeReason,
            diagnostics.LastSmartEventResubscribeSuccess,
            diagnostics.LastSmartEventResubscribeError,
            diagnostics.StaleSmartEventDetected,
            diagnostics.SmartEventWatchdogEnabled,
            diagnostics.LastSmartEventType,
            diagnostics.LastSmartEventName,
            diagnostics.LastSmartEventPayloadBytes,
            diagnostics.LastSmartEventImageBytesLength,
            diagnostics.LastSmartEventParseStatus,
            diagnostics.LastSmartEventUserId,
            diagnostics.LastSmartEventCardName,
            diagnostics.LastSmartEventRecNo,
            diagnostics.LastSmartEventTime,
            diagnostics.LastSmartEventRawStructSummaryJson,
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
        smartEventEnabled = diagnostics?.SmartEventEnabled ?? IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_SMART_EVENT_ENABLED"], defaultValue: true),
        smartEventNeedPicture = diagnostics?.SmartEventNeedPicture ?? IsEnabled(configuration["DAHUA_ACTIVE_REGISTER_SMART_EVENT_NEED_PICTURE"], defaultValue: true),
        smartEventChannel = diagnostics?.SmartEventChannel ?? ParseInt(configuration["DAHUA_ACTIVE_REGISTER_SMART_EVENT_CHANNEL"], -1),
        smartEventSubscriptionAttempted = diagnostics?.SmartEventSubscriptionAttempted ?? false,
        smartEventSubscriptionSuccess = diagnostics?.SmartEventSubscriptionSuccess,
        smartEventAttachHandle = diagnostics?.SmartEventAttachHandle,
        smartEventErrorSigned = diagnostics?.SmartEventErrorSigned,
        smartEventErrorHex = diagnostics?.SmartEventErrorHex,
        smartEventSubscriptionGeneration = diagnostics?.SmartEventSubscriptionGeneration ?? 0,
        smartEventSubscribedAt = diagnostics?.SmartEventSubscribedAt,
        smartEventRemoteIp = diagnostics?.SmartEventRemoteIp,
        smartEventRemotePort = diagnostics?.SmartEventRemotePort,
        lastServiceCallbackAt = diagnostics?.LastServiceCallbackAt,
        lastSmartEventAt = diagnostics?.LastSmartEventAt,
        lastSmartEventAgeSeconds,
        lastServiceCallbackAgeSeconds,
        lastSmartEventResubscribeAt = diagnostics?.LastSmartEventResubscribeAt,
        lastSmartEventResubscribeReason = diagnostics?.LastSmartEventResubscribeReason,
        lastSmartEventResubscribeSuccess = diagnostics?.LastSmartEventResubscribeSuccess,
        lastSmartEventResubscribeError = diagnostics?.LastSmartEventResubscribeError,
        staleSmartEventDetected = diagnostics?.StaleSmartEventDetected ?? false,
        watchdogEnabled = diagnostics?.SmartEventWatchdogEnabled ?? IsEnabled(configuration["DAHUA_SMART_EVENT_WATCHDOG_ENABLED"], defaultValue: true),
        lastSmartEventType = diagnostics?.LastSmartEventType,
        lastSmartEventName = diagnostics?.LastSmartEventName,
        lastSmartEventPayloadBytes = diagnostics?.LastSmartEventPayloadBytes ?? 0,
        lastSmartEventImageBytesLength = diagnostics?.LastSmartEventImageBytesLength ?? 0,
        lastSmartEventParseStatus = diagnostics?.LastSmartEventParseStatus,
        lastSmartEventUserId = diagnostics?.LastSmartEventUserId,
        lastSmartEventCardName = diagnostics?.LastSmartEventCardName,
        lastSmartEventRecNo = diagnostics?.LastSmartEventRecNo,
        lastSmartEventTime = diagnostics?.LastSmartEventTime,
        lastSmartEventRawStructSummaryJson = diagnostics?.LastSmartEventRawStructSummaryJson,
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
        smartEventEnabled = persisted.SmartEventEnabled,
        smartEventNeedPicture = persisted.SmartEventNeedPicture,
        smartEventChannel = persisted.SmartEventChannel,
        smartEventSubscriptionAttempted = persisted.SmartEventSubscriptionAttempted,
        smartEventSubscriptionSuccess = persisted.SmartEventSubscriptionSuccess,
        smartEventAttachHandle = persisted.SmartEventAttachHandle,
        smartEventErrorSigned = persisted.SmartEventErrorSigned,
        smartEventErrorHex = persisted.SmartEventErrorHex,
        smartEventSubscriptionGeneration = persisted.SmartEventSubscriptionGeneration,
        smartEventSubscribedAt = persisted.SmartEventSubscribedAt,
        smartEventRemoteIp = persisted.SmartEventRemoteIp,
        smartEventRemotePort = persisted.SmartEventRemotePort,
        lastServiceCallbackAt = persisted.LastServiceCallbackAt,
        lastSmartEventAt = persisted.LastSmartEventAt,
        lastSmartEventAgeSeconds = persisted.LastSmartEventAt is null ? (int?)null : Math.Max(0, (int)(DateTimeOffset.UtcNow - persisted.LastSmartEventAt.Value).TotalSeconds),
        lastServiceCallbackAgeSeconds = persisted.LastServiceCallbackAt is null ? (int?)null : Math.Max(0, (int)(DateTimeOffset.UtcNow - persisted.LastServiceCallbackAt.Value).TotalSeconds),
        lastSmartEventResubscribeAt = persisted.LastSmartEventResubscribeAt,
        lastSmartEventResubscribeReason = persisted.LastSmartEventResubscribeReason,
        lastSmartEventResubscribeSuccess = persisted.LastSmartEventResubscribeSuccess,
        lastSmartEventResubscribeError = persisted.LastSmartEventResubscribeError,
        staleSmartEventDetected = persisted.StaleSmartEventDetected,
        watchdogEnabled = persisted.SmartEventWatchdogEnabled,
        lastSmartEventType = persisted.LastSmartEventType,
        lastSmartEventName = persisted.LastSmartEventName,
        lastSmartEventPayloadBytes = persisted.LastSmartEventPayloadBytes,
        lastSmartEventImageBytesLength = persisted.LastSmartEventImageBytesLength,
        lastSmartEventParseStatus = persisted.LastSmartEventParseStatus,
        lastSmartEventUserId = persisted.LastSmartEventUserId,
        lastSmartEventCardName = persisted.LastSmartEventCardName,
        lastSmartEventRecNo = persisted.LastSmartEventRecNo,
        lastSmartEventTime = persisted.LastSmartEventTime,
        lastSmartEventRawStructSummaryJson = persisted.LastSmartEventRawStructSummaryJson,
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
            ? "shimmer"
            : (configuration["OPENAI_TTS_VOICE"] ?? configuration["Ai:TtsVoice"] ?? "shimmer").Trim(),
        TtsFormat = string.IsNullOrWhiteSpace(configuration["OPENAI_TTS_FORMAT"] ?? configuration["Ai:TtsFormat"])
            ? "mp3"
            : (configuration["OPENAI_TTS_FORMAT"] ?? configuration["Ai:TtsFormat"] ?? "mp3").Trim(),
    };
}

static JwtOptions BuildJwtOptions(IConfiguration configuration)
{
    var secret = configuration["JWT_SECRET"] ?? configuration["Jwt:Secret"] ?? string.Empty;
    var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
    if (string.IsNullOrWhiteSpace(secret) && environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
    {
        secret = "buildtrack-local-development-secret-change-before-production";
    }

    return new JwtOptions
    {
        Secret = secret,
        Issuer = string.IsNullOrWhiteSpace(configuration["JWT_ISSUER"] ?? configuration["Jwt:Issuer"])
            ? "BuildTrack"
            : (configuration["JWT_ISSUER"] ?? configuration["Jwt:Issuer"] ?? "BuildTrack").Trim(),
        Audience = string.IsNullOrWhiteSpace(configuration["JWT_AUDIENCE"] ?? configuration["Jwt:Audience"])
            ? "BuildTrack.App"
            : (configuration["JWT_AUDIENCE"] ?? configuration["Jwt:Audience"] ?? "BuildTrack.App").Trim(),
        ExpiresMinutes = ParseInt(configuration["JWT_EXPIRES_MINUTES"] ?? configuration["Jwt:ExpiresMinutes"], 720),
    };
}

static bool IsApiPath(string path) => path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

static bool IsPublicApiPath(string path) =>
    path.Equals("/api/health", StringComparison.OrdinalIgnoreCase)
    || path.Equals("/api/auth/register", StringComparison.OrdinalIgnoreCase)
    || path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase);

static bool IsLicenseExemptPath(string path) =>
    path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase)
    || path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
    || path.Equals("/api/licenses/activate", StringComparison.OrdinalIgnoreCase);

static string ExtractBearerToken(HttpRequest request)
{
    var authorization = request.Headers.Authorization.ToString();
    const string prefix = "Bearer ";
    return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        ? authorization[prefix.Length..].Trim()
        : string.Empty;
}

static Guid RequireTenantId(ITenantContext tenantContext) =>
    tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is missing");

static Guid RequireUserId(ITenantContext tenantContext) =>
    tenantContext.UserId ?? throw new InvalidOperationException("User context is missing");

static bool IsAdminRole(string? role) =>
    string.Equals(role, BuildTrackUserRole.Owner.ToString(), StringComparison.OrdinalIgnoreCase)
    || string.Equals(role, BuildTrackUserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

static AuthUserResponse ToAuthUserResponse(AppUser user) =>
    new(user.Id, user.TenantId, user.FullName, user.Email, user.Role, user.Status);

static TenantResponse ToTenantResponse(Tenant tenant) =>
    new(tenant.Id, tenant.CompanyName, tenant.Code, tenant.Status);

static LicenseResponse ToLicenseResponse(License license) =>
    new(license.Id, license.TenantId, license.Plan, license.Status, license.StartsAt, license.ExpiresAt, license.MaxProjects, license.MaxUsers, license.MaxCameras);

static IResult CreateAuthResponseResult(IJwtTokenService tokenService, AppUser user, Tenant tenant, License? license)
{
    try
    {
        return Results.Ok(new AuthResponse(
            tokenService.CreateToken(user),
            ToAuthUserResponse(user),
            ToTenantResponse(tenant),
            license is null ? null : ToLicenseResponse(license)));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

static async Task<License?> GetCurrentLicenseAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct) =>
    await db.Licenses
        .Where(x => x.TenantId == tenantId)
        .OrderByDescending(x => x.Status == LicenseStatus.Active)
        .ThenByDescending(x => x.ActivatedAt ?? x.CreatedAt)
        .FirstOrDefaultAsync(ct);

static async Task<string> GenerateTenantCodeAsync(BuildTrackDbContext db, string companyName, CancellationToken ct)
{
    var chars = companyName
        .ToUpperInvariant()
        .Where(char.IsLetterOrDigit)
        .Take(10)
        .ToArray();
    var baseCode = chars.Length == 0 ? "TENANT" : new string(chars);
    var code = baseCode;
    for (var index = 2; await db.Tenants.AnyAsync(x => x.Code == code, ct); index++)
    {
        code = $"{baseCode}{index}";
    }

    return code;
}

static async Task<string> GenerateRegisterDeviceIdAsync(BuildTrackDbContext db, string tenantCode, CancellationToken ct)
{
    var safeTenantCode = new string((tenantCode ?? "TENANT").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    if (string.IsNullOrWhiteSpace(safeTenantCode)) safeTenantCode = "TENANT";
    for (var index = 1; index < 10_000; index++)
    {
        var candidate = $"BT-{safeTenantCode}-CAM{index:000}";
        var exists = await db.Devices.IgnoreQueryFilters().AnyAsync(x => x.RegisterDeviceId == candidate, ct);
        if (!exists) return candidate;
    }

    throw new InvalidOperationException("Could not generate a unique Dahua register device id");
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

static string? BuildSnapshotUrl(string? snapshotPath) =>
    SnapshotPathPolicy.TryCreateApiUrl(snapshotPath, out var snapshotUrl) ? snapshotUrl : null;

static string? PublicSnapshotPath(string? snapshotPath) =>
    SnapshotPathPolicy.TryCreateApiUrl(snapshotPath, out _) ? null : snapshotPath;

static bool ShouldExposeSecurityRecNo(SecurityEvent securityEvent) =>
    securityEvent.EventType is not (SecurityEventType.ParserUncertainSmartEvent or SecurityEventType.SuspiciousRecognition);

static async Task<bool> SnapshotReferenceExistsAsync(BuildTrackDbContext db, string relativePath, Guid? tenantId, CancellationToken ct)
{
    var normalized = relativePath.Trim().Replace('\\', '/').Trim('/');
    var slashSuffix = "/" + normalized;
    var backslashSuffix = "\\" + normalized.Replace('/', '\\');

    var attendanceQuery = db.AttendanceEvents.IgnoreQueryFilters().AsNoTracking();
    if (tenantId is not null) attendanceQuery = attendanceQuery.Where(x => x.TenantId == tenantId.Value);
    var attendanceExists = await attendanceQuery.AnyAsync(
        x => x.SnapshotPath != null
             && (x.SnapshotPath.EndsWith(slashSuffix)
                 || x.SnapshotPath.EndsWith(backslashSuffix)
                 || x.SnapshotPath == normalized),
        ct);
    if (attendanceExists) return true;

    var securityQuery = db.SecurityEvents.IgnoreQueryFilters().AsNoTracking();
    if (tenantId is not null) securityQuery = securityQuery.Where(x => x.TenantId == tenantId.Value);
    return await securityQuery.AnyAsync(
        x => (x.SnapshotPath != null
              && (x.SnapshotPath.EndsWith(slashSuffix)
                  || x.SnapshotPath.EndsWith(backslashSuffix)
                  || x.SnapshotPath == normalized))
             || (x.StoredSnapshotPath != null
                 && (x.StoredSnapshotPath.EndsWith(slashSuffix)
                     || x.StoredSnapshotPath.EndsWith(backslashSuffix)
                     || x.StoredSnapshotPath == normalized)),
        ct);
}

static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetUtcRangeForWorkDate(DateOnly workDate, TimeZoneInfo timeZone)
{
    var localStart = workDate.ToDateTime(TimeOnly.MinValue);
    var startOffset = timeZone.GetUtcOffset(localStart);
    var startUtc = new DateTimeOffset(localStart, startOffset).ToUniversalTime();
    return (startUtc, startUtc.AddDays(1));
}

static bool IsRecognizedAttendancePayload(AttendanceEvent attendanceEvent)
    => DahuaVerifiedAttendancePayload.IsVerifiedAttendance(attendanceEvent);

static async Task<List<AttendanceSession>> FilterVerifiedAttendanceSessionsAsync(
    BuildTrackDbContext db,
    IReadOnlyCollection<AttendanceSession> sessions,
    CancellationToken ct)
{
    if (sessions.Count == 0) return [];

    var eventIds = sessions
        .Where(session => string.Equals(session.Source, DahuaEventSourceExtensions.ActiveRegisterSource, StringComparison.OrdinalIgnoreCase))
        .SelectMany(session => new[] { (Guid?)session.CheckInEventId, session.LastSeenEventId, session.CheckOutEventId })
        .Where(id => id is not null)
        .Select(id => id!.Value)
        .Distinct()
        .ToArray();
    if (eventIds.Length == 0) return sessions.ToList();

    var eventsById = await db.AttendanceEvents.AsNoTracking()
        .Where(x => eventIds.Contains(x.Id))
        .ToDictionaryAsync(x => x.Id, ct);

    return sessions
        .Where(session =>
        {
            if (!string.Equals(session.Source, DahuaEventSourceExtensions.ActiveRegisterSource, StringComparison.OrdinalIgnoreCase)) return true;

            var linkedEventIds = new[] { (Guid?)session.CheckInEventId, session.LastSeenEventId, session.CheckOutEventId }
                .Where(id => id is not null)
                .Select(id => id!.Value)
                .ToArray();
            return linkedEventIds.Length > 0
                   && linkedEventIds.All(id => eventsById.TryGetValue(id, out var attendanceEvent)
                                                && IsRecognizedAttendancePayload(attendanceEvent));
        })
        .ToList();
}

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

static async Task<WorkerResponse[]> MapWorkerResponsesAsync(BuildTrackDbContext db, IReadOnlyCollection<Worker> workers, Guid? selectedSiteId, ILogger? statsLogger, CancellationToken ct)
{
    if (workers.Count == 0) return [];

    var workerIds = workers.Select(x => x.Id).ToArray();
    var assignmentSiteIds = workers
        .SelectMany(x => x.SiteAssignments.Select(assignment => assignment.SiteId))
        .Concat(workers.Select(x => x.SiteId))
        .Distinct()
        .ToArray();
    var deviceIds = workers
        .SelectMany(x => x.CameraIdentities.Select(identity => identity.DeviceId))
        .Where(id => id is not null)
        .Select(id => id!.Value)
        .Distinct()
        .ToArray();
    var deviceNames = deviceIds.Length == 0
        ? new Dictionary<Guid, string>()
        : await db.Devices.AsNoTracking().Where(x => deviceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
    var siteNames = assignmentSiteIds.Length == 0
        ? new Dictionary<Guid, string>()
        : await db.Sites.AsNoTracking().Where(x => assignmentSiteIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);

    var bakuTimeZone = ResolveApiTimeZone("Asia/Baku");
    var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, bakuTimeZone).DateTime);
    var monthStart = new DateOnly(today.Year, today.Month, 1);
    var workerSessionKeys = workers.ToDictionary(x => x.Id, WorkerSessionKeys);
    var externalSessionKeys = workerSessionKeys.Values.SelectMany(x => x).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var sessionsQuery = db.AttendanceSessions.AsNoTracking()
        .Where(x => x.WorkDate >= monthStart
                    && ((x.WorkerId != null && workerIds.Contains(x.WorkerId.Value))
                        || externalSessionKeys.Contains(x.WorkerExternalId)));
    if (selectedSiteId is not null) sessionsQuery = sessionsQuery.Where(x => x.SiteId == selectedSiteId.Value);
    var sessions = await sessionsQuery.ToListAsync(ct);
    var sessionsByWorker = workers.ToDictionary(worker => worker.Id, _ => new List<AttendanceSession>());
    foreach (var session in sessions)
    {
        var workerId = session.WorkerId is not null && sessionsByWorker.ContainsKey(session.WorkerId.Value)
            ? session.WorkerId.Value
            : workers.FirstOrDefault(worker => SessionMatchesWorker(session, worker))?.Id;
        if (workerId is not null) sessionsByWorker[workerId.Value].Add(session);
    }

    return workers.Select(worker =>
    {
        var workerSessions = sessionsByWorker.GetValueOrDefault(worker.Id) ?? new List<AttendanceSession>();
        var todaySessions = workerSessions.Where(x => x.WorkDate == today).ToArray();
        var todayHours = Math.Round(todaySessions.Sum(CalculatePresenceHours), 2);
        var monthlyHours = Math.Round(workerSessions.Sum(CalculatePresenceHours), 2);
        var isCurrentlyActive = todaySessions.Any(x => x.Status == AttendanceSessionStatus.Open);
        DateTimeOffset? currentSessionStartedAt = todaySessions
            .Where(x => x.Status == AttendanceSessionStatus.Open)
            .OrderBy(x => x.CheckInTime)
            .Select(x => (DateTimeOffset?)x.CheckInTime)
            .FirstOrDefault();
        DateTimeOffset? lastSeenAt = workerSessions.Count == 0 ? null : workerSessions.Max(x => x.LastSeenTime ?? x.CheckOutTime ?? x.CheckInTime);
        var todayAmount = Math.Round((decimal)todayHours * worker.HourlyRate, 2);
        var monthlyAmount = Math.Round((decimal)monthlyHours * worker.HourlyRate, 2);
        statsLogger?.LogInformation(
            "Worker camera stats calculated. WorkerId={WorkerId}, TenantId={TenantId}, SiteFilter={SiteFilter}, TodaySessionCount={TodaySessionCount}, ActiveSessionCount={ActiveSessionCount}, TodayHours={TodayHours}, HourlyRate={HourlyRate}, TodayAmount={TodayAmount}",
            worker.Id,
            worker.TenantId,
            selectedSiteId,
            todaySessions.Length,
            todaySessions.Count(x => x.Status == AttendanceSessionStatus.Open),
            todayHours,
            worker.HourlyRate,
            todayAmount);
        return new WorkerResponse(
            worker.Id,
            worker.SiteId,
            worker.ExternalWorkerCode,
            worker.FullName,
            worker.Status,
            worker.Brigade,
            worker.Role,
            worker.HourlyRate,
            worker.PlannedDailyHours,
            ResolveWorkerAttendanceSource(worker),
            worker.RiskScore,
            worker.Notes,
            worker.CreatedAt,
            worker.UpdatedAt,
            worker.CameraIdentities
                .OrderByDescending(identity => identity.IsPrimary)
                .ThenBy(identity => identity.CreatedAt)
                .Select(identity => new WorkerCameraIdentityResponse(
                    identity.Id,
                    identity.WorkerId,
                    identity.DeviceId,
                    identity.DeviceId is not null ? deviceNames.GetValueOrDefault(identity.DeviceId.Value) : null,
                    identity.Vendor,
                    identity.ExternalUserId,
                    identity.CardName,
                    identity.NormalizedCardName,
                    identity.IsPrimary,
                    identity.CreatedAt,
                    identity.UpdatedAt))
                .ToArray(),
            worker.SiteAssignments
                .Where(assignment => assignment.Status == WorkerSiteAssignmentStatus.Active)
                .OrderByDescending(assignment => assignment.IsPrimary)
                .ThenBy(assignment => siteNames.GetValueOrDefault(assignment.SiteId))
                .Select(assignment => new WorkerSiteAssignmentResponse(
                    assignment.Id,
                    assignment.WorkerId,
                    assignment.SiteId,
                    siteNames.GetValueOrDefault(assignment.SiteId),
                    assignment.IsPrimary,
                    assignment.Status,
                    assignment.CreatedAt,
                    assignment.UpdatedAt))
                .ToArray(),
            new WorkerPayrollSummaryResponse(
                todayHours,
                todayAmount,
                todayAmount,
                monthlyHours,
                monthlyAmount,
                monthlyAmount,
                isCurrentlyActive,
                currentSessionStartedAt,
                lastSeenAt));
    }).ToArray();
}

static bool SessionMatchesWorker(AttendanceSession session, Worker worker)
{
    if (session.WorkerId == worker.Id) return true;
    var keys = WorkerSessionKeys(worker);
    return keys.Contains(session.WorkerExternalId, StringComparer.OrdinalIgnoreCase)
           || (!string.IsNullOrWhiteSpace(session.WorkerName) && keys.Contains(session.WorkerName, StringComparer.OrdinalIgnoreCase));
}

static string[] WorkerSessionKeys(Worker worker) =>
    new[]
    {
        worker.ExternalWorkerCode,
    }
    .Concat(worker.CameraIdentities.SelectMany(identity => new[] { identity.ExternalUserId, identity.CardName, identity.NormalizedCardName }))
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Select(value => value!.Trim())
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

static async Task SyncWorkerSiteAssignmentsFromRequestAsync(
    IWorkerSiteAssignmentService siteAssignmentService,
    Guid workerId,
    Guid fallbackSiteId,
    IReadOnlyList<SaveWorkerSiteAssignmentRequest>? assignments,
    CancellationToken ct)
{
    if (assignments is null)
    {
        await siteAssignmentService.SyncAssignmentsAsync(workerId, [fallbackSiteId], fallbackSiteId, ct);
        return;
    }

    var siteIds = assignments.Select(x => x.SiteId).Where(id => id != Guid.Empty).Distinct().ToArray();
    var primarySiteId = assignments.FirstOrDefault(x => x.IsPrimary)?.SiteId;
    await siteAssignmentService.SyncAssignmentsAsync(
        workerId,
        siteIds,
        primarySiteId == Guid.Empty ? null : primarySiteId,
        ct);
}

static double CalculatePresenceHours(AttendanceSession session)
{
    var end = session.Status == AttendanceSessionStatus.Open
        ? DateTimeOffset.UtcNow
        : session.CheckOutTime ?? session.LastSeenTime ?? session.CheckInTime;
    return Math.Max(0, (end - session.CheckInTime).TotalHours);
}

static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

static string NormalizeAttendanceSource(string? value)
{
    if (string.Equals(value, "Camera", StringComparison.OrdinalIgnoreCase)) return "Camera";
    if (string.Equals(value, "ForemanTablet", StringComparison.OrdinalIgnoreCase)) return "ForemanTablet";
    return "Manual";
}

static string NormalizeAttendanceSourceForWorker(string? value, SaveWorkerCameraIdentityRequest? cameraIdentity)
{
    var normalized = NormalizeAttendanceSource(value);
    if (normalized == "ForemanTablet") return normalized;
    return cameraIdentity is not null && HasCameraIdentityValues(cameraIdentity) ? "Camera" : normalized;
}

static string ResolveWorkerAttendanceSource(Worker worker)
{
    if (string.Equals(worker.AttendanceSource, "ForemanTablet", StringComparison.OrdinalIgnoreCase)) return "ForemanTablet";
    return worker.CameraIdentities.Count > 0 ? "Camera" : NormalizeAttendanceSource(worker.AttendanceSource);
}

static bool HasCameraIdentityValues(SaveWorkerCameraIdentityRequest request) =>
    !string.IsNullOrWhiteSpace(request.CardName) || !string.IsNullOrWhiteSpace(request.ExternalUserId);

static (string? ExternalUserId, string? CardName) ExtractCameraIdentity(string rawPayloadJson)
{
    try
    {
        using var document = JsonDocument.Parse(rawPayloadJson);
        var root = document.RootElement;
        return (
            FirstJsonString(root, "DahuaUserID", "CameraUserID", "UserID", "UserId", "WorkerExternalId"),
            FirstJsonString(root, "DahuaCardName", "ReceivedCardName", "TrustedCardName", "CardName"));
    }
    catch (JsonException)
    {
        return (null, null);
    }
}

static string? FirstJsonString(JsonElement root, params string[] propertyNames)
{
    foreach (var propertyName in propertyNames)
    {
        if (!root.TryGetProperty(propertyName, out var value)) continue;
        var candidate = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
        if (!string.IsNullOrWhiteSpace(candidate)) return candidate.Trim();
    }

    return null;
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

static int ParseInt(string? value, int defaultValue) =>
    int.TryParse(value, out var parsed) ? parsed : defaultValue;

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
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            await DbInitializer.EnsureDatabaseAsync(db, configuration, cancellationToken);
            return;
        }
        catch (Exception ex) when (attempt < 10)
        {
            logger.LogWarning(ex, "Database is not ready. Retry {Attempt}/10", attempt);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}






























