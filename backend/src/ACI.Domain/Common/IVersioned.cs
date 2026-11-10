namespace ACI.Domain.Common;

/// <summary>
/// A record whose every save gives it a new <see cref="Version"/>, and whose save only
/// lands while the row still holds the version that request read.
/// </summary>
/// <remarks>
/// AppDbContext writes the new value on every save, whoever saves: the API, a job, a
/// merge, the cloner. The column is a concurrency token, so a save made from an older
/// read of the row is refused instead of written over the newer one. A Guid rather than
/// a SQL Server rowversion, because the in-memory provider the HTTP tests run on never
/// fills a rowversion in, so the refusal could not be shown there.
/// </remarks>
public interface IVersioned
{
    Guid Version { get; set; }
}
