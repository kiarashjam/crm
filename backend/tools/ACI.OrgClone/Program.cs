// Copies one organization into a new one, against the database in ORG_CLONE_CONNECTION.
//
//   dotnet run --project backend/tools/ACI.OrgClone -- --source "Pavillon46" --name "Pavillon 46 v2" [--commit] [--report <file>]
//
// Without --commit it is a dry run, and writes nothing: it counts what the copy would
// hold, table by table, estimates its size and checks the database has room for it.
// With --commit it checks the room first, then makes the copy inside a transaction,
// checks it, and commits only if the check passes.
//
// The dry run used to make the whole copy inside a transaction and roll it back. That
// wrote every row of the organization, attachment bytes included, into the production
// database every tenant shares, with no look at whether it had room, and held its locks
// until the rollback: a large organization could fill the database and fail everyone's
// writes. Counting reads ids only.
//
// The table-by-table report goes to --report when given, and to the output otherwise;
// the output itself names no organization. The workflow keeps the report out of its
// public log. Run from .github/workflows/org-clone.yml; see OrganizationCloner for what
// is copied.

using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var connection = Environment.GetEnvironmentVariable("ORG_CLONE_CONNECTION");
string? Arg(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
var sourceName = Arg("--source");
var copyName = Arg("--name")?.Trim();
var commit = args.Contains("--commit");
var reportPath = Arg("--report");
if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(copyName))
{
    Console.Error.WriteLine("Usage: ORG_CLONE_CONNECTION=... ACI.OrgClone --source <organization> --name <new name> [--commit] [--report <file>]");
    return 2;
}

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connection, sql => sql.CommandTimeout(300))
    .Options;
await using var db = new AppDbContext(options);
await using var report = reportPath is null ? null : File.CreateText(reportPath);
var detail = report ?? Console.Out;

// Names match without spaces, as in the org admin workflow: "Pavillon 46" is "Pavillon46".
static string Squash(string s) => s.Replace(" ", "").ToLowerInvariant();
var organizations = await db.Organizations.AsNoTracking().Select(o => new { o.Id, o.Name }).ToListAsync();
var sources = organizations.Where(o => Squash(o.Name) == Squash(sourceName)).ToList();
if (sources.Count != 1)
{
    // Not the names of the organizations there are: the output can end up in a public log.
    Console.Error.WriteLine($"ABORT: expected exactly one organization with the name given, found {sources.Count}. Nothing was copied.");
    return 1;
}
if (organizations.Any(o => Squash(o.Name) == Squash(copyName)))
{
    // Running it twice must not make two copies.
    Console.Error.WriteLine("ABORT: an organization with the new name exists already. Nothing was copied.");
    return 1;
}

var source = sources[0];
var cloner = new OrganizationCloner(db);
Console.WriteLine($"{(commit ? "COPYING" : "DRY RUN")}: organization {source.Id} into a new organization.");

// What the copy would hold, and whether the database has room for it, before anything is written.
var plan = await cloner.PlanAsync(source.Id);
var needed = await cloner.EstimateBytesAsync(plan);
var free = await cloner.FreeBytesAsync();
detail.WriteLine();
detail.WriteLine($"{"Table",-26}{"To copy",8}{"Left out",10}");
foreach (var (table, rows) in plan.RowsToCopy.OrderBy(r => r.Key))
    detail.WriteLine($"{table,-26}{rows,8}{(plan.RowsLeftOut.GetValueOrDefault(table) is > 0 and var n ? n.ToString() : ""),10}");
foreach (var (table, rows) in plan.RowsLeftOut.Where(r => !plan.RowsToCopy.ContainsKey(r.Key)))
    detail.WriteLine($"{table,-26}{0,8}{rows,10}");
detail.WriteLine($"{"Total",-26}{plan.RowsToCopy.Values.Sum(),8}");
detail.WriteLine($"Attachments: {Megabytes(plan.AttachmentBytes)}.");
detail.WriteLine();

Console.WriteLine($"SIZE: the copy needs about {Megabytes(needed)}; the database has {Megabytes(free)} free.");
if (free < needed * OrganizationCloner.Headroom)
{
    Console.Error.WriteLine($"ABORT: not enough room. A copy is only started with {OrganizationCloner.Headroom} times its size free, "
        + "because running out partway fails every organization's writes until it is rolled back. Nothing was copied.");
    return 1;
}
if (!commit)
{
    Console.WriteLine("DRY RUN: it fits. Nothing was written. Run again with \"copy\" to make the copy; it is checked before it is kept.");
    return 0;
}

await using var transaction = await db.Database.BeginTransactionAsync();
var copied = await cloner.CloneAsync(source.Id, copyName);

detail.WriteLine($"{"Table",-26}{"Copied",8}{"Left out",10}");
foreach (var (table, rows) in copied.RowsCopied.OrderBy(r => r.Key))
    detail.WriteLine($"{table,-26}{rows,8}{(copied.RowsLeftOut.GetValueOrDefault(table) is > 0 and var n ? n.ToString() : ""),10}");
foreach (var (table, rows) in copied.RowsLeftOut.Where(r => !copied.RowsCopied.ContainsKey(r.Key)))
    detail.WriteLine($"{table,-26}{0,8}{rows,10}");
detail.WriteLine($"{"Total",-26}{copied.RowsCopied.Values.Sum(),8}");
detail.WriteLine();
foreach (var (what, rows) in copied.Adjustments)
    detail.WriteLine($"  · {what}: {rows}");

var problems = await cloner.VerifyAsync(copied);
if (problems.Count > 0)
{
    detail.WriteLine();
    foreach (var problem in problems) detail.WriteLine($"  ✗ {problem}");
    Console.Error.WriteLine($"CHECK FAILED — rolled back, nothing was kept: {problems.Count} problem(s), listed in the report.");
    await transaction.RollbackAsync();
    return 1;
}
Console.WriteLine("CHECK PASSED: every table matches the original, and nothing in the copy points back into it.");

await transaction.CommitAsync();
Console.WriteLine($"DONE: the copy is organization {copied.CopyId}. Everyone in the original is a member, with the same role.");
return 0;

static string Megabytes(long bytes) => $"{bytes / (1024.0 * 1024.0):0.0} MB";
