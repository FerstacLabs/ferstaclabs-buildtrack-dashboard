using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Tests;

public sealed class DahuaCgiRecordParserTests
{
    [Fact]
    public void ParseKeyValueResponse_MapsSuccessfulFaceEntry()
    {
        const string text = """
records[0].RecNo=101
records[0].CreateTime=2026-07-06 09:12:11
records[0].CardName=Ilham
records[0].UserID=1
records[0].Status=1
records[0].Method=15
records[0].Type=Entry
records[0].URL=/snap/101.jpg
""";

        var records = DahuaCgiRecordParser.ParseKeyValueResponse(text);

        Assert.Single(records);
        var record = records[0];
        Assert.Equal(101, record.RecNo);
        Assert.Equal("Ilham", record.CardName);
        Assert.Equal("1", record.UserId);
        Assert.Equal(AttendanceEventStatus.Ok, record.NormalizedStatus);
        Assert.Equal(AttendanceMethod.Face, record.NormalizedMethod);
        Assert.Equal(AttendanceDirection.Entry, record.NormalizedDirection);
    }

    [Fact]
    public void ParseKeyValueResponse_TreatsEmptyWorkerAsStranger()
    {
        const string text = """
records[0].RecNo=102
records[0].CreateTime=2026-07-06 09:13:11
records[0].CardName=
records[0].UserID=
records[0].Status=0
records[0].Method=15
records[0].Type=Entry
""";

        var record = DahuaCgiRecordParser.ParseKeyValueResponse(text)[0];

        Assert.Equal(AttendanceEventStatus.Stranger, record.NormalizedStatus);
        Assert.Equal(AttendanceMethod.Face, record.NormalizedMethod);
    }

    [Fact]
    public void ParseKeyValueResponse_ParsesMultipleRecordsInOrder()
    {
        const string text = """
records[1].RecNo=201
records[1].CardName=Second
records[1].UserID=2
records[1].Status=1
records[1].Method=15
records[1].Type=Exit
records[0].RecNo=200
records[0].CardName=First
records[0].UserID=1
records[0].Status=1
records[0].Method=15
records[0].Type=Entry
""";

        var records = DahuaCgiRecordParser.ParseKeyValueResponse(text);

        Assert.Equal(2, records.Count);
        Assert.Equal(200, records[0].RecNo);
        Assert.Equal(201, records[1].RecNo);
        Assert.Equal(AttendanceDirection.Exit, records[1].NormalizedDirection);
    }

    [Fact]
    public void ParseKeyValueResponse_ParsesAccessControlCardRecSampleWithUnixTime()
    {
        const string text = """
found=20
records[0].RecNo=345
records[0].CreateTime=1720332911
records[0].CardName=ilham
records[0].UserID=1
records[0].Status=1
records[0].ErrorCode=0
records[0].Method=15
records[0].Type=Entry
records[0].URL=/cgi-bin/snapshot.cgi?channel=1&id=345
""";

        var records = DahuaCgiRecordParser.ParseKeyValueResponse(text);

        Assert.Single(records);
        var record = records[0];
        Assert.Equal(345, record.RecNo);
        Assert.Equal("1", record.UserId);
        Assert.Equal("ilham", record.CardName);
        Assert.Equal("15", record.MethodRaw);
        Assert.Equal("Entry", record.Type);
        Assert.Equal("/cgi-bin/snapshot.cgi?channel=1&id=345", record.Url);
        Assert.Equal("0", record.RawFields["ErrorCode"]);
        Assert.Equal(AttendanceEventStatus.Ok, record.NormalizedStatus);
        Assert.Equal(AttendanceMethod.Face, record.NormalizedMethod);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1720332911), record.CreateTime.ToUniversalTime());
    }

    [Fact]
    public void ParseKeyValueResponse_InterpretsCreateTimeAsDeviceLocalTimezone()
    {
        const string text = """
records[0].RecNo=400
records[0].CreateTime=2026-07-08 22:51:59
records[0].CardName=ilham
records[0].UserID=1
records[0].Status=1
records[0].Method=15
records[0].Type=Entry
""";
        var deviceTimeZone = TimeZoneInfo.CreateCustomTimeZone("Test +04", TimeSpan.FromHours(4), "Test +04", "Test +04");

        var record = DahuaCgiRecordParser.ParseKeyValueResponse(text, deviceTimeZone)[0];

        Assert.Equal(DateTimeOffset.Parse("2026-07-08T18:51:59+00:00"), record.CreateTime.ToUniversalTime());
    }


    [Fact]
    public void ParseKeyValueResponse_TreatsNumericUnixCreateTimeAsAbsoluteUtc()
    {
        const string text = """
records[0].RecNo=500
records[0].CreateTime=1783545369
records[0].CardName=ilham
records[0].UserID=1
records[0].Status=1
records[0].Method=15
records[0].Type=Entry
""";
        var baku = TimeZoneInfo.CreateCustomTimeZone("Test +04", TimeSpan.FromHours(4), "Test +04", "Test +04");

        var record = DahuaCgiRecordParser.ParseKeyValueResponse(text, baku)[0];
        var bakuTime = TimeZoneInfo.ConvertTime(record.CreateTime, baku);

        Assert.Equal(DateTimeOffset.Parse("2026-07-08T21:16:09+00:00"), record.CreateTime.ToUniversalTime());
        Assert.Equal(2026, bakuTime.Year);
        Assert.Equal(7, bakuTime.Month);
        Assert.Equal(9, bakuTime.Day);
        Assert.Equal(1, bakuTime.Hour);
        Assert.Equal(16, bakuTime.Minute);
        Assert.Equal(9, bakuTime.Second);
    }


    [Fact]
    public void ParseKeyValueResponse_PrefersSeventeenDigitSnapshotTimestampOverNumericCreateTime()
    {
        const string text = """
records[0].RecNo=501
records[0].CreateTime=1783534323
records[0].CardName=ilham
records[0].UserID=1
records[0].Status=1
records[0].Method=15
records[0].Type=Entry
records[0].URL=/mnt/appdata1/userpic/SnapShot/2026-07-10/02/12/1_98_0_20260710021203815.jpg
""";
        var baku = TimeZoneInfo.CreateCustomTimeZone("Test +04", TimeSpan.FromHours(4), "Test +04", "Test +04");

        var record = DahuaCgiRecordParser.ParseKeyValueResponse(text, baku)[0];
        var bakuTime = TimeZoneInfo.ConvertTime(record.CreateTime, baku);

        Assert.Equal(DateTimeOffset.Parse("2026-07-09T22:12:03+00:00"), record.CreateTime.ToUniversalTime());
        Assert.Equal(2026, bakuTime.Year);
        Assert.Equal(7, bakuTime.Month);
        Assert.Equal(10, bakuTime.Day);
        Assert.Equal(2, bakuTime.Hour);
        Assert.Equal(12, bakuTime.Minute);
        Assert.Equal(3, bakuTime.Second);
        Assert.Equal("SnapshotPath", record.RawFields["CreateTimeSource"]);
    }

    [Fact]
    public void ParseKeyValueResponse_ParsesSnapshotStyleCreateTimeAsDeviceLocalTimezone()
    {
        const string text = """
records[0].RecNo=401
records[0].CreateTime=20260708225159
records[0].CardName=ilham
records[0].UserID=1
records[0].Status=1
records[0].Method=15
records[0].Type=Entry
""";
        var deviceTimeZone = TimeZoneInfo.CreateCustomTimeZone("Test +04", TimeSpan.FromHours(4), "Test +04", "Test +04");

        var record = DahuaCgiRecordParser.ParseKeyValueResponse(text, deviceTimeZone)[0];

        Assert.Equal(DateTimeOffset.Parse("2026-07-08T18:51:59+00:00"), record.CreateTime.ToUniversalTime());
    }
}





public sealed class DahuaCgiPollingPlannerTests
{
    [Theory]
    [InlineData(null, null, null, null, 100, 5000, 2, 500)]
    [InlineData("5", "10", "1", "5", 20, 20, 2, 20)]
    [InlineData("300", "1000", "3", "700", 300, 1000, 3, 700)]
    [InlineData("6000", "9000", "20", "9000", 5000, 5000, 10, 5000)]
    public void CreateSettings_UsesSafeAdaptiveDefaultsAndClamps(string? initial, string? max, string? growth, string? lookahead, int expectedInitial, int expectedMax, int expectedGrowth, int expectedLookahead)
    {
        var settings = DahuaCgiPollingPlanner.CreateSettings(initial, max, growth, lookahead);

        Assert.Equal(expectedInitial, settings.InitialFetchCount);
        Assert.Equal(expectedMax, settings.MaxFetchCount);
        Assert.Equal(expectedGrowth, settings.GrowthFactor);
        Assert.Equal(expectedLookahead, settings.FetchLookahead);
    }

    [Fact]
    public void BuildRecordFinderUri_UsesCurrentAdaptiveFetchCount()
    {
        var uri = DahuaCgiPollingPlanner.BuildRecordFinderUri("192.168.31.174", 200);

        Assert.Equal("http://192.168.31.174/cgi-bin/recordFinder.cgi?action=find&name=AccessControlCardRec&count=200", uri.ToString());
    }

    [Fact]
    public void AnalyzeFetch_WhenResponseFullAndMaxRecNoNotPastLastRecNo_RetriesWithBiggerCount()
    {
        var settings = new DahuaCgiFetchSettings(100, 5000, 2, 500);
        var records = Records(1, 100);

        var analysis = DahuaCgiPollingPlanner.AnalyzeFetch(records, lastRecNo: 100, currentFetchCount: 100, settings);

        Assert.True(analysis.ShouldRetry);
        Assert.Equal(200, analysis.NextFetchCount);
        Assert.False(analysis.MaxFetchReachedWithoutNewerRecords);
    }

    [Fact]
    public void SelectCandidates_AfterAdaptiveRetry_ProcessesRecords101To150()
    {
        var records = Records(1, 150);

        var candidates = DahuaCgiPollingPlanner.SelectCandidates(records, 100);

        Assert.Equal(50, candidates.Count);
        Assert.Equal(101, candidates[0].RecNo);
        Assert.Equal(150, candidates[^1].RecNo);
    }

    [Fact]
    public void AdvanceLastRecNoForProcessedRecords_DebouncedRecordsStillAdvanceLastRecNo()
    {
        var debouncedRecords = Records(101, 150);

        var advanced = DahuaCgiPollingPlanner.AdvanceLastRecNoForProcessedRecords(debouncedRecords, 100);

        Assert.Equal(150, advanced);
    }

    [Fact]
    public void AnalyzeFetch_WhenMaxFetchReachedWithoutNewerRecords_ReportsWarningPath()
    {
        var settings = new DahuaCgiFetchSettings(100, 100, 2, 500);
        var records = Records(1, 100);

        var analysis = DahuaCgiPollingPlanner.AnalyzeFetch(records, lastRecNo: 100, currentFetchCount: 100, settings);

        Assert.False(analysis.ShouldRetry);
        Assert.True(analysis.MaxFetchReachedWithoutNewerRecords);
    }


    [Fact]
    public void AnalyzeFetch_WhenLastRecNo598AndCount800DoesRetryTowardLookaheadTarget()
    {
        var settings = new DahuaCgiFetchSettings(100, 5000, 2, 500);
        var records = Records(1, 598);

        var analysis = DahuaCgiPollingPlanner.AnalyzeFetch(records, lastRecNo: 598, currentFetchCount: 800, settings);

        Assert.True(analysis.ShouldRetry);
        Assert.Equal(1098, analysis.TargetFetchCount);
        Assert.Equal(1098, analysis.NextFetchCount);
    }

    [Fact]
    public void SelectCandidates_WhenRetryResponseContains599To606_ProcessesIlhamAndTahira()
    {
        var records = new List<BuildTrack.Domain.Dahua.DahuaAccessRecord>();
        records.AddRange(Records(1, 598));
        records.AddRange(new[]
        {
            Record(599, "1", "ilham"),
            Record(600, "1", "ilham"),
            Record(601, "1", "ilham"),
            Record(602, "2", "Tahira"),
            Record(603, "2", "Tahira"),
            Record(604, "1", "ilham"),
            Record(605, "2", "Tahira"),
            Record(606, "2", "Tahira"),
        });

        var candidates = DahuaCgiPollingPlanner.SelectCandidates(records, 598);

        Assert.Equal(8, candidates.Count);
        Assert.Equal(599, candidates[0].RecNo);
        Assert.Equal(606, candidates[^1].RecNo);
        Assert.Contains(candidates, record => record.UserId == "2" && record.CardName == "Tahira" && record.RecNo == 606);
    }

    private static BuildTrack.Domain.Dahua.DahuaAccessRecord Record(int recNo, string userId, string cardName) => new()
    {
        RecNo = recNo,
        UserId = userId,
        CardName = cardName,
        StatusRaw = "1",
        MethodRaw = "15",
        Type = "Entry",
        CreateTime = DateTimeOffset.UtcNow.AddSeconds(recNo),
    };
    private static IReadOnlyList<BuildTrack.Domain.Dahua.DahuaAccessRecord> Records(int startRecNo, int endRecNo) => Enumerable.Range(startRecNo, endRecNo - startRecNo + 1)
        .Select(recNo => new BuildTrack.Domain.Dahua.DahuaAccessRecord
        {
            RecNo = recNo,
            UserId = "1",
            CardName = "ilham",
            StatusRaw = "1",
            MethodRaw = "15",
            Type = "Entry",
            CreateTime = DateTimeOffset.UtcNow.AddSeconds(recNo),
        })
        .ToList();
}






public sealed class DahuaUnknownFacePolicyTests
{
    [Fact]
    public void IsUnknownFace_WhenStatusFailedFaceAndNoIdentityWithSnapshot_ReturnsTrue()
    {
        var record = new BuildTrack.Domain.Dahua.DahuaAccessRecord
        {
            RecNo = 700,
            StatusRaw = "0",
            MethodRaw = "15",
            Type = "Entry",
            UserId = string.Empty,
            CardName = string.Empty,
            Url = "/mnt/appdata1/userpic/SnapShot/2026-07-13/unknown.jpg",
            RawFields = new Dictionary<string, string?> { ["ErrorCode"] = "16" },
            CreateTime = DateTimeOffset.UtcNow,
        };

        Assert.True(DahuaUnknownFacePolicy.IsUnknownFace(record));
    }

    [Fact]
    public void IsUnknownFace_WhenKnownSuccessfulWorker_ReturnsFalse()
    {
        var record = new BuildTrack.Domain.Dahua.DahuaAccessRecord
        {
            RecNo = 701,
            StatusRaw = "1",
            MethodRaw = "15",
            Type = "Entry",
            UserId = "1",
            CardName = "ilham",
            Url = "/mnt/appdata1/userpic/SnapShot/2026-07-13/known.jpg",
            CreateTime = DateTimeOffset.UtcNow,
        };

        Assert.False(DahuaUnknownFacePolicy.IsUnknownFace(record));
    }

    [Fact]
    public void SelectProcessableRecords_IncludesUnknownFacesSoCgiCursorCanAdvance()
    {
        var records = new[]
        {
            new BuildTrack.Domain.Dahua.DahuaAccessRecord
            {
                RecNo = 801,
                StatusRaw = "0",
                MethodRaw = "15",
                Type = "Entry",
                UserId = string.Empty,
                CardName = string.Empty,
                Url = "/mnt/appdata1/userpic/SnapShot/2026-07-13/unknown.jpg",
                CreateTime = DateTimeOffset.UtcNow,
            },
            new BuildTrack.Domain.Dahua.DahuaAccessRecord
            {
                RecNo = 802,
                StatusRaw = "1",
                MethodRaw = "15",
                Type = "Entry",
                UserId = "1",
                CardName = "ilham",
                Url = "/mnt/appdata1/userpic/SnapShot/2026-07-13/known.jpg",
                CreateTime = DateTimeOffset.UtcNow,
            },
        };

        var processable = DahuaCgiPollingPlanner.SelectProcessableRecords(records, 800);

        Assert.Equal(2, processable.Count);
        Assert.Contains(processable, record => record.RecNo == 801 && DahuaUnknownFacePolicy.IsUnknownFace(record));
        Assert.Contains(processable, record => record.RecNo == 802);
    }
}
