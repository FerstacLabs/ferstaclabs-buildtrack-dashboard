using BuildTrack.Infrastructure.Dahua;
using Xunit;

namespace BuildTrack.Tests;

public sealed class DahuaActiveRegisterLoginDiagnosticsTests
{
    [Fact]
    public void ZeroHandleAndZeroErrors_IsReportedAsPossibleMarshallingWarning()
    {
        var warning = DahuaActiveRegisterLoginDiagnostics.IsPossibleMarshallingWarning(0, 0, 0);

        Assert.True(warning);
    }

    [Fact]
    public void NonZeroError_IsNotReportedAsPossibleMarshallingWarning()
    {
        var warning = DahuaActiveRegisterLoginDiagnostics.IsPossibleMarshallingWarning(0, unchecked((int)0x80000004), 0);

        Assert.False(warning);
    }

    [Fact]
    public void FailedSubscription_ReleasesRegistrationKeyForFutureRetry()
    {
        var release = DahuaActiveRegisterLoginDiagnostics.ShouldReleaseRegistrationKeyAfterSubscription(subscribed: false);

        Assert.True(release);
    }

    [Fact]
    public void SuccessfulSubscription_KeepsRegistrationKeyHandled()
    {
        var release = DahuaActiveRegisterLoginDiagnostics.ShouldReleaseRegistrationKeyAfterSubscription(subscribed: true);

        Assert.False(release);
    }
}
