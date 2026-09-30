using Microsoft.Extensions.Configuration;

namespace BuildTrack.Infrastructure.Security;

public static class ProductionConfiguration
{
    public static void Validate(IConfiguration configuration, bool development, bool api)
    {
        if (development) return;
        RequireSecret(configuration, "BUILDTRACK_SECRET_KEY", 32);
        if (api) RequireSecret(configuration, "JWT_SECRET", 32);
        if (string.IsNullOrWhiteSpace(configuration["POSTGRES_CONNECTION_STRING"]))
            throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required outside Development.");
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(configuration["POSTGRES_CONNECTION_STRING"]);
        if (string.IsNullOrWhiteSpace(connection.Password) || connection.Password == "buildtrack")
            throw new InvalidOperationException("A non-default PostgreSQL password is required.");
        foreach (var key in new[] { "SEED_ADMIN_PASSWORD", "SEED_SUPERVISOR_PASSWORD", "SEED_BAKINITY_DEMO_PASSWORD", "SEED_BAKINITY_DEMO_PRORAB_PASSWORD", "SEED_BAKINITY_DEMO_SUPPLY_PASSWORD", "SEED_SKYSNAP_DEMO_PASSWORD" })
            if (!string.IsNullOrEmpty(configuration[key])) RequireSecret(configuration, key, 16);
        foreach (var key in new[] { "SEED_BAKINITY_DEMO_RESET", "SEED_SKYSNAP_DEMO_RESET" })
            if (string.Equals(configuration[key], "true", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{key} is not allowed outside Development.");
        if (api && (configuration["CORS_ALLOWED_ORIGINS"] ?? "").Split(',').Any(x => x.Trim() == "*"))
            throw new InvalidOperationException("Wildcard production CORS is not allowed.");
    }

    private static void RequireSecret(IConfiguration configuration, string key, int minimumLength)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value.Length < minimumLength
            || value.Contains("change", StringComparison.OrdinalIgnoreCase)
            || value.Contains("dev-only", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Demo!2026", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{key} requires an explicitly configured non-default secret of at least {minimumLength} characters.");
    }
}
