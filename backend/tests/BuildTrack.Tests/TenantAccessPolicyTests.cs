using BuildTrack.Infrastructure.Tenancy;

namespace BuildTrack.Tests;

public sealed class TenantAccessPolicyTests
{
    [Fact]
    public void MatchingTenantCanAccessEntity()
    {
        var tenantId = Guid.NewGuid();

        Assert.True(TenantAccessPolicy.CanAccessTenant(tenantId, tenantId));
    }

    [Fact]
    public void DifferentTenantCannotAccessEntity()
    {
        Assert.False(TenantAccessPolicy.CanAccessTenant(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void EmptyTenantContextIsAllowedForWorkerProcesses()
    {
        Assert.True(TenantAccessPolicy.CanAccessTenant(null, Guid.NewGuid()));
    }
}
