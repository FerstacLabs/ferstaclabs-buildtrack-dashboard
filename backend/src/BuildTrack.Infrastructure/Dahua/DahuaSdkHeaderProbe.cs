namespace BuildTrack.Infrastructure.Dahua;

public interface IDahuaSdkHeaderProbe
{
    bool HasHeadersOrSamples { get; }
    string SearchRoot { get; }
    IReadOnlyList<string> MatchedFiles { get; }
    string MissingHeadersWarning { get; }
}

public sealed class DahuaSdkHeaderProbe : IDahuaSdkHeaderProbe
{
    private static readonly string[] Patterns = ["*.h", "*.hpp", "*.c", "*.cpp", "*.cs", "*.java"];

    public bool HasHeadersOrSamples => MatchedFiles.Count > 0;

    public string SearchRoot { get; }

    public IReadOnlyList<string> MatchedFiles { get; }

    public string MissingHeadersWarning => "Native binaries are present, but Dahua SDK headers/samples are missing. Exact access-event struct binding cannot be completed safely.";

    public DahuaSdkHeaderProbe()
    {
        SearchRoot = ResolveSearchRoot();
        MatchedFiles = Directory.Exists(SearchRoot)
            ? Patterns.SelectMany(pattern => Directory.EnumerateFiles(SearchRoot, pattern, SearchOption.AllDirectories)).Take(50).ToArray()
            : [];
    }

    private static string ResolveSearchRoot()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "vendor", "dahua-netsdk"),
            Path.Combine(Directory.GetCurrentDirectory(), "vendor", "dahua-netsdk"),
            Path.Combine(AppContext.BaseDirectory, "vendor", "dahua-netsdk"),
            Path.Combine("/app", "vendor", "dahua-netsdk"),
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }
}
