using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Tests;

public sealed class DahuaCgiCursorPolicyTests
{
    [Fact]
    public void SimulatorEventWithHugeRawRecNoDoesNotAdvanceCgiCursor()
    {
        var shouldAdvance = DahuaCgiCursorPolicy.ShouldAdvanceCgiCursor(
            "simulator",
            1_783_638_962_559,
            currentCgiLastRecNo: 517);

        Assert.False(shouldAdvance);
    }

    [Fact]
    public void CgiPollingEventWithRealRecNoAdvancesCgiCursor()
    {
        var shouldAdvance = DahuaCgiCursorPolicy.ShouldAdvanceCgiCursor(
            "dahua_cgi_polling",
            518,
            currentCgiLastRecNo: 517);

        Assert.True(shouldAdvance);
    }

    [Fact]
    public void CgiPollingUsesCgiLastRecNoBeforeLegacyLastRecNo()
    {
        var resolution = DahuaCgiCursorPolicy.Resolve(cgiLastRecNo: 517, legacyLastRecNo: 1_783_638_962_559);

        Assert.Equal(517, resolution.LastRecNo);
        Assert.False(resolution.WasPolluted);
        Assert.Equal("CgiLastRecNo", resolution.SourceField);
    }

    [Fact]
    public void PollutedLegacyLastRecNoIsIgnoredAndRecoveredFromZero()
    {
        var resolution = DahuaCgiCursorPolicy.Resolve(cgiLastRecNo: null, legacyLastRecNo: 1_783_638_962_559);

        Assert.Equal(0, resolution.LastRecNo);
        Assert.True(resolution.WasPolluted);
        Assert.Equal("LastRecNo", resolution.SourceField);
    }

    [Fact]
    public void PollutedCgiLastRecNoIsIgnoredAndRecoveredFromZero()
    {
        var resolution = DahuaCgiCursorPolicy.Resolve(cgiLastRecNo: 1_783_638_962_559, legacyLastRecNo: 517);

        Assert.Equal(0, resolution.LastRecNo);
        Assert.True(resolution.WasPolluted);
        Assert.Equal("CgiLastRecNo", resolution.SourceField);
    }
}
