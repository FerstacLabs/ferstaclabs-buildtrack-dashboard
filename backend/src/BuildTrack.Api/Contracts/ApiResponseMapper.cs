using BuildTrack.Domain.Entities;

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
        attendanceEvent.SnapshotPath,
        string.IsNullOrWhiteSpace(attendanceEvent.SnapshotPath) ? null : $"/api/attendance-events/{attendanceEvent.Id}/snapshot",
        attendanceEvent.Source,
        attendanceEvent.CreatedAt);
}
