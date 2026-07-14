using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Dahua;

public enum DahuaCgiDebounceDecisionType
{
    Insert,
    Skip
}

public sealed record DahuaCgiDebounceDecision(DahuaCgiDebounceDecisionType Type, string Reason)
{
    public bool ShouldSkip => Type == DahuaCgiDebounceDecisionType.Skip;
    public static DahuaCgiDebounceDecision Insert(string reason) => new(DahuaCgiDebounceDecisionType.Insert, reason);
    public static DahuaCgiDebounceDecision Skip(string reason) => new(DahuaCgiDebounceDecisionType.Skip, reason);
}

public static class DahuaCgiDebouncePolicy
{
    public static DahuaCgiDebounceDecision Decide(
        bool hasDebouncedRawEvent,
        AttendanceSession? openSession,
        DateTimeOffset eventTime,
        TimeSpan minCheckoutGap)
    {
        if (!hasDebouncedRawEvent) return DahuaCgiDebounceDecision.Insert("no debounce duplicate");

        if (openSession is null)
        {
            return DahuaCgiDebounceDecision.Skip("no open session");
        }

        if (eventTime >= openSession.CheckInTime.Add(minCheckoutGap))
        {
            return DahuaCgiDebounceDecision.Insert("open session checkout gap passed");
        }

        return DahuaCgiDebounceDecision.Skip("checkout gap not reached");
    }
}
