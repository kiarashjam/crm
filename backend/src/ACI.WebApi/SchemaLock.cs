using System.Data;
using Microsoft.Data.SqlClient;

namespace ACI.WebApi;

/// <summary>
/// Lets one instance at a time through the start-up schema work.
/// </summary>
/// <remarks>
/// <para>
/// EF 8's MigrateAsync takes no lock of its own. Two instances starting together (a
/// scale-out, or a slot swap that starts the new one while the old one restarts) both
/// found the same migration pending and both ran it, and the second failed part-way
/// through; the start-up DDL and the baseline record had the same race. Holding an
/// application lock makes the second wait for the first and then find nothing left to
/// do.
/// </para>
/// <para>
/// The lock belongs to a connection of its own, held open until <see cref="DisposeAsync"/>,
/// so the schema work's own commands run exactly as they did without it. If the instance
/// dies, SQL Server ends that session and the lock goes with it.
/// </para>
/// </remarks>
public sealed class SchemaLock : IAsyncDisposable
{
    public const string Resource = "Cadence start-up schema";

    private readonly SqlConnection _connection;

    private SchemaLock(SqlConnection connection) => _connection = connection;

    /// <summary>Waits up to <paramref name="wait"/> for the lock.</summary>
    /// <exception cref="TimeoutException">Another instance held it for longer.</exception>
    public static async Task<SchemaLock> AcquireAsync(string connectionString, TimeSpan wait, CancellationToken ct = default)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "sp_getapplock";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = (int)wait.TotalSeconds + 30;
            command.Parameters.AddWithValue("@Resource", Resource);
            command.Parameters.AddWithValue("@LockMode", "Exclusive");
            command.Parameters.AddWithValue("@LockOwner", "Session");
            command.Parameters.AddWithValue("@LockTimeout", (int)wait.TotalMilliseconds);
            var result = command.Parameters.Add("@Result", SqlDbType.Int);
            result.Direction = ParameterDirection.ReturnValue;
            await command.ExecuteNonQueryAsync(ct);

            // 0: granted at once; 1: granted after waiting; below 0: not granted.
            var code = (int)result.Value;
            if (code == -1)
                throw new TimeoutException(
                    $"Another instance held the start-up schema lock for more than {wait.TotalMinutes:0} minutes.");
            if (code < 0)
                throw new InvalidOperationException($"SQL Server refused the start-up schema lock (sp_getapplock returned {code}).");
            return new SchemaLock(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "sp_releaseapplock";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Resource", Resource);
            command.Parameters.AddWithValue("@LockOwner", "Session");
            await command.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            // The connection is gone, and the lock with its session.
        }
        catch (InvalidOperationException)
        {
            // Likewise: the connection was closed under us.
        }
        await _connection.DisposeAsync();
    }
}
