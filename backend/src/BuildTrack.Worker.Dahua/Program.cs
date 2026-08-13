using BuildTrack.Infrastructure;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Worker.Dahua.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddBuildTrackInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DahuaActiveRegisterHostedService>();
builder.Services.AddHostedService<DahuaSmartEventWatchdogHostedService>();
builder.Services.AddHostedService<DahuaSimulatorHostedService>();
builder.Services.AddHostedService<DahuaCgiPollingHostedService>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BuildTrackDbContext>();
    await DbInitializer.EnsureDatabaseAsync(db, builder.Configuration);
}

await host.RunAsync();

