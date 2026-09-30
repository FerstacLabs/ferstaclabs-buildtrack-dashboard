using Npgsql;

namespace BuildTrack.Infrastructure.Data;

// Dedicated non-pooled session: disposing it releases the lock even after failed initialization.
public sealed class DatabaseInitializationLock : IAsyncDisposable
{
    private readonly NpgsqlConnection connection;
    private DatabaseInitializationLock(NpgsqlConnection connection) => this.connection = connection;

    public static async Task<DatabaseInitializationLock> AcquireAsync(string connectionString, CancellationToken ct)
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
        var connection = new NpgsqlConnection(settings.ConnectionString);
        try
        {
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(738211904812);", connection) { CommandTimeout = 300 };
            await command.ExecuteNonQueryAsync(ct);
            return new DatabaseInitializationLock(connection);
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
