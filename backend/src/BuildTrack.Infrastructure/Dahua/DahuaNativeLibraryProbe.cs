using System.Runtime.InteropServices;

namespace BuildTrack.Infrastructure.Dahua;

public interface IDahuaNativeLibraryProbe
{
    bool HasNativeSdk { get; }
    string RuntimeFolder { get; }
    string ExpectedPath { get; }
    string? NativeLibraryPath { get; }
    string? LastLoadError { get; }
    bool TryLoadNativeSdk(out IntPtr libraryHandle, out string? error);
}

public sealed class DahuaNativeLibraryProbe : IDahuaNativeLibraryProbe
{
    private string? _lastLoadError;

    public string RuntimeFolder { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";

    public string ExpectedPath { get; }

    public string? NativeLibraryPath { get; }

    public string? LastLoadError => _lastLoadError;

    public bool HasNativeSdk => NativeLibraryPath is not null;

    public DahuaNativeLibraryProbe()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "vendor", "dahua-netsdk", RuntimeFolder),
            Path.Combine(Directory.GetCurrentDirectory(), "vendor", "dahua-netsdk", RuntimeFolder),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "vendor", "dahua-netsdk", RuntimeFolder),
            Path.Combine("/app", "vendor", "dahua-netsdk", RuntimeFolder),
        };

        ExpectedPath = candidates.First();
        NativeLibraryPath = candidates
            .Select(GetSdkLibraryPath)
            .FirstOrDefault(path => path is not null);
    }

    public bool TryLoadNativeSdk(out IntPtr libraryHandle, out string? error)
    {
        libraryHandle = IntPtr.Zero;
        error = null;

        if (NativeLibraryPath is null)
        {
            error = $"Dahua NetSDK library was not found. Expected folder: {ExpectedPath}";
            _lastLoadError = error;
            return false;
        }

        try
        {
            libraryHandle = NativeLibrary.Load(NativeLibraryPath);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to load Dahua NetSDK library '{NativeLibraryPath}': {ex.Message}";
            _lastLoadError = error;
            return false;
        }
    }

    private string? GetSdkLibraryPath(string folder)
    {
        if (!Directory.Exists(folder)) return null;

        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "dhnetsdk.dll"
            : "libdhnetsdk.so";
        var exact = Path.Combine(folder, fileName);
        if (File.Exists(exact)) return exact;

        return Directory.EnumerateFiles(folder)
            .FirstOrDefault(file => file.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
    }
}
