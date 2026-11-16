using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.Assistant;

/// <inheritdoc cref="IAssistantSettingsService"/>
/// <remarks>
/// A workspace that never saved its settings has none stored: it uses the server's default
/// limit and has morning briefs on. The first change makes its row. Who may change them is the
/// endpoint's to say (settings.manage); anyone in the workspace may read them, as the usage is
/// theirs too.
/// </remarks>
public sealed class AssistantSettingsService : IAssistantSettingsService
{
    private readonly AppDbContext _db;
    private readonly AssistantOptions _options;
    private readonly IAssistantModel _model;
    private readonly IAiBudget _budget;

    public AssistantSettingsService(AppDbContext db, IOptions<AssistantOptions> options, IAssistantModel model, IAiBudget budget)
    {
        _db = db;
        _options = options.Value;
        _model = model;
        _budget = budget;
    }

    public async Task<Result<AssistantSettingsDto>> GetAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var row = await _db.AssistantSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        return await DtoAsync(organizationId, userId, row, ct);
    }

    public async Task<Result<AssistantSettingsDto>> UpdateAsync(Guid organizationId, Guid userId, UpdateAssistantSettingsRequest request, Guid? expectedVersion, CancellationToken ct = default)
    {
        var ceiling = Math.Max(0, _options.MaxMonthlyLimit);
        if (request.UseDefaultLimit != true && request.MonthlyLimit is int asked && (asked < 0 || asked > ceiling))
            return DomainErrors.Assistant.LimitInvalid(ceiling);

        var row = await _db.AssistantSettings.FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        if (row != null && expectedVersion is Guid version && row.Version != version)
            return DomainErrors.Assistant.SettingsChanged;

        var now = DateTime.UtcNow;
        if (row == null)
        {
            row = new AssistantSettings { Id = Guid.NewGuid(), OrganizationId = organizationId, CreatedAtUtc = now };
            _db.AssistantSettings.Add(row);
        }
        if (request.UseDefaultLimit == true) row.MonthlyLimit = null;
        else if (request.MonthlyLimit is int limit) row.MonthlyLimit = limit;
        if (request.MorningBriefs is bool briefs) row.MorningBriefs = briefs;
        row.UpdatedAtUtc = now;
        row.UpdatedByUserId = userId;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return DomainErrors.Assistant.SettingsChanged;
        }
        return await DtoAsync(organizationId, userId, row, ct);
    }

    public async Task<bool> MorningBriefsOnAsync(Guid organizationId, CancellationToken ct = default) =>
        // Read past the filters by its organization: the morning brief job acts for no workspace.
        await _db.AssistantSettings.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.OrganizationId == organizationId).Select(s => (bool?)s.MorningBriefs).FirstOrDefaultAsync(ct) ?? true;

    private async Task<Result<AssistantSettingsDto>> DtoAsync(Guid organizationId, Guid userId, AssistantSettings? row, CancellationToken ct)
    {
        var usage = await _budget.AllowanceAsync(organizationId, userId, ct);
        return new AssistantSettingsDto(
            _model.IsConfigured,
            _model.Provider,
            _model.IsConfigured ? _model.Model : "",
            AiBudget.EffectiveLimit(row?.MonthlyLimit, _options),
            row?.MonthlyLimit,
            Math.Clamp(_options.DefaultMonthlyLimit, 0, Math.Max(0, _options.MaxMonthlyLimit)),
            Math.Max(0, _options.MaxMonthlyLimit),
            row?.MorningBriefs ?? true,
            usage,
            row?.Version);
    }
}
