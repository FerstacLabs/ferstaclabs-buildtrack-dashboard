using BuildTrack.Infrastructure.Dahua;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Security;
using BuildTrack.Infrastructure.Services;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildTrack.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBuildTrackInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["POSTGRES_CONNECTION_STRING"]
            ?? configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=buildtrack;Username=buildtrack;Password=buildtrack";

        services.AddDbContext<BuildTrackDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IPasswordProtector, AesPasswordProtector>();
        services.AddScoped<IAttendanceIngestionService, AttendanceIngestionService>();
        services.AddScoped<IDahuaAccessRecordIngestionPipeline, DahuaAccessRecordIngestionPipeline>();
        services.AddScoped<IAttendanceSessionService, AttendanceSessionService>();
        services.AddScoped<IWorkerCameraIdentityResolver, WorkerCameraIdentityResolver>();
        services.AddScoped<IWorkerSiteAssignmentService, WorkerSiteAssignmentService>();
        services.AddScoped<IFieldAccessService, FieldAccessService>();
        services.AddScoped<IWarehouseAvailabilityService, WarehouseAvailabilityService>();
        services.AddScoped<IWarehouseUsagePolicyService, WarehouseUsagePolicyService>();
        services.AddScoped<ISupplyAttachmentStorage, SupplyAttachmentStorage>();
        services.AddScoped<ISupplyChainService, SupplyChainService>();
        services.AddScoped<ISecuritySnapshotStore, SecuritySnapshotStore>();
        services.AddScoped<ISecurityEventService, SecurityEventService>();
        services.AddScoped<IDeviceConnectionLogger, DeviceConnectionLogger>();
        services.AddSingleton<IDahuaNativeLibraryProbe, DahuaNativeLibraryProbe>();
        services.AddSingleton<IDahuaSdkHeaderProbe, DahuaSdkHeaderProbe>();
        services.AddSingleton<IDahuaActiveRegisterSdk, DahuaNetSdkActiveRegisterService>();
        return services;
    }
}





