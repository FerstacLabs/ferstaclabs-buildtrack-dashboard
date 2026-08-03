namespace BuildTrack.Infrastructure.Tenancy;

public static class TenantAccessPolicy
{
    public static bool CanAccessTenant(Guid? currentTenantId, Guid entityTenantId) =>
        currentTenantId is null || currentTenantId.Value == entityTenantId;
}
