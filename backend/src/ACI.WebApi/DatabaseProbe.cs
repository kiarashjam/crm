using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ACI.WebApi;

/// <summary>
/// Whether the database in the app's connection string exists, asked once at start-up.
/// </summary>
/// <remarks>
/// <para>
/// EF's <c>ExistsAsync</c> opens a connection to the database itself. For a missing one
/// that login fails with error 4060, which SqlClient treats as transient: it waits its
/// ConnectRetryInterval (10 s) and logs in again, with only what is left of the connect
/// timeout. On a busy server that second attempt could run out of time before reaching
/// the login, and the "could not open a connection to SQL Server" it then threw is not
/// an error EF reads as "does not exist". So a first start in development failed instead
/// of creating the database (FreshDatabaseStartupTests failed on CI that way, which held
/// back a deploy), and it always waited the 10 s.
/// </para>
/// <para>
/// The probe opens a connection of its own without that retry, and without pooling, so
/// a missing database answers at once. Any other failure (a server still starting, an
/// Azure database waking from a pause, a sign-in it cannot make) is left to EF's own
/// check on the app's connection, with SqlClient's retries, as before.
/// </para>
/// </remarks>
public static class DatabaseProbe
{
    public static async Task<bool> ExistsAsync(DatabaseFacade database, CancellationToken ct = default)
    {
        var creator = database.GetService<IRelationalDatabaseCreator>();
        if (!database.IsSqlServer() || database.GetConnectionString() is not { Length: > 0 } connectionString)
            return await creator.ExistsAsync(ct);

        var probe = new SqlConnectionStringBuilder(connectionString) { ConnectRetryCount = 0, Pooling = false };
        try
        {
            await using var connection = new SqlConnection(probe.ConnectionString);
            await connection.OpenAsync(ct);
            return true;
        }
        catch (SqlException e) when (IsDoesNotExist(e))
        {
            return false;
        }
        catch (SqlException)
        {
            return await creator.ExistsAsync(ct);
        }
    }

    /// <summary>The errors EF reads as a missing database: cannot open it (4060), or its files (1832, 5120).</summary>
    public static bool IsDoesNotExist(SqlException e) =>
        e.Errors.Cast<SqlError>().Any(error => error.Number is 4060 or 1832 or 5120);
}
