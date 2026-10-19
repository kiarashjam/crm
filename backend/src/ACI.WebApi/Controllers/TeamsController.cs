using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACI.WebApi.Controllers;

/// <summary>Sales teams in the workspace. Everyone can see them; the owner or a manager changes them.</summary>
[ApiController]
[Route("api/teams")]
[Authorize]
[WorkspaceRequired]
public class TeamsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;

    public TeamsController(AppDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (_user.UserId == null || _user.CurrentOrganizationId is not { } orgId) return Unauthorized();
        var teams = await _db.SalesTeams.AsNoTracking().Where(t => t.OrganizationId == orgId).OrderBy(t => t.Name).ToListAsync(ct);
        var members = await _db.SalesTeamMembers.AsNoTracking()
            .Where(m => teams.Select(t => t.Id).Contains(m.TeamId))
            .ToListAsync(ct);
        return Ok(teams.Select(t => new
        {
            t.Id,
            t.Name,
            memberUserIds = members.Where(m => m.TeamId == t.Id).Select(m => m.UserId).ToList(),
        }));
    }

    [HttpPost]
    [ManagersOnly("change teams")]
    public async Task<IActionResult> Create([FromBody] TeamWrite body, CancellationToken ct)
    {
        if (_user.UserId == null || _user.CurrentOrganizationId is not { } orgId) return Unauthorized();
        var name = (body.Name ?? "").Trim();
        if (name.Length == 0) return Problem(detail: "A team needs a name.", statusCode: StatusCodes.Status400BadRequest);
        if (name.Length > 120) return Problem(detail: "A team name can be at most 120 characters.", statusCode: StatusCodes.Status400BadRequest);
        var team = new SalesTeam { Id = Guid.NewGuid(), OrganizationId = orgId, Name = name, CreatedAtUtc = DateTime.UtcNow };
        _db.SalesTeams.Add(team);
        await _db.SaveChangesAsync(ct);
        return Ok(new { team.Id, team.Name, memberUserIds = Array.Empty<Guid>() });
    }

    [HttpPost("{id:guid}/members")]
    [ManagersOnly("change teams")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] TeamMemberWrite body, CancellationToken ct)
    {
        if (_user.UserId == null || _user.CurrentOrganizationId is not { } orgId) return Unauthorized();
        var team = await _db.SalesTeams.FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == orgId, ct);
        if (team == null) return NotFound();
        // Only people in this workspace. Any user id used to be accepted.
        if (!await _db.OrganizationMembers.AnyAsync(m => m.OrganizationId == orgId && m.UserId == body.UserId, ct))
            return Problem(detail: "Add someone who is in this workspace.", statusCode: StatusCodes.Status400BadRequest);
        var exists = await _db.SalesTeamMembers.AnyAsync(m => m.TeamId == id && m.UserId == body.UserId, ct);
        if (!exists)
        {
            _db.SalesTeamMembers.Add(new SalesTeamMember { TeamId = id, UserId = body.UserId });
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ManagersOnly("change teams")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        if (_user.CurrentOrganizationId is not { } orgId) return Unauthorized();
        var team = await _db.SalesTeams.AnyAsync(t => t.Id == id && t.OrganizationId == orgId, ct);
        if (!team) return NotFound();
        var row = await _db.SalesTeamMembers.FirstOrDefaultAsync(m => m.TeamId == id && m.UserId == userId, ct);
        if (row != null)
        {
            _db.SalesTeamMembers.Remove(row);
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    public sealed class TeamWrite { public string? Name { get; set; } }
    public sealed class TeamMemberWrite { public Guid UserId { get; set; } }
}
