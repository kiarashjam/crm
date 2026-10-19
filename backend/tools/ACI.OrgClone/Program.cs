// Copies one organization into a new one, against the database in ORG_CLONE_CONNECTION.
//
//   dotnet run --project backend/tools/ACI.OrgClone -- --source "Pavillon46" --name "Pavillon 46 v2" [--commit]
//
// Without --commit it is a dry run: the whole copy is made and checked inside a
// transaction, then rolled back, so it proves the copy against the real data and keeps
// nothing. With --commit the same transaction is committed, but only if the check passes.
// Run from .github/workflows/org-clone.yml; see OrganizationCloner for what is copied.

using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("ORG_CLONE_CONNECTION");
string? Arg(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
var sourceName = Arg("--source");
var copyName = Arg("--name")?.Trim();
var commit = args.Contains("--commit");
if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(copyName))
{
    Console.Error.WriteLine("Usage: ORG_CLONE_CONNECTION=... ACI.OrgClone --source <organization> --name <new name> [--commit]");
    return 2;
}

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connection, sql => sql.CommandTimeout(300))
    .Options;
await using var db = new AppDbContext(options);

// Names match without spaces, as in the org admin workflow: "Pavillon 46" is "Pavillon46".
static string Squash(string s) => s.Replace(" ", "").ToLowerInvariant();
var organizations = await db.Organizations.AsNoTracking().Select(o => new { o.Id, o.Name }).ToListAsync();
var sources = organizations.Where(o => Squash(o.Name) == Squash(sourceName)).ToList();
if (sources.Count != 1)
{
    Console.Error.WriteLine($"ABORT: expected exactly one organization named \"{sourceName}\", found {sources.Count}. Present: {string.Join(", ", organizations.Select(o => o.Name).Order())}");
    return 1;
}
if (organizations.Any(o => Squash(o.Name) == Squash(copyName)))
{
    // Running it twice must not make two copies.
    Console.Error.WriteLine($"ABORT: an organization named \"{copyName}\" exists already. Nothing was copied.");
    return 1;
}

var source = sources[0];
Console.WriteLine($"{(commit ? "COPYING" : "DRY RUN")}: \"{source.Name}\" ({source.Id}) into a new organization \"{copyName}\".");
await using var transaction = await db.Database.BeginTransactionAsync();
var cloner = new OrganizationCloner(db);
var report = await cloner.CloneAsync(source.Id, copyName);

Console.WriteLine();
Console.WriteLine($"{"Table",-26}{"Copied",8}{"Left out",10}");
foreach (var (table, rows) in report.RowsCopied.OrderBy(r => r.Key))
    Console.WriteLine($"{table,-26}{rows,8}{(report.RowsLeftOut.GetValueOrDefault(table) is > 0 and var n ? n.ToString() : ""),10}");
foreach (var (table, rows) in report.RowsLeftOut.Where(r => !report.RowsCopied.ContainsKey(r.Key)))
    Console.WriteLine($"{table,-26}{0,8}{rows,10}");
Console.WriteLine($"{"Total",-26}{report.RowsCopied.Values.Sum(),8}");
Console.WriteLine();
foreach (var (what, rows) in report.Adjustments)
    Console.WriteLine($"  · {what}: {rows}");

var problems = await cloner.VerifyAsync(report);
if (problems.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("CHECK FAILED — rolled back, nothing was kept:");
    foreach (var problem in problems) Console.Error.WriteLine($"  ✗ {problem}");
    await transaction.RollbackAsync();
    return 1;
}
Console.WriteLine();
Console.WriteLine("CHECK PASSED: every table matches the original, and nothing in the copy points back into it.");

if (!commit)
{
    await transaction.RollbackAsync();
    Console.WriteLine("DRY RUN: rolled back. Nothing was kept. Run again with \"copy\" to keep it.");
    return 0;
}
await transaction.CommitAsync();
Console.WriteLine($"DONE: \"{copyName}\" is organization {report.CopyId}. Everyone in \"{source.Name}\" is a member, with the same role.");
return 0;
