using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Tests;

public sealed class DahuaCgiDebouncePolicyTests
{
    private static readonly TimeSpan MinCheckoutGap = TimeSpan.FromMinutes(15);

    [Fact]
    public void OpenSessionWithCheckoutGapPassed_IsNotBlockedByDebounce()
    {
        var openSession = OpenSession(DateTimeOffset.Parse("2026-07-12T04:00:00+00:00"));

        var decision = DahuaCgiDebouncePolicy.Decide(
            hasDebouncedRawEvent: true,
            openSession,
            eventTime: DateTimeOffset.Parse("2026-07-12T04:20:00+00:00"),
            MinCheckoutGap);

        Assert.False(decision.ShouldSkip);
        Assert.Equal("open session checkout gap passed", decision.Reason);
    }

    [Fact]
    public void OpenSessionWithCheckoutGapNotPassed_IsSkippedSafely()
    {
        var openSession = OpenSession(DateTimeOffset.Parse("2026-07-12T04:00:00+00:00"));

        var decision = DahuaCgiDebouncePolicy.Decide(
            hasDebouncedRawEvent: true,
            openSession,
            eventTime: DateTimeOffset.Parse("2026-07-12T04:05:00+00:00"),
            MinCheckoutGap);

        Assert.True(decision.ShouldSkip);
        Assert.Equal("checkout gap not reached", decision.Reason);
    }

    [Fact]
    public void NoOpenSession_DebouncePreventsRepeatedDuplicateCheckIns()
    {
        var decision = DahuaCgiDebouncePolicy.Decide(
            hasDebouncedRawEvent: true,
            openSession: null,
            eventTime: DateTimeOffset.Parse("2026-07-12T04:05:00+00:00"),
            MinCheckoutGap);

        Assert.True(decision.ShouldSkip);
        Assert.Equal("no open session", decision.Reason);
    }

    [Fact]
    public void NoDebouncedRawEvent_IsInsertedNormally()
    {
        var decision = DahuaCgiDebouncePolicy.Decide(
            hasDebouncedRawEvent: false,
            openSession: null,
            eventTime: DateTimeOffset.Parse("2026-07-12T04:05:00+00:00"),
            MinCheckoutGap);

        Assert.False(decision.ShouldSkip);
        Assert.Equal("no debounce duplicate", decision.Reason);
    }

    private static AttendanceSession OpenSession(DateTimeOffset checkInTime) => new()
    {
        Id = Guid.NewGuid(),
        SiteId = Guid.NewGuid(),
        DeviceId = Guid.NewGuid(),
        WorkerExternalId = "1",
        WorkerName = "ilham",
        WorkDate = DateOnly.FromDateTime(checkInTime.Date),
        CheckInEventId = Guid.NewGuid(),
        CheckInTime = checkInTime,
        Status = AttendanceSessionStatus.Open,
        Source = "dahua_cgi_polling",
    };
}
