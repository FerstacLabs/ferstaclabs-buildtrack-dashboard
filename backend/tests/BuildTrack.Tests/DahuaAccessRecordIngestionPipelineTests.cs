using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildTrack.Tests;

public sealed class DahuaAccessRecordIngestionPipelineTests
{
    [Fact]
    public async Task CgiKnownWorkerRecordGoesToAttendanceIngestionWithCgiSource()
    {
        var attendance = new FakeAttendanceIngestionService();
        var security = new FakeSecurityEventService();
        var pipeline = CreatePipeline(attendance, security);
        var record = KnownFaceRecord();

        await pipeline.IngestAsync(Guid.NewGuid(), record, DahuaEventSource.CgiPolling, CancellationToken.None);

        Assert.Equal(1, attendance.Calls);
        Assert.Equal(DahuaEventSourceExtensions.CgiPollingSource, attendance.LastSource);
        Assert.Equal(0, security.Calls);
    }

    [Fact]
    public async Task CgiUnknownFaceRecordGoesToSecurityIngestionWithCgiSource()
    {
        var attendance = new FakeAttendanceIngestionService();
        var security = new FakeSecurityEventService();
        var pipeline = CreatePipeline(attendance, security);
        var record = UnknownFaceRecord();

        await pipeline.IngestAsync(Guid.NewGuid(), record, DahuaEventSource.CgiPolling, CancellationToken.None);

        Assert.Equal(0, attendance.Calls);
        Assert.Equal(1, security.Calls);
        Assert.Equal(DahuaEventSourceExtensions.CgiPollingSource, security.LastSource);
    }

    [Fact]
    public async Task ActiveRegisterDecodedKnownRecordUsesActiveRegisterSource()
    {
        var attendance = new FakeAttendanceIngestionService();
        var security = new FakeSecurityEventService();
        var pipeline = CreatePipeline(attendance, security);
        var record = KnownFaceRecord();

        await pipeline.IngestAsync(Guid.NewGuid(), record, DahuaEventSource.ActiveRegister, CancellationToken.None);

        Assert.Equal(1, attendance.Calls);
        Assert.Equal(DahuaEventSourceExtensions.ActiveRegisterSource, attendance.LastSource);
    }

    [Fact]
    public async Task FailedNonUnknownRecordDoesNotCreateAttendanceOrSecurityEvent()
    {
        var attendance = new FakeAttendanceIngestionService();
        var security = new FakeSecurityEventService();
        var pipeline = CreatePipeline(attendance, security);
        var record = KnownFaceRecord();
        record.StatusRaw = "0";
        record.CardName = "Ilham";
        record.UserId = "1";

        await pipeline.IngestAsync(Guid.NewGuid(), record, DahuaEventSource.ActiveRegister, CancellationToken.None);

        Assert.Equal(0, attendance.Calls);
        Assert.Equal(0, security.Calls);
    }

    private static DahuaAccessRecordIngestionPipeline CreatePipeline(FakeAttendanceIngestionService attendance, FakeSecurityEventService security)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DAHUA_UNKNOWN_FACE_DEBOUNCE_SECONDS"] = "30",
                ["DAHUA_CGI_DEVICE_TIMEZONE"] = "Asia/Baku",
            })
            .Build();
        return new DahuaAccessRecordIngestionPipeline(configuration, attendance, security, NullLogger<DahuaAccessRecordIngestionPipeline>.Instance);
    }

    private static DahuaAccessRecord KnownFaceRecord() => new()
    {
        RecNo = 101,
        CreateTime = DateTimeOffset.Parse("2026-07-14T05:00:00+00:00"),
        UserId = "1",
        CardName = "Ilham",
        StatusRaw = "1",
        MethodRaw = "15",
        Type = "Entry",
        Url = "/SnapShot/known.jpg",
        RawFields = new Dictionary<string, string?>
        {
            ["UserID"] = "1",
            ["CardName"] = "Ilham",
            ["Status"] = "1",
            ["Method"] = "15",
            ["Type"] = "Entry",
        },
    };

    private static DahuaAccessRecord UnknownFaceRecord() => new()
    {
        RecNo = 102,
        CreateTime = DateTimeOffset.Parse("2026-07-14T05:01:00+00:00"),
        UserId = null,
        CardName = null,
        StatusRaw = "0",
        MethodRaw = "15",
        Type = "Entry",
        Url = "/SnapShot/unknown.jpg",
        RawFields = new Dictionary<string, string?>
        {
            ["Status"] = "0",
            ["ErrorCode"] = "16",
            ["Method"] = "15",
            ["Type"] = "Entry",
            ["URL"] = "/SnapShot/unknown.jpg",
        },
    };

    private sealed class FakeAttendanceIngestionService : IAttendanceIngestionService
    {
        public int Calls { get; private set; }
        public string? LastSource { get; private set; }

        public Task<AttendanceEvent?> IngestDahuaRecordAsync(Guid deviceId, DahuaAccessRecord record, string? remoteIp = null, int? remotePort = null, CancellationToken cancellationToken = default, string source = "dahua_terminal", bool requireSuccessfulAttendance = false)
        {
            Calls++;
            LastSource = source;
            return Task.FromResult<AttendanceEvent?>(new AttendanceEvent
            {
                DeviceId = deviceId,
                WorkerExternalId = record.UserId,
                WorkerName = record.CardName,
                Source = source,
            });
        }
    }

    private sealed class FakeSecurityEventService : ISecurityEventService
    {
        public int Calls { get; private set; }
        public string? LastSource { get; private set; }

        public Task<SecurityEventIngestionResult> IngestUnknownFaceAsync(Guid deviceId, DahuaAccessRecord record, TimeSpan debounceWindow, TimeZoneInfo eventTimeZone, string source = "dahua_cgi_polling", CancellationToken cancellationToken = default)
        {
            Calls++;
            LastSource = source;
            return Task.FromResult(new SecurityEventIngestionResult(SecurityEventIngestionResultStatus.Created));
        }
    }
}
