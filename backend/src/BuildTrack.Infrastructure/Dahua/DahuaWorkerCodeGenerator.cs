using System.Text.RegularExpressions;
using BuildTrack.Domain.Entities;

namespace BuildTrack.Infrastructure.Dahua;

public static partial class DahuaWorkerCodeGenerator
{
    public static string NextWorkerCode(IReadOnlyCollection<Worker> workers)
    {
        var usedCodes = workers
            .Select(worker => worker.ExternalWorkerCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var maxNumber = usedCodes
            .Select(code => WorkerCodeRegex().Match(code.Trim()))
            .Where(match => match.Success)
            .Select(match => int.TryParse(match.Groups[1].Value, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();

        for (var next = maxNumber + 1; next < 10000; next++)
        {
            var candidate = $"W-{next:0000}";
            if (!usedCodes.Contains(candidate)) return candidate;
        }

        return $"W-{DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000:00000}";
    }

    [GeneratedRegex("^W-(\\d{4})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WorkerCodeRegex();
}
