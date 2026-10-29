namespace ACI.WebApi.Tests;

/// <summary>
/// The GitHub workflows and scripts that reach production.
/// </summary>
/// <remarks>
/// They only run on GitHub or against Azure, so these read the files. They pin down
/// what was wrong before: both deploys went to production on a push with no test in
/// front of them, the frontend could go out before the backend it calls, and the
/// provisioning scripts made a plan that unloads the app, and its jobs, when idle.
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
