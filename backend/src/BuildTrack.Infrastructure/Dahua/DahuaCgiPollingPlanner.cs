using BuildTrack.Domain.Dahua;

namespace BuildTrack.Infrastructure.Dahua;

public sealed record DahuaCgiFetchSettings(int InitialFetchCount, int MaxFetchCount, int GrowthFactor, int FetchLookahead);

public sealed record DahuaCgiFetchAnalysis(
    int CurrentFetchCount,
    int FetchedCount,
    long MaxRecNoInResponse,
    long LastRecNo,
    int TargetFetchCount,
    bool ShouldRetry,
    int NextFetchCount,
    bool MaxFetchReachedWithoutNewerRecords);

public static class DahuaCgiPollingPlanner
{
    public const int DefaultInitialFetchCount = 100;
    public const int DefaultMaxFetchCount = 5000;
    public const int DefaultGrowthFactor = 2;
    public const int DefaultFetchLookahead = 500;
    public const int MinFetchCount = 20;

    public static DahuaCgiFetchSettings CreateSettings(string? initialFetchCount, string? maxFetchCount, string? growthFactor, string? fetchLookahead = null)
    {
        var initial = ParseClampedInt(initialFetchCount, DefaultInitialFetchCount, MinFetchCount, DefaultMaxFetchCount);
        var max = ParseClampedInt(maxFetchCount, DefaultMaxFetchCount, MinFetchCount, DefaultMaxFetchCount);
        if (max < initial) max = initial;
        var growth = ParseClampedInt(growthFactor, DefaultGrowthFactor, 2, 10);
        var lookahead = ParseClampedInt(fetchLookahead, DefaultFetchLookahead, MinFetchCount, DefaultMaxFetchCount);
        return new DahuaCgiFetchSettings(initial, max, growth, lookahead);
    }

    public static Uri BuildRecordFinderUri(string host, int fetchCount)
    {
        var normalizedHost = host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? host.TrimEnd('/')
            : $"http://{host.TrimEnd('/')}";
        return new Uri($"{normalizedHost}/cgi-bin/recordFinder.cgi?action=find&name=AccessControlCardRec&count={Math.Max(MinFetchCount, fetchCount)}");
    }

    public static DahuaCgiFetchAnalysis AnalyzeFetch(IReadOnlyCollection<DahuaAccessRecord> records, long lastRecNo, int currentFetchCount, DahuaCgiFetchSettings settings)
    {
        var maxRecNo = records.Where(record => record.RecNo is not null).Select(record => record.RecNo!.Value).DefaultIfEmpty(0).Max();
        var fetchedCount = records.Count;
        var targetFetchCount = CalculateTargetFetchCount(lastRecNo, settings);
        var newerRecordsVisible = maxRecNo > lastRecNo;
        var canGrowTowardTarget = currentFetchCount < targetFetchCount && currentFetchCount < settings.MaxFetchCount;
        var shouldRetry = !newerRecordsVisible && canGrowTowardTarget;
        var nextFetchCount = shouldRetry ? NextFetchCount(currentFetchCount, targetFetchCount, settings) : currentFetchCount;
        var maxReachedWithoutNewer = !newerRecordsVisible && !canGrowTowardTarget;
        return new DahuaCgiFetchAnalysis(currentFetchCount, fetchedCount, maxRecNo, lastRecNo, targetFetchCount, shouldRetry, nextFetchCount, maxReachedWithoutNewer);
    }

    public static int CalculateTargetFetchCount(long lastRecNo, DahuaCgiFetchSettings settings)
    {
        var desired = lastRecNo + settings.FetchLookahead;
        if (desired > int.MaxValue) desired = int.MaxValue;
        return Math.Clamp((int)Math.Max(settings.InitialFetchCount, desired), settings.InitialFetchCount, settings.MaxFetchCount);
    }

    public static int NextFetchCount(int currentFetchCount, int targetFetchCount, DahuaCgiFetchSettings settings)
    {
        var grown = currentFetchCount * settings.GrowthFactor;
        if (grown <= currentFetchCount) grown = currentFetchCount + 1;
        var next = Math.Min(grown, targetFetchCount);
        if (next <= currentFetchCount) next = Math.Min(currentFetchCount + 1, targetFetchCount);
        return Math.Min(next, settings.MaxFetchCount);
    }

    public static int NextFetchCount(int currentFetchCount, DahuaCgiFetchSettings settings) =>
        NextFetchCount(currentFetchCount, settings.MaxFetchCount, settings);


    public static IReadOnlyList<DahuaAccessRecord> SelectProcessableRecords(IEnumerable<DahuaAccessRecord> records, long lastRecNo) => records
        .Where(record => record.RecNo is not null && record.RecNo > lastRecNo)
        .Where(record => IsKnownAttendanceCandidate(record) || DahuaUnknownFacePolicy.IsUnknownFace(record))
        .OrderBy(record => record.RecNo)
        .ToList();

    private static bool IsKnownAttendanceCandidate(DahuaAccessRecord record) =>
        record.StatusRaw == "1"
        && !string.IsNullOrWhiteSpace(record.UserId)
        && !string.IsNullOrWhiteSpace(record.CardName);
    public static IReadOnlyList<DahuaAccessRecord> SelectCandidates(IEnumerable<DahuaAccessRecord> records, long lastRecNo) => records
        .Where(record => record.StatusRaw == "1")
        .Where(record => !string.IsNullOrWhiteSpace(record.UserId))
        .Where(record => !string.IsNullOrWhiteSpace(record.CardName))
        .Where(record => record.RecNo is not null && record.RecNo > lastRecNo)
        .OrderBy(record => record.RecNo)
        .ToList();

    public static long AdvanceLastRecNoForProcessedRecords(IEnumerable<DahuaAccessRecord> processedRecords, long lastRecNo) => processedRecords
        .Where(record => record.RecNo is not null)
        .Select(record => record.RecNo!.Value)
        .DefaultIfEmpty(lastRecNo)
        .Max();

    private static int ParseClampedInt(string? value, int defaultValue, int min, int max)
    {
        if (!int.TryParse(value, out var parsed)) return defaultValue;
        return Math.Clamp(parsed, min, max);
    }
}

