using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;

namespace ACI.Application.Services;

public class TemplateService : ITemplateService
{
    private readonly ITemplateRepository _repository;

    public TemplateService(ITemplateRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<TemplateDto>> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        var system = await _repository.GetAllAsync(ct);
        var user = await _repository.GetByUserIdAsync(userId, ct);
        var combined = system.Concat(user).DistinctBy(t => t.Id).OrderBy(t => t.Title).ToList();
        return combined.Select(Map).ToList();
    }

    public async Task<TemplateDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        return entity == null ? null : Map(entity);
    }

    private static string CopyTypeIdToString(CopyTypeId id) => id switch
    {
        CopyTypeId.SalesEmail => "sales-email",
        CopyTypeId.FollowUp => "follow-up",
        CopyTypeId.CrmNote => "crm-note",
        CopyTypeId.DealMessage => "deal-message",
        CopyTypeId.WorkflowMessage => "workflow-message",
        _ => "sales-email",
    };

    private static TemplateDto Map(Template e) =>
        new TemplateDto(e.Id, e.Title, e.Description, e.Category, CopyTypeIdToString(e.CopyTypeId), e.Goal, e.UseCount);
}
