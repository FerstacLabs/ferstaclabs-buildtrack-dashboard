using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Services;

public interface IAttendanceSessionService
{
    Task ProcessEventAsync(AttendanceEvent attendanceEvent, CancellationToken cancellationToken = default);
}
