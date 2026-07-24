using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Tests;

public sealed class DahuaNetSdkSmartEventDecoderTests
{
    [Fact]
    public void ResolveEventName_MapsAccessControlSmartEvent()
    {
        Assert.Equal("EVENT_IVS_ACCESS_CTL", DahuaNetSdkSmartEventDecoder.ResolveEventName(0x204));
    }

    [Fact]
    public void Decode_SkipsUnsupportedSmartEvent()
    {
        var buffer = new byte[8192];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            var result = DahuaNetSdkSmartEventDecoder.Decode(0x9999, handle.AddrOfPinnedObject(), IntPtr.Zero, 0, 7);

            Assert.Equal("UnsupportedSmartEvent", result.ParseStatus);
            Assert.Null(result.Record);
            Assert.Equal(7, result.Sequence);
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public void Decode_AccessEventWithoutPersonFields_ReturnsDiagnosticOnlyResult()
    {
        var buffer = new byte[8192];
        WriteInt(buffer, 0, 1);
        WriteAscii(buffer, 4, 128, "AccessControl");
        WriteInt(buffer, 144, 2026);
        WriteInt(buffer, 148, 7);
        WriteInt(buffer, 152, 24);
        WriteInt(buffer, 156, 9);
        WriteInt(buffer, 160, 35);
        WriteInt(buffer, 164, 12);
        WriteInt(buffer, 180, 1234);

        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            var result = DahuaNetSdkSmartEventDecoder.Decode(0x204, handle.AddrOfPinnedObject(), IntPtr.Zero, 4096, 11);

            Assert.Equal("EVENT_IVS_ACCESS_CTL", result.EventName);
            Assert.Equal("DEV_EVENT_ACCESS_CTL_INFO", result.StructName);
            Assert.Equal("DecodedAccessSmartEventNoPersonFields", result.ParseStatus);
            Assert.NotNull(result.Record);
            Assert.Equal(1234, result.Record!.RecNo);
            Assert.Equal("15", result.Record.MethodRaw);
            Assert.Equal("Entry", result.Record.Type);
            Assert.Contains("ImageBytesLength", result.RawStructSummaryJson);
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public void SmartEventClassification_TrustedSummaryOverridesBrokenTopLevelRecord()
    {
        var brokenTopLevel = UnknownTopLevelRecord();
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(brokenTopLevel, TrustedSummary("1", "ilham", "1"), worker);
        var recognized = DahuaSmartEventClassification.IsRecognizedAttendance(trusted, worker);

        Assert.True(recognized);
        Assert.Equal("1", trusted.StatusRaw);
        Assert.Equal("1", trusted.UserId);
        Assert.Equal("Ilham", trusted.CardName);
        Assert.Equal("1", trusted.RawFields["Status"]);
        Assert.Equal("1", trusted.RawFields["UserID"]);
        Assert.Equal("Ilham", trusted.RawFields["CardName"]);
    }

    [Fact]
    public void SmartEventClassification_MappedWorkerNameWinsOverRandomCandidate()
    {
        var record = KnownFaceRecord("1", "pp");
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(record, TrustedSummary("1", "pp", "1"), worker);
        var recognized = DahuaSmartEventClassification.IsRecognizedAttendance(trusted, worker);

        Assert.True(recognized);
        Assert.Equal("Ilham", trusted.CardName);
        Assert.Equal("Ilham", trusted.RawFields["CardName"]);
    }

    [Fact]
    public void SmartEventClassification_MappedWorkerNameWinsOverAnotherRandomCandidate()
    {
        var record = KnownFaceRecord("1", "cj");
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(record, TrustedSummary("1", "cj", "1"), worker);

        Assert.True(DahuaSmartEventClassification.IsRecognizedAttendance(trusted, worker));
        Assert.Equal("Ilham", trusted.CardName);
    }

    [Fact]
    public void SmartEventClassification_UnknownFaceRequiresFailedOrMissingTrustedPersonFields()
    {
        var unknown = DahuaSmartEventClassification.BuildUnknownFaceRecord(
            UnknownTopLevelRecord(),
            TrustedSummary(null, null, "0"));

        Assert.True(DahuaUnknownFacePolicy.IsUnknownFace(unknown));
    }

    [Fact]
    public void SmartEventClassification_UnresolvedTrustedWorkerStillCreatesAttendance()
    {
        var record = KnownFaceRecord("2", "Tahira");
        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(record, TrustedSummary("2", "Tahira", "1"), null);

        Assert.True(DahuaSmartEventClassification.IsRecognizedAttendance(trusted, null));
        Assert.Equal("UnresolvedExternalWorker", trusted.RawFields["WorkerResolutionStatus"]);
        Assert.Equal("2", trusted.UserId);
        Assert.Equal("Tahira", trusted.CardName);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        BitConverter.GetBytes(value).CopyTo(buffer, offset);
    }

    private static void WriteAscii(byte[] buffer, int offset, int length, string value)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(length, bytes.Length));
    }

    private static DahuaAccessRecord KnownFaceRecord(string userId, string cardName) => new()
    {
        RecNo = 701,
        CreateTime = DateTimeOffset.Parse("2026-07-24T06:15:00+00:00"),
        UserId = userId,
        CardName = cardName,
        StatusRaw = "1",
        MethodRaw = "15",
        Type = "Entry",
        Url = "/app/data/security-snapshots/smart-events/known.jpg",
        RawFields = new Dictionary<string, string?>
        {
            ["Status"] = "1",
            ["Method"] = "15",
            ["ErrorCode"] = "0",
        },
    };

    private static DahuaAccessRecord UnknownTopLevelRecord() => new()
    {
        RecNo = 702,
        CreateTime = DateTimeOffset.Parse("2026-07-24T06:16:00+00:00"),
        UserId = null,
        CardName = null,
        StatusRaw = "0",
        MethodRaw = "15",
        Type = "Entry",
        Url = "/app/data/security-snapshots/smart-events/unknown.jpg",
        RawFields = new Dictionary<string, string?>
        {
            ["Status"] = "0",
            ["Method"] = "15",
        },
    };

    private static string TrustedSummary(string? userId, string? cardName, string status) =>
        $$"""
          {
            "SmartEventName": "EVENT_IVS_ACCESS_CTL",
            "SmartEventType": "0x204",
            "Status": "{{status}}",
            "UserId": {{JsonValue(userId)}},
            "CardName": {{JsonValue(cardName)}},
            "Method": "face",
            "Direction": "Entry",
            "EventTime": "2026-07-24T06:15:00+00:00",
            "ImageBytesLength": 47701,
            "ErrorCode": "0"
          }
          """;

    private static string JsonValue(string? value) => value is null ? "null" : $"\"{value}\"";
}
