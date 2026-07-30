using System.Text.Json;
using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Dahua;

public static class DahuaVerifiedAttendancePayload
{
    public static bool IsVerifiedAttendance(AttendanceEvent attendanceEvent)
    {
        if (!string.Equals(attendanceEvent.Source, DahuaEventSourceExtensions.ActiveRegisterSource, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsVerifiedActiveRegisterPayload(attendanceEvent.RawPayloadJson);
    }

    public static bool IsVerifiedActiveRegisterPayload(string? rawPayloadJson)
    {
        if (string.IsNullOrWhiteSpace(rawPayloadJson)) return false;

        try
        {
            using var document = JsonDocument.Parse(rawPayloadJson);
            var root = document.RootElement;
            var classification = GetString(root, "Classification");
            if (!string.Equals(classification, "RecognizedAttendance", StringComparison.OrdinalIgnoreCase)) return false;
            if (!IsTrue(GetString(root, "IdentityVerified"))) return false;
            if (IsTrue(GetString(root, "CardNameMismatch"))) return false;
            if (string.Equals(GetString(root, "IdentityRisk"), "High", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static string? GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }
}
