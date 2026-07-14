using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Services;

namespace BuildTrack.Tests;

public sealed class AttendanceSessionPlannerTests
{
    private static readonly TimeSpan MinGap = TimeSpan.FromMinutes(15);

    [Fact]
    public void FirstEventOfDayCreatesOpenSessionDecision()
    {
        var decision = AttendanceSessionPlanner.DecideSingleDeviceToggle(null, DateTimeOffset.Parse("2026-07-10T04:00:00+00:00"), MinGap);

        Assert.Equal(AttendanceSessionDecisionType.CreateCheckIn, decision.Type);
    }

    [Fact]
    public void RepeatedEventWithinMinimumCheckoutGapDoesNotCloseSession()
    {
        var openSession = OpenSession("1", DateTimeOffset.Parse("2026-07-10T04:00:00+00:00"));

        var decision = AttendanceSessionPlanner.DecideSingleDeviceToggle(openSession, DateTimeOffset.Parse("2026-07-10T04:10:00+00:00"), MinGap);

        Assert.Equal(AttendanceSessionDecisionType.Ignore, decision.Type);
    }

    [Fact]
    public void EventAfterMinimumCheckoutGapClosesSession()
    {
        var openSession = OpenSession("1", DateTimeOffset.Parse("2026-07-10T04:00:00+00:00"));

        var decision = AttendanceSessionPlanner.DecideSingleDeviceToggle(openSession, DateTimeOffset.Parse("2026-07-10T04:16:00+00:00"), MinGap);

        Assert.Equal(AttendanceSessionDecisionType.CloseCheckOut, decision.Type);
    }

    [Fact]
    public void WorkDateIsCalculatedUsingBakuTimezone()
    {
        var baku = TimeZoneInfo.CreateCustomTimeZone("Test +04", TimeSpan.FromHours(4), "Test +04", "Test +04");
        var utcEventTime = DateTimeOffset.Parse("2026-07-09T22:12:03+00:00");

        var workDate = AttendanceSessionPlanner.CalculateWorkDate(utcEventTime, baku);

        Assert.Equal(new DateOnly(2026, 7, 10), workDate);
    }

    [Fact]
    public void MultipleWorkersHaveSeparateSessionDecisions()
    {
        var workerOneOpen = OpenSession("1", DateTimeOffset.Parse("2026-07-10T04:00:00+00:00"));

        var workerOneDecision = AttendanceSessionPlanner.DecideSingleDeviceToggle(workerOneOpen, DateTimeOffset.Parse("2026-07-10T04:20:00+00:00"), MinGap);
        var workerTwoDecision = AttendanceSessionPlanner.DecideSingleDeviceToggle(null, DateTimeOffset.Parse("2026-07-10T04:20:00+00:00"), MinGap);

        Assert.Equal(AttendanceSessionDecisionType.CloseCheckOut, workerOneDecision.Type);
        Assert.Equal(AttendanceSessionDecisionType.CreateCheckIn, workerTwoDecision.Type);
    }

    [Fact]
    public void ClosedSessionPlusLaterEventCreatesNewOpenSessionDecision()
    {
        var decision = AttendanceSessionPlanner.DecideSingleDeviceToggle(null, DateTimeOffset.Parse("2026-07-10T09:00:00+00:00"), MinGap);

        Assert.Equal(AttendanceSessionDecisionType.CreateCheckIn, decision.Type);
    }

    [Fact]
    public void DeviceDirectionExitClosesOpenSessionAfterGap()
    {
        var openSession = OpenSession("1", DateTimeOffset.Parse("2026-07-10T04:00:00+00:00"));

        var decision = AttendanceSessionPlanner.DecideDeviceDirection(openSession, AttendanceDirection.Exit, DateTimeOffset.Parse("2026-07-10T12:00:00+00:00"), MinGap);

        Assert.Equal(AttendanceSessionDecisionType.CloseCheckOut, decision.Type);
    }


    [Fact]
    public void DailySession_FirstEventCreatesOneOpenSessionDecision()
    {
        var decision = AttendanceSessionPlanner.DecideSingleDeviceDailySession(null, DateTimeOffset.Parse("2026-07-10T04:00:00+00:00"), MinGap, updateCheckoutToLastSeen: true);

        Assert.Equal(AttendanceSessionDecisionType.CreateCheckIn, decision.Type);
    }

    [Fact]
    public void DailySession_SecondEventAfterMinimumGapClosesSameSession()
    {
        var openSession = OpenSession("1", DateTimeOffset.Parse("2026-07-10T04:00:00+00:00"));

        var decision = AttendanceSessionPlanner.DecideSingleDeviceDailySession(openSession, DateTimeOffset.Parse("2026-07-10T04:20:00+00:00"), MinGap, updateCheckoutToLastSeen: true);

        Assert.Equal(AttendanceSessionDecisionType.CloseCheckOut, decision.Type);
    }

    [Fact]
    public void DailySession_ThirdEventSameDayDoesNotCreateAnotherSessionAndUpdatesCheckoutWhenEnabled()
    {
        var closedSession = ClosedSession("1", DateTimeOffset.Parse("2026-07-10T04:00:00+00:00"), DateTimeOffset.Parse("2026-07-10T04:20:00+00:00"));

        var decision = AttendanceSessionPlanner.DecideSingleDeviceDailySession(closedSession, DateTimeOffset.Parse("2026-07-10T05:00:00+00:00"), MinGap, updateCheckoutToLastSeen: true);

        Assert.Equal(AttendanceSessionDecisionType.UpdateCheckOut, decision.Type);
    }

    [Fact]
    public void DailySession_ClosedSessionIgnoresLaterEventWhenUpdateDisabled()
    {
        var closedSession = ClosedSession("1", DateTimeOffset.Parse("2026-07-10T04:00:00+00:00"), DateTimeOffset.Parse("2026-07-10T04:20:00+00:00"));

        var decision = AttendanceSessionPlanner.DecideSingleDeviceDailySession(closedSession, DateTimeOffset.Parse("2026-07-10T05:00:00+00:00"), MinGap, updateCheckoutToLastSeen: false);

        Assert.Equal(AttendanceSessionDecisionType.Ignore, decision.Type);
    }

    [Fact]
    public void DailySession_EventBeforeMinimumGapDoesNotCloseSession()
    {
        var openSession = OpenSession("1", DateTimeOffset.Parse("2026-07-10T04:00:00+00:00"));

        var decision = AttendanceSessionPlanner.DecideSingleDeviceDailySession(openSession, DateTimeOffset.Parse("2026-07-10T04:10:00+00:00"), MinGap, updateCheckoutToLastSeen: true);

        Assert.Equal(AttendanceSessionDecisionType.Ignore, decision.Type);
    }


    [Fact]
    public void LiveStatus_OldOpenSessionIsNotActiveForCurrentWorkDate()
    {
        var sessions = new[]
        {
            OpenSession("1", DateTimeOffset.Parse("2026-07-10T05:00:00+00:00"), new DateOnly(2026, 7, 10)),
        };

        var active = AttendanceSessionPlanner.SelectCurrentOpenSessions(sessions, new DateOnly(2026, 7, 14));

        Assert.Empty(active);
    }

    [Fact]
    public void LiveStatus_CurrentWorkDateOpenSessionIsActive()
    {
        var sessions = new[]
        {
            OpenSession("1", DateTimeOffset.Parse("2026-07-10T05:00:00+00:00"), new DateOnly(2026, 7, 10)),
            OpenSession("2", DateTimeOffset.Parse("2026-07-14T05:00:00+00:00"), new DateOnly(2026, 7, 14)),
        };

        var active = AttendanceSessionPlanner.SelectCurrentOpenSessions(sessions, new DateOnly(2026, 7, 14));

        Assert.Single(active);
        Assert.Equal("2", active[0].WorkerExternalId);
    }

    [Fact]
    public void OneCameraPresence_FirstEventCreatesOpenPresenceSessionDecision()
    {
        var decision = AttendanceSessionPlanner.DecideOneCameraPresence(null, DateTimeOffset.Parse("2026-07-14T05:00:00+00:00"));

        Assert.Equal(AttendanceSessionDecisionType.CreateCheckIn, decision.Type);
    }

    [Fact]
    public void OneCameraPresence_RepeatedEventUpdatesLastSeenWithoutCheckout()
    {
        var openSession = OpenSession("1", DateTimeOffset.Parse("2026-07-14T05:00:00+00:00"));
        openSession.LastSeenTime = openSession.CheckInTime;

        var decision = AttendanceSessionPlanner.DecideOneCameraPresence(openSession, DateTimeOffset.Parse("2026-07-14T06:00:00+00:00"));

        Assert.Equal(AttendanceSessionDecisionType.UpdateLastSeen, decision.Type);
        Assert.Null(openSession.CheckOutTime);
        Assert.Equal(AttendanceSessionStatus.Open, openSession.Status);
    }

    [Fact]
    public void OneCameraPresence_DisplayStatusDoesNotShowExitedForOpenSession()
    {
        var now = DateTimeOffset.Parse("2026-07-14T06:10:00+00:00");

        var status = AttendanceSessionPlanner.BuildDisplayStatus(
            AttendanceSessionStatus.Open,
            null,
            DateTimeOffset.Parse("2026-07-14T06:00:00+00:00"),
            now);

        Assert.NotEqual("Çıxıb", status);
        Assert.Equal("Az əvvəl göründü", status);
    }

    [Fact]
    public void ConfirmedExitSourceStillDisplaysConfirmedCheckout()
    {
        var status = AttendanceSessionPlanner.BuildDisplayStatus(
            AttendanceSessionStatus.Closed,
            "DeviceDirection",
            DateTimeOffset.Parse("2026-07-14T12:00:00+00:00"),
            DateTimeOffset.Parse("2026-07-14T12:05:00+00:00"));

        Assert.Equal("Təsdiqli çıxış", status);
    }
    private static AttendanceSession ClosedSession(string workerExternalId, DateTimeOffset checkInTime, DateTimeOffset checkOutTime) => new()
    {
        DeviceId = Guid.NewGuid(),
        SiteId = Guid.NewGuid(),
        WorkerExternalId = workerExternalId,
        WorkerName = workerExternalId,
        WorkDate = DateOnly.FromDateTime(checkInTime.Date),
        CheckInEventId = Guid.NewGuid(),
        CheckInTime = checkInTime,
        CheckOutEventId = Guid.NewGuid(),
        CheckOutTime = checkOutTime,
        Status = AttendanceSessionStatus.Closed,
        Source = "dahua_cgi_polling",
    };
    private static AttendanceSession OpenSession(string workerExternalId, DateTimeOffset checkInTime, DateOnly workDate) => new()
    {
        DeviceId = Guid.NewGuid(),
        SiteId = Guid.NewGuid(),
        WorkerExternalId = workerExternalId,
        WorkerName = workerExternalId,
        WorkDate = workDate,
        CheckInEventId = Guid.NewGuid(),
        CheckInTime = checkInTime,
        Status = AttendanceSessionStatus.Open,
        Source = "dahua_cgi_polling",
    };    private static AttendanceSession OpenSession(string workerExternalId, DateTimeOffset checkInTime) => new()
    {
        DeviceId = Guid.NewGuid(),
        SiteId = Guid.NewGuid(),
        WorkerExternalId = workerExternalId,
        WorkerName = workerExternalId,
        WorkDate = DateOnly.FromDateTime(checkInTime.Date),
        CheckInEventId = Guid.NewGuid(),
        CheckInTime = checkInTime,
        Status = AttendanceSessionStatus.Open,
        Source = "dahua_cgi_polling",
    };
}




