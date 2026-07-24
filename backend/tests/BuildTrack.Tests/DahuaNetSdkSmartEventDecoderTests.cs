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
    public void SmartEventClassification_RecognizesOnlyResolvedMatchingWorker()
    {
        var record = KnownFaceRecord("1", "ilham");
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var recognized = DahuaSmartEventClassification.IsRecognizedAttendance(record, worker);

        Assert.True(recognized);
    }

    [Fact]
    public void SmartEventClassification_NameMismatchBecomesUnknownFace()
    {
        var record = KnownFaceRecord("1", "Random Candidate");
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var recognized = DahuaSmartEventClassification.IsRecognizedAttendance(record, worker);
        var unknown = DahuaSmartEventClassification.BuildUnknownFaceRecord(record, "{}");

        Assert.False(recognized);
        Assert.True(DahuaUnknownFacePolicy.IsUnknownFace(unknown));
        Assert.Null(unknown.UserId);
        Assert.Null(unknown.CardName);
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
}
