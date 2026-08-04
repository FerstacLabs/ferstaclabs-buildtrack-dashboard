using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Services;

namespace BuildTrack.Api.Contracts;

public static class ApiResponseMapper
{
    public static DeviceResponse ToDeviceResponse(Device device, AttendanceEvent? lastEvent, string netSdkDecodeStatus = "Unknown") => new(
        device.Id,
        device.SiteId,
        device.Name,
        device.Vendor,
        device.Model,
        device.Mode,
        device.RegisterDeviceId,
        device.RegisterPort,
        device.LastKnownIp,
        device.LastSeenAt,
        device.Status,
        device.LastRecNo,
        device.CreatedAt,
        device.UpdatedAt,
        lastEvent?.EventTime,
        lastEvent?.WorkerName,
        netSdkDecodeStatus);

    public static AttendanceEventResponse ToAttendanceEventResponse(
        AttendanceEvent attendanceEvent,
        string? siteName = null,
        string? deviceName = null) => new(
        attendanceEvent.Id,
        attendanceEvent.SiteId,
        siteName,
        attendanceEvent.DeviceId,
        deviceName,
        attendanceEvent.WorkerExternalId,
        attendanceEvent.WorkerName,
        attendanceEvent.EventTime,
        attendanceEvent.Direction,
        attendanceEvent.Status,
        attendanceEvent.Method,
        attendanceEvent.RawRecNo,
        PublicSnapshotPath(attendanceEvent.SnapshotPath),
        SnapshotUrl(attendanceEvent.SnapshotPath),
        attendanceEvent.Source,
        attendanceEvent.CreatedAt);

    private static string? SnapshotUrl(string? snapshotPath) =>
        SnapshotPathPolicy.TryCreateApiUrl(snapshotPath, out var snapshotUrl) ? snapshotUrl : null;

    private static string? PublicSnapshotPath(string? snapshotPath) =>
        SnapshotPathPolicy.TryCreateApiUrl(snapshotPath, out _) ? null : snapshotPath;
}
