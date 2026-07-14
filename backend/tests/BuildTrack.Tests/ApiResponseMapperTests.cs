using System.Text.Json;
using System.Text.Json.Serialization;
using BuildTrack.Api.Contracts;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Tests;

public sealed class ApiResponseMapperTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    [Fact]
    public void ToAttendanceEventResponse_DoesNotSerializeEfNavigationCyclesOrSensitiveDeviceFields()
    {
        var site = new Site { Id = Guid.NewGuid(), Name = "Demo Site" };
        var device = new Device
        {
            Id = Guid.NewGuid(),
            SiteId = site.Id,
            Name = "Dahua Door",
            Username = "admin",
            EncryptedPassword = "secret-cipher",
            RegisterDeviceId = "BT-API-TEST-001",
            RegisterPort = 7000,
        };
        var attendanceEvent = new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            SiteId = site.Id,
            Site = site,
            DeviceId = device.Id,
            Device = device,
            WorkerExternalId = "1",
            WorkerName = "Simulator Worker",
            EventTime = DateTimeOffset.UtcNow,
            Direction = AttendanceDirection.Entry,
            Status = AttendanceEventStatus.Ok,
            Method = AttendanceMethod.Face,
            RawRecNo = 123,
            Source = "dahua_terminal",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        device.AttendanceEvents.Add(attendanceEvent);

        var response = ApiResponseMapper.ToAttendanceEventResponse(attendanceEvent, site.Name, device.Name);
        var json = JsonSerializer.Serialize(response, JsonOptions);

        Assert.Contains("Simulator Worker", json);
        Assert.DoesNotContain("encryptedPassword", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attendanceEvents", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("device\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToDeviceResponse_DoesNotSerializeUsernameEncryptedPasswordOrCollections()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            Name = "Dahua Door",
            Username = "admin",
            EncryptedPassword = "secret-cipher",
            RegisterDeviceId = "BT-API-TEST-001",
            RegisterPort = 7000,
            Mode = DeviceMode.ActiveRegister,
            Status = DeviceStatus.Pending,
        };
        var response = ApiResponseMapper.ToDeviceResponse(device, null);
        var json = JsonSerializer.Serialize(response, JsonOptions);

        Assert.Contains("BT-API-TEST-001", json);
        Assert.Contains("7000", json);
        Assert.Contains("ActiveRegister", json);
        Assert.Contains("Pending", json);
        Assert.DoesNotContain("username", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("encryptedPassword", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attendanceEvents", json, StringComparison.OrdinalIgnoreCase);
    }
}
