using BuildTrack.Infrastructure;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Security;
using BuildTrack.Worker.Dahua.Services;
using System.Runtime.InteropServices;

if (args.Contains("--sdk-check"))
{
    var library = NativeLibrary.Load("/app/vendor/dahua-netsdk/linux-x64/libdhnetsdk.so");
    try
    {
        foreach (var name in new[] { "CLIENT_Init", "CLIENT_Cleanup", "CLIENT_ListenServer", "CLIENT_ResponseDevReg", "CLIENT_LoginEx", "CLIENT_StartListenEx", "CLIENT_RealLoadPictureEx", "CLIENT_StopLoadPic" })
            if (!NativeLibrary.TryGetExport(library, name, out _))
                throw new InvalidOperationException($"Dahua SDK export missing: {name}");
        Console.WriteLine("Dahua runtime SDK loaded and required exports verified.");
    }
    finally { NativeLibrary.Free(library); }
    return;
}

var builder = Host.CreateApplicationBuilder(args);
ProductionConfiguration.Validate(builder.Configuration, builder.Environment.IsDevelopment(), api: false);
builder.Services.AddBuildTrackInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DahuaActiveRegisterHostedService>();
builder.Services.AddHostedService<DahuaSmartEventWatchdogHostedService>();
builder.Services.AddHostedService<DahuaSimulatorHostedService>();
builder.Services.AddHostedService<DahuaCgiPollingHostedService>();
builder.Services.AddHostedService<WorkerHealthHostedService>();

var host = builder.Build();

if (!string.Equals(builder.Configuration["BUILDTRACK_INITIALIZE_DATABASE"], "false", StringComparison.OrdinalIgnoreCase))
{
    using var scope = host.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BuildTrackDbContext>();
    await DbInitializer.EnsureDatabaseAsync(db, builder.Configuration);
}

await host.RunAsync();

