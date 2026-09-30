using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace BuildTrack.Tests;

public sealed class ProductionDeploymentTests
{
    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["BUILDTRACK_SECRET_KEY"] = new string('e', 40),
        ["JWT_SECRET"] = new string('j', 40),
        ["POSTGRES_CONNECTION_STRING"] = "Host=localhost;Database=test;Password=test-only-explicit-password",
    };

    [Theory]
    [InlineData("BUILDTRACK_SECRET_KEY")]
    [InlineData("JWT_SECRET")]
    [InlineData("POSTGRES_CONNECTION_STRING")]
    public void ProductionRejectsMissingSecrets(string key)
    {
        var settings = ValidSettings();
        settings.Remove(key);
        Assert.Throws<InvalidOperationException>(() => ProductionConfiguration.Validate(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), false, true));
    }

    [Fact]
    public void ProductionAcceptsExplicitSecretsAndRejectsWildcardCorsAndDestructiveSeed()
    {
        var settings = ValidSettings();
        ProductionConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), false, true);
        settings["CORS_ALLOWED_ORIGINS"] = "*";
        Assert.Throws<InvalidOperationException>(() => ProductionConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), false, true));
        settings.Remove("CORS_ALLOWED_ORIGINS");
        settings["SEED_BAKINITY_DEMO_RESET"] = "true";
        Assert.Throws<InvalidOperationException>(() => ProductionConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), false, true));
    }

    [PostgresFact]
    public async Task InitializationLockSerializesIndependentSessionsAndReleasesAfterFailure()
    {
        var connectionString = Environment.GetEnvironmentVariable("BUILDTRACK_TEST_POSTGRES")!;
        var first = await DatabaseInitializationLock.AcquireAsync(connectionString, CancellationToken.None);
        var second = DatabaseInitializationLock.AcquireAsync(connectionString, CancellationToken.None);
        await Task.Delay(200);
        Assert.False(second.IsCompleted);
        await first.DisposeAsync();
        var acquired = await second.WaitAsync(TimeSpan.FromSeconds(10));
        await acquired.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var failedInitialization = await DatabaseInitializationLock.AcquireAsync(connectionString, CancellationToken.None);
            throw new InvalidOperationException("Simulated initialization failure");
        });
        await using var recovered = await DatabaseInitializationLock.AcquireAsync(connectionString, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [PostgresFact]
    public async Task ConcurrentInitializersFinishAgainstSameDatabase()
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("BUILDTRACK_TEST_POSTGRES")).Options;
        await using var api = new BuildTrackDbContext(options);
        await using var worker = new BuildTrackDbContext(options);
        await Task.WhenAll(DbInitializer.EnsureDatabaseAsync(api), DbInitializer.EnsureDatabaseAsync(worker));
        Assert.True(await api.Database.CanConnectAsync());
    }

    [Fact]
    public async Task SkySnapRequiresExplicitPasswordWithoutCreatingTenant()
    {
        await using var db = new BuildTrackDbContext(new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SEED_SKYSNAP_DEMO"] = "true" }).Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => SkySnapDemoSeeder.SeedAsync(db, configuration, CancellationToken.None));
        Assert.Empty(await db.Tenants.ToListAsync());
    }

    [Fact]
    public async Task DemoReseedDoesNotResetExistingOwnerPassword()
    {
        await using var db = new BuildTrackDbContext(new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SEED_SKYSNAP_DEMO"] = "true", ["SEED_SKYSNAP_DEMO_PASSWORD"] = "initial-unit-test-password",
        }).Build();
        await SkySnapDemoSeeder.SeedAsync(db, configuration, CancellationToken.None);
        var original = (await db.Users.SingleAsync(x => x.Email == SkySnapDemoSeeder.DefaultOwnerEmail)).PasswordHash;
        configuration["SEED_SKYSNAP_DEMO_PASSWORD"] = "different-unit-test-password";
        await SkySnapDemoSeeder.SeedAsync(db, configuration, CancellationToken.None);
        Assert.Equal(original, (await db.Users.SingleAsync(x => x.Email == SkySnapDemoSeeder.DefaultOwnerEmail)).PasswordHash);
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BUILDTRACK_TEST_POSTGRES")))
            Skip = "Requires isolated PostgreSQL BUILDTRACK_TEST_POSTGRES; provided by production-images CI.";
    }
}
