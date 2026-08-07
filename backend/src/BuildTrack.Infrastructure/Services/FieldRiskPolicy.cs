using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public static class FieldRiskPolicy
{
    public static int CalculateRiskDelta(SupervisorWorkerEventType eventType) => eventType switch
    {
        SupervisorWorkerEventType.Late => 1,
        SupervisorWorkerEventType.LeftEarly => 1,
        SupervisorWorkerEventType.Absent => 3,
        SupervisorWorkerEventType.Permission => 0,
        SupervisorWorkerEventType.Medical => 0,
        SupervisorWorkerEventType.SiteTransfer => 0,
        SupervisorWorkerEventType.SafetyWarning => 3,
        SupervisorWorkerEventType.ManualAttendanceCorrectionRequest => 1,
        _ => 0,
    };
}
