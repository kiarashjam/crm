using System.Text;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.SqlClient;

namespace ACI.WebApi.Tests;

/// <summary>
/// The GitHub workflows and scripts that reach production.
/// </summary>
/// <remarks>
/// They only run on GitHub or against Azure, so these read the files. They pin down
/// what was wrong before: both deploys went to production on a push with no test in
/// front of them, the frontend could go out before the backend it calls, and the
/// provisioning scripts made a plan that unloads the app, and its jobs, when idle. The
/// admin workflows, in a public repository, printed customer names, emails and per-lead
/// records into public logs, had a real person's email as a default, ran with no
/// approval, and could demote an organization's owner for good.
/// </remarks>
public class ProductionWorkflowTests
{
    [Fact]
    public void TheBackendDeployHasNoTriggerOfItsOwn()
    {
        // Any trigger here (a push, a release, a manual run) would deploy the backend
        // without the checks; it is only ever called, after them, by deploy.yml.
        Triggers(Workflow("deploy-backend.yml")).Should().Equal("workflow_call");
    }

    [Fact]
    public void TheDeployChecksFirstThenTheBackendThenTheFrontend()
    {
        var deploy = Workflow("deploy.yml");
        Triggers(deploy).Should().Contain("push");

        var jobs = Jobs(deploy);
        jobs.Keys.Should().BeEquivalentTo("checks", "backend", "build_and_deploy");
        jobs["checks"].Should().Contain("uses: ./.github/workflows/ci.yml");
        jobs["backend"].Should().Contain("needs: checks").And.Contain("uses: ./.github/workflows/deploy-backend.yml");
        jobs["build_and_deploy"].Should().Contain("needs: backend").And.Contain("Azure/static-web-apps-deploy");
        TopLevel(deploy, "concurrency").Should().Contain("cancel-in-progress: false", "a deploy is never cut off halfway");
    }

    [Fact]
    public void TheChecksRunOnPullRequestsAndPushes_AndCoverBothHalves()
    {
        var ci = Workflow("ci.yml");
        Triggers(ci).Should().Contain(new[] { "pull_request", "push", "workflow_call" });
        foreach (var command in new[] { "dotnet build ACI.sln", "dotnet test ACI.sln", "npx tsc --noEmit", "npm run lint", "npx vitest run", "npm run build" })
            ci.Should().Contain(command);
        ci.Should().Contain("CADENCE_TEST_SQLSERVER", "the start-up schema tests need a real SQL Server, or they are skipped");
    }

    [Fact]
    public void TheProvisioningScriptsMakeAPlanThatKeepsTheAppLoaded()
    {
        // The sequence, reminder and SLA jobs run inside the web app. A Free (F1) or
        // Shared plan has no Always On and unloads the app when nobody uses it, so the
        // jobs stopped overnight; and a plan above it still needs Always On turned on.
        var scripts = Directory.GetFiles(Path.Combine(RepoRoot(), "scripts"), "*.ps1")
            .Where(f => File.ReadAllText(f).Contains("az appservice plan create"))
            .ToList();
        scripts.Should().NotBeEmpty();
        foreach (var path in scripts)
        {
            var script = File.ReadAllText(path);
            var name = Path.GetFileName(path);
            script.Should().NotMatchRegex(@"--sku\s+(F1|D1|FREE|SHARED)\b", name);
            script.Should().Contain("--sku $AppServicePlanSku", name);
            script.Should().Contain("[string]$AppServicePlanSku = \"B1\"", name);
            script.Should().NotMatchRegex(@"ValidateSet\([^)]*""(F1|D1)""", name);
            script.Should().Contain("--always-on true", name);
        }
    }

    private static readonly string[] AdminWorkflows = ["org-admin-member.yml", "org-clone.yml", "lead-status-audit.yml"];

    [Fact]
    public void TheAdminWorkflowsRunInTheProductionEnvironment()
    {
        // So a required reviewer can be added in Settings -> Environments, and every run
        // against production waits for one.
        foreach (var file in AdminWorkflows)
            foreach (var (job, lines) in Jobs(Workflow(file)))
                lines.Should().Contain("environment: production", $"{file}: {job}");
    }

    [Fact]
    public void TheAdminWorkflowsNameNobodyByDefault_AndKeepTheirInputsOutOfTheLog()
    {
        foreach (var file in AdminWorkflows)
        {
            var yaml = Workflow(file);
            // A person or a customer as a default is one click from acting on them.
            var inputs = Inputs(yaml);
            foreach (var name in new[] { "email", "organization", "source", "name" }.Where(inputs.ContainsKey))
                inputs[name].Should().NotContain("default:", $"{file}: {name}");
            yaml.Should().NotMatchRegex(@"default:\s*'[^']*@", file);

            // An input in a step's env or script is printed in the log with it, so they are
            // read from the event and masked instead.
            yaml.Should().NotContain("${{ inputs.", file).And.NotContain("github.event.inputs", file);
            yaml.Should().Contain("::add-mask::", file);

            // A mistyped name printed every customer organization.
            yaml.Should().NotMatchRegex(@"SELECT\s+Name\s+FROM\s+Organizations", file);
        }
    }

    [Fact]
    public void TheMemberTaskPrintsNoMembers()
    {
        // It printed every member's role and join date and the start of their email.
        // Every SELECT in it now sits inside a condition or an assignment, so it returns no rows.
        File.ReadAllLines(MemberTaskPath()).Where(l => l.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            .Should().BeEmpty();
        Workflow("org-admin-member.yml").Should().Contain("cat .github/workflows/sql/org-admin-member.sql");
    }

    [Fact]
    public void TheAuditAndTheCopyReportAreEncrypted_NotPrinted()
    {
        foreach (var file in new[] { "org-clone.yml", "lead-status-audit.yml" })
        {
            var yaml = Workflow(file);
            yaml.Should().Contain("openssl enc -aes-256-cbc -pbkdf2", file)
                .And.Contain("${{ secrets.ADMIN_REPORT_PASSPHRASE }}", file)
                .And.Contain("actions/upload-artifact", file);
        }
        Workflow("lead-status-audit.yml").Should().Contain("> \"$RUNNER_TEMP/audit.txt\"");
        Workflow("org-clone.yml").Should().Contain("--report \"$RUNNER_TEMP/clone-report.txt\"");
    }

    [SqlServerFact]
    public async Task TheMemberTaskNeverChangesTheOwnersRole()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = SqlServerApp.NewDatabaseName();
        try
        {
            var owner = $"{Guid.NewGuid():N}@example.com";
            var org = await OrganizationAsync(database, owner, "Pavillon 46");

            var run = await RunMemberTaskAsync(database, owner, "Pavillon46", "Viewer");

            run.Error.Should().BeOfType<SqlException>("the task refuses").Which.Number.Should().Be(50001);
            run.Messages.Should().Contain("ABORT: this person is the owner");
            (await SqlServerApp.ScalarAsync(database, $"SELECT [Role] FROM [OrganizationMembers] WHERE [OrganizationId] = '{org}'"))
                .Should().Be("Owner", "the owner is still the owner, not a read-only viewer");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task TheMemberTaskSetsAMembersRole_AndNamesNoOrganizationWhenTheNameIsWrong()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = SqlServerApp.NewDatabaseName();
        try
        {
            var owner = $"{Guid.NewGuid():N}@example.com";
            var org = await OrganizationAsync(database, owner, "Pavillon 46");
            var colleague = $"o'{Guid.NewGuid():N}@example.com";
            using (var app = SqlServerApp.Start(database))
                await Workspaces.SignInAsync(app, colleague);

            var run = await RunMemberTaskAsync(database, colleague, "Pavillon46", "Manager");
            run.Error.Should().BeNull(run.Messages);
            run.Messages.Should().Contain("RESULT: added to the organization with role Manager.").And.Contain("2 member(s)");
            run.Messages.Should().NotContain(colleague).And.NotContain("Pavillon");
            (await SqlServerApp.ScalarAsync(database,
                $"SELECT m.[Role] FROM [OrganizationMembers] m JOIN [Users] u ON u.[Id] = m.[UserId] WHERE m.[OrganizationId] = '{org}' AND u.[Email] = N'{colleague.Replace("'", "''")}'"))
                .Should().Be("Manager");

            var wrong = await RunMemberTaskAsync(database, colleague, "Pavilion46", "Viewer");
            wrong.Error.Should().BeOfType<SqlException>().Which.Number.Should().Be(50000);
            wrong.Messages.Should().Contain("found 0").And.NotContain("Pavillon", "the log is public");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static string MemberTaskPath() => Path.Combine(RepoRoot(), ".github", "workflows", "sql", "org-admin-member.sql");

    /// <summary>An organization made through the API by a new account, so it is set up as the app sets one up.</summary>
    private static async Task<Guid> OrganizationAsync(string database, string ownerEmail, string name)
    {
        using var app = SqlServerApp.Start(database);
        var (owner, _) = await Workspaces.SignInAsync(app, ownerEmail);
        var created = await owner.PostAsync("/api/organizations", Workspaces.Body(new { name }));
        using var doc = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Runs sql/org-admin-member.sql the way the workflow does: the inputs declared as T-SQL literals above it.</summary>
    private static async Task<(string Messages, Exception? Error)> RunMemberTaskAsync(string database, string email, string organization, string role)
    {
        static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
        var sql = $"DECLARE @Email nvarchar(256) = {Literal(email)};\n"
            + $"DECLARE @OrgName nvarchar(256) = {Literal(organization)};\n"
            + $"DECLARE @Role nvarchar(32) = {Literal(role)};\n"
            + await File.ReadAllTextAsync(MemberTaskPath());

        var messages = new StringBuilder();
        await using var connection = new SqlConnection(SqlServerApp.ConnectionString(database));
        connection.InfoMessage += (_, e) => messages.AppendLine(e.Message);
        await connection.OpenAsync();
        try
        {
            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
            return (messages.ToString(), null);
        }
        catch (SqlException ex)
        {
            // What the batch printed before failing comes with the error, not as an info message.
            foreach (SqlError error in ex.Errors) messages.AppendLine(error.Message);
            return (messages.ToString(), ex);
        }
    }

    /// <summary>Each input under <c>on: workflow_dispatch: inputs:</c>, by name, with its lines.</summary>
    internal static Dictionary<string, string> Inputs(string yaml)
    {
        var inputs = new Dictionary<string, string>();
        string? current = null;
        foreach (var line in TopLevel(yaml, "on").Split('\n'))
        {
            if (line.Length > 6 && line.StartsWith("      ") && line[6] != ' ')
            {
                current = line.Trim().TrimEnd(':');
                inputs[current] = "";
            }
            else if (current != null && line.StartsWith("        "))
            {
                inputs[current] += line.Trim() + "\n";
            }
            else
            {
                current = null;
            }
        }
        return inputs;
    }

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException(".github/workflows not found above " + AppContext.BaseDirectory);
    }

    internal static string Workflow(string file) =>
        File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", file)).Replace("\r\n", "\n");

    /// <summary>The lines of a top-level key's block, comments left out.</summary>
    internal static string TopLevel(string yaml, string key)
    {
        var lines = yaml.Split('\n');
        var start = Array.FindIndex(lines, l => l == key + ":" || l.StartsWith(key + ": "));
        if (start < 0) return "";
        var block = lines.Skip(start + 1).TakeWhile(l => l.Length == 0 || l[0] == ' ' || l[0] == '#');
        return string.Join("\n", block.Where(l => !l.TrimStart().StartsWith('#')));
    }

    /// <summary>The events under <c>on:</c>.</summary>
    internal static string[] Triggers(string yaml) => Keys(TopLevel(yaml, "on"));

    /// <summary>Each job under <c>jobs:</c>, by name, with its lines.</summary>
    internal static Dictionary<string, string> Jobs(string yaml)
    {
        var jobs = new Dictionary<string, string>();
        string? current = null;
        foreach (var line in TopLevel(yaml, "jobs").Split('\n'))
        {
            if (line.Length > 2 && line.StartsWith("  ") && line[2] != ' ')
            {
                current = line.Trim().TrimEnd(':');
                jobs[current] = "";
            }
            else if (current != null)
            {
                jobs[current] += line.Trim() + "\n";
            }
        }
        return jobs;
    }

    private static string[] Keys(string block) =>
        block.Split('\n')
            .Where(l => l.Length > 2 && l.StartsWith("  ") && l[2] != ' ')
            .Select(l => l.Trim().Split(':')[0])
            .ToArray();
}
