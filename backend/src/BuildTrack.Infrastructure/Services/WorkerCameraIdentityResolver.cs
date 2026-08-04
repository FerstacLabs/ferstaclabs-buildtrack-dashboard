using System.Globalization;
using System.Text;
using System.Text.Json;
using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildTrack.Infrastructure.Services;

public sealed class WorkerCameraIdentityResolver(
    BuildTrackDbContext db,
    ILogger<WorkerCameraIdentityResolver> logger) : IWorkerCameraIdentityResolver
{
    public string? NormalizeCardName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsWhiteSpace(ch) ? ' ' : char.ToLowerInvariant(ch));
        }

        var collapsed = string.Join(' ', builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(collapsed) ? null : collapsed;
    }

    public async Task<WorkerCameraIdentityResolution> ResolveAsync(
        Device device,
        DahuaAccessRecord record,
        CancellationToken cancellationToken = default)
    {
        var normalizedCardName = NormalizeCardName(
            record.RawFields.GetValueOrDefault("ReceivedCardName")
            ?? record.RawFields.GetValueOrDefault("TrustedCardName")
            ?? record.CardName);
        var externalUserId = string.IsNullOrWhiteSpace(record.UserId) ? null : record.UserId.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedCardName))
        {
            var exactDevice = await FindByCardNameAsync(device.TenantId, device.Id, normalizedCardName, cancellationToken);
            if (exactDevice is not null) return Resolved(exactDevice, "TenantDeviceCardName");

            var anyDevice = await FindByCardNameAsync(device.TenantId, null, normalizedCardName, cancellationToken);
            if (anyDevice is not null) return Resolved(anyDevice, "TenantAnyDeviceCardName");
        }

        if (!string.IsNullOrWhiteSpace(externalUserId) && IsCardNameMissingOrTrusted(record))
        {
            var exactDevice = await FindByExternalUserIdAsync(device.TenantId, device.Id, externalUserId, cancellationToken);
            if (exactDevice is not null) return Resolved(exactDevice, "TenantDeviceExternalUserId");

            var anyDevice = await FindByExternalUserIdAsync(device.TenantId, null, externalUserId, cancellationToken);
            if (anyDevice is not null) return Resolved(anyDevice, "TenantAnyDeviceExternalUserId");
        }

        if (!string.IsNullOrWhiteSpace(externalUserId) && LooksLikeInternalWorkerCode(externalUserId))
        {
            var worker = await db.Workers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.TenantId == device.TenantId
                         && x.SiteId == device.SiteId
                         && x.ExternalWorkerCode == externalUserId
                         && x.Status == WorkerStatus.Active,
                    cancellationToken);
            if (worker is not null)
            {
                return new WorkerCameraIdentityResolution(worker, null, "ResolvedLegacyInternalWorkerCode", "LegacyInternalWorkerCode", null);
            }
        }

        return new WorkerCameraIdentityResolution(null, null, "UnmappedCameraIdentity", null, "No worker-camera identity matched this tenant/device");
    }

    public async Task<WorkerCameraIdentity> UpsertAsync(
        Guid workerId,
        Guid? deviceId,
        string? externalUserId,
        string? cardName,
        bool isPrimary,
        CancellationToken cancellationToken = default)
    {
        var worker = await db.Workers.FirstOrDefaultAsync(x => x.Id == workerId, cancellationToken)
            ?? throw new InvalidOperationException("Worker was not found");
        if (deviceId is not null)
        {
            var deviceTenantId = await db.Devices
                .AsNoTracking()
                .Where(x => x.Id == deviceId.Value)
                .Select(x => (Guid?)x.TenantId)
                .FirstOrDefaultAsync(cancellationToken);
            if (deviceTenantId is null) throw new InvalidOperationException("Device was not found");
            if (deviceTenantId.Value != worker.TenantId) throw new InvalidOperationException("Device belongs to another tenant");
        }

        var normalizedCardName = NormalizeCardName(cardName);
        var normalizedExternalUserId = string.IsNullOrWhiteSpace(externalUserId) ? null : externalUserId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCardName) && string.IsNullOrWhiteSpace(normalizedExternalUserId))
        {
            throw new InvalidOperationException("Dahua CardName or UserID is required for camera mapping");
        }

        var existing = await db.WorkerCameraIdentities
            .FirstOrDefaultAsync(
                x => x.WorkerId == worker.Id
                     && x.DeviceId == deviceId
                     && ((normalizedCardName != null && x.NormalizedCardName == normalizedCardName)
                         || (normalizedExternalUserId != null && x.ExternalUserId == normalizedExternalUserId)),
                cancellationToken);

        existing ??= await FindExistingConflictAsync(worker.TenantId, deviceId, normalizedExternalUserId, normalizedCardName, cancellationToken);
        if (existing is not null && existing.WorkerId != worker.Id)
        {
            throw new InvalidOperationException("Bu kamera identifikasiyasi basqa isciye baglanib");
        }

        if (existing is null)
        {
            existing = new WorkerCameraIdentity
            {
                TenantId = worker.TenantId,
                WorkerId = worker.Id,
                DeviceId = deviceId,
                Vendor = "Dahua",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.WorkerCameraIdentities.Add(existing);
        }

        existing.ExternalUserId = normalizedExternalUserId;
        existing.CardName = string.IsNullOrWhiteSpace(cardName) ? null : cardName.Trim();
        existing.NormalizedCardName = normalizedCardName;
        existing.IsPrimary = isPrimary;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<WorkerCameraIdentityRemapResult> RemapRecentAsync(
        Guid workerId,
        Guid? identityId,
        CancellationToken cancellationToken = default)
    {
        var worker = await db.Workers.FirstOrDefaultAsync(x => x.Id == workerId, cancellationToken)
            ?? throw new InvalidOperationException("Worker was not found");
        var identitiesQuery = db.WorkerCameraIdentities.Where(x => x.WorkerId == workerId);
        if (identityId is not null) identitiesQuery = identitiesQuery.Where(x => x.Id == identityId.Value);
        var identities = await identitiesQuery.ToListAsync(cancellationToken);
        if (identities.Count == 0) return new WorkerCameraIdentityRemapResult(0, 0);

        var since = DateTimeOffset.UtcNow.AddDays(-45);
        var deviceIds = identities.Where(x => x.DeviceId is not null).Select(x => x.DeviceId!.Value).Distinct().ToArray();
        var rawCardNames = identities.Select(x => x.CardName).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray();
        var normalizedCardNames = identities.Select(x => x.NormalizedCardName).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray();
        var externalUserIds = identities.Select(x => x.ExternalUserId).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray();

        var eventCandidatesQuery = db.AttendanceEvents
            .Where(x => x.TenantId == worker.TenantId && x.CreatedAt >= since);
        if (deviceIds.Length > 0) eventCandidatesQuery = eventCandidatesQuery.Where(x => deviceIds.Contains(x.DeviceId) || x.WorkerId == null);
        var eventCandidates = await eventCandidatesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(1000)
            .ToListAsync(cancellationToken);

        var updatedEvents = 0;
        foreach (var attendanceEvent in eventCandidates.Where(x => MatchesIdentity(x, rawCardNames, normalizedCardNames, externalUserIds)))
        {
            attendanceEvent.WorkerId = worker.Id;
            attendanceEvent.WorkerExternalId = worker.ExternalWorkerCode;
            attendanceEvent.WorkerName = worker.FullName;
            attendanceEvent.RawPayloadJson = MergeRawPayload(attendanceEvent.RawPayloadJson, new Dictionary<string, string?>
            {
                ["WorkerResolutionStatus"] = "RemappedWorkerCameraIdentity",
                ["ResolvedWorkerExternalId"] = worker.ExternalWorkerCode,
                ["ResolvedWorkerName"] = worker.FullName,
            });
            updatedEvents++;
        }

        var sessionCandidates = await db.AttendanceSessions
            .Where(x => x.TenantId == worker.TenantId && x.CreatedAt >= since)
            .OrderByDescending(x => x.CreatedAt)
            .Take(1000)
            .ToListAsync(cancellationToken);

        var updatedSessions = 0;
        foreach (var session in sessionCandidates.Where(x => MatchesIdentity(x, rawCardNames, normalizedCardNames, externalUserIds)))
        {
            session.WorkerId = worker.Id;
            session.WorkerExternalId = worker.ExternalWorkerCode;
            session.WorkerName = worker.FullName;
            session.UpdatedAt = DateTimeOffset.UtcNow;
            updatedSessions++;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Worker camera identity remap completed. WorkerId={WorkerId}, AttendanceEventsUpdated={AttendanceEventsUpdated}, AttendanceSessionsUpdated={AttendanceSessionsUpdated}",
            worker.Id,
            updatedEvents,
            updatedSessions);
        return new WorkerCameraIdentityRemapResult(updatedEvents, updatedSessions);
    }

    private async Task<WorkerCameraIdentity?> FindByCardNameAsync(Guid tenantId, Guid? deviceId, string normalizedCardName, CancellationToken cancellationToken)
    {
        var query = db.WorkerCameraIdentities
            .AsNoTracking()
            .Include(x => x.Worker)
            .Where(x => x.TenantId == tenantId
                        && x.NormalizedCardName == normalizedCardName
                        && x.Worker != null
                        && x.Worker.Status == WorkerStatus.Active);
        query = deviceId is null ? query.Where(x => x.DeviceId == null) : query.Where(x => x.DeviceId == deviceId.Value);
        return await query.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<WorkerCameraIdentity?> FindByExternalUserIdAsync(Guid tenantId, Guid? deviceId, string externalUserId, CancellationToken cancellationToken)
    {
        var query = db.WorkerCameraIdentities
            .AsNoTracking()
            .Include(x => x.Worker)
            .Where(x => x.TenantId == tenantId
                        && x.ExternalUserId == externalUserId
                        && x.Worker != null
                        && x.Worker.Status == WorkerStatus.Active);
        query = deviceId is null ? query.Where(x => x.DeviceId == null) : query.Where(x => x.DeviceId == deviceId.Value);
        return await query.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<WorkerCameraIdentity?> FindExistingConflictAsync(Guid tenantId, Guid? deviceId, string? externalUserId, string? normalizedCardName, CancellationToken cancellationToken)
    {
        var query = db.WorkerCameraIdentities.Where(x => x.TenantId == tenantId && x.DeviceId == deviceId);
        return await query.FirstOrDefaultAsync(
            x => (normalizedCardName != null && x.NormalizedCardName == normalizedCardName)
                 || (externalUserId != null && x.ExternalUserId == externalUserId),
            cancellationToken);
    }

    private static WorkerCameraIdentityResolution Resolved(WorkerCameraIdentity identity, string resolvedBy) =>
        new(identity.Worker, identity, "ResolvedWorkerCameraIdentity", resolvedBy, null);

    private static bool IsCardNameMissingOrTrusted(DahuaAccessRecord record)
    {
        var receivedCardName = record.RawFields.GetValueOrDefault("ReceivedCardName") ?? record.CardName;
        if (string.IsNullOrWhiteSpace(receivedCardName)) return true;
        var source = record.RawFields.GetValueOrDefault("CardNameSource");
        var confidence = record.RawFields.GetValueOrDefault("CardNameConfidence");
        return string.Equals(source, "DEV_EVENT_ACCESS_CTL_INFO", StringComparison.OrdinalIgnoreCase)
               && string.Equals(confidence, "High", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeInternalWorkerCode(string value) =>
        value.StartsWith("W-", StringComparison.OrdinalIgnoreCase);

    private bool MatchesIdentity(AttendanceEvent attendanceEvent, IReadOnlyCollection<string> rawCardNames, IReadOnlyCollection<string> normalizedCardNames, IReadOnlyCollection<string> externalUserIds)
    {
        if (externalUserIds.Any(id => string.Equals(attendanceEvent.WorkerExternalId, id, StringComparison.OrdinalIgnoreCase))) return true;
        if (rawCardNames.Any(name => string.Equals(attendanceEvent.WorkerName, name, StringComparison.OrdinalIgnoreCase))) return true;
        return RawPayloadMatches(attendanceEvent.RawPayloadJson, rawCardNames, normalizedCardNames, externalUserIds);
    }

    private bool MatchesIdentity(AttendanceSession session, IReadOnlyCollection<string> rawCardNames, IReadOnlyCollection<string> normalizedCardNames, IReadOnlyCollection<string> externalUserIds)
    {
        if (externalUserIds.Any(id => string.Equals(session.WorkerExternalId, id, StringComparison.OrdinalIgnoreCase))) return true;
        if (rawCardNames.Any(name => string.Equals(session.WorkerName, name, StringComparison.OrdinalIgnoreCase))) return true;
        var normalizedSessionName = NormalizeCardName(session.WorkerName);
        return normalizedSessionName is not null && normalizedCardNames.Contains(normalizedSessionName, StringComparer.OrdinalIgnoreCase);
    }

    private bool RawPayloadMatches(string rawPayloadJson, IReadOnlyCollection<string> rawCardNames, IReadOnlyCollection<string> normalizedCardNames, IReadOnlyCollection<string> externalUserIds)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayloadJson);
            var root = document.RootElement;
            var payloadUserId = GetJsonString(root, "CameraUserID")
                                ?? GetJsonString(root, "UserID")
                                ?? GetJsonString(root, "UserId");
            if (externalUserIds.Any(id => string.Equals(payloadUserId, id, StringComparison.OrdinalIgnoreCase))) return true;

            var payloadCardName = GetJsonString(root, "ReceivedCardName")
                                  ?? GetJsonString(root, "TrustedCardName")
                                  ?? GetJsonString(root, "CardName");
            if (rawCardNames.Any(name => string.Equals(payloadCardName, name, StringComparison.OrdinalIgnoreCase))) return true;
            var normalizedPayloadCardName = NormalizeCardName(payloadCardName);
            return normalizedPayloadCardName is not null && normalizedCardNames.Contains(normalizedPayloadCardName, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string MergeRawPayload(string rawPayloadJson, Dictionary<string, string?> values)
    {
        var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(rawPayloadJson);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                merged[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => property.Value.GetRawText(),
                };
            }
        }
        catch (JsonException)
        {
            merged["OriginalRawPayloadJson"] = rawPayloadJson;
        }

        foreach (var (key, value) in values) merged[key] = value;
        return JsonSerializer.Serialize(merged);
    }

    private static string? GetJsonString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }
}
