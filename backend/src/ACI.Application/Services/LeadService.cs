using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public class LeadService : ILeadService
{
    private readonly ILeadRepository _repository;
    private readonly IContactRepository _contactRepository;
    private readonly IDealRepository _dealRepository;

    public LeadService(ILeadRepository repository, IContactRepository contactRepository, IDealRepository dealRepository)
    {
        _repository = repository;
        _contactRepository = contactRepository;
        _dealRepository = dealRepository;
    }

    public async Task<IReadOnlyList<LeadDto>> GetLeadsAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await _repository.GetByUserIdAsync(userId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<LeadDto>> SearchAsync(Guid userId, string query, CancellationToken ct = default)
    {
        var list = await _repository.SearchAsync(userId, query.Trim(), ct);
        return list.Select(Map).ToList();
    }

    public async Task<LeadDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, ct);
        return entity == null ? null : Map(entity);
    }

    public async Task<LeadDto?> CreateAsync(Guid userId, CreateLeadRequest request, CancellationToken ct = default)
    {
        var entity = new Lead
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            CompanyId = request.CompanyId,
            Source = request.Source,
            Status = request.Status ?? "New",
            CreatedAtUtc = DateTime.UtcNow,
        };
        entity = await _repository.AddAsync(entity, ct);
        return Map(entity);
    }

    public async Task<LeadDto?> UpdateAsync(Guid id, Guid userId, UpdateLeadRequest request, CancellationToken ct = default)
    {
        var existing = await _repository.GetByIdAsync(id, userId, ct);
        if (existing == null) return null;
        if (request.Name != null) existing.Name = request.Name;
        if (request.Email != null) existing.Email = request.Email;
        if (request.Phone != null) existing.Phone = request.Phone;
        if (request.CompanyId != null) existing.CompanyId = request.CompanyId;
        if (request.Source != null) existing.Source = request.Source;
        if (request.Status != null) existing.Status = request.Status;
        existing = await _repository.UpdateAsync(existing, userId, ct);
        return existing == null ? null : Map(existing);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return await _repository.DeleteAsync(id, userId, ct);
    }

    public async Task<ConvertLeadResult?> ConvertAsync(Guid leadId, Guid userId, ConvertLeadRequest request, CancellationToken ct = default)
    {
        var lead = await _repository.GetByIdAsync(leadId, userId, ct);
        if (lead == null) return null;
        if (!request.CreateContact && !request.CreateDeal) return new ConvertLeadResult(null, null);

        Guid? contactId = null;
        if (request.CreateContact)
        {
            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = lead.Name,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyId = lead.CompanyId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _contactRepository.AddAsync(contact, ct);
            contactId = contact.Id;
        }

        Guid? dealId = null;
        if (request.CreateDeal)
        {
            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = string.IsNullOrWhiteSpace(request.DealName) ? lead.Name : request.DealName.Trim(),
                Value = string.IsNullOrWhiteSpace(request.DealValue) ? "0" : request.DealValue.Trim(),
                Stage = request.DealStage?.Trim(),
                CompanyId = lead.CompanyId,
                ContactId = contactId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _dealRepository.AddAsync(deal, ct);
            dealId = deal.Id;
        }

        return new ConvertLeadResult(contactId, dealId);
    }

    private static LeadDto Map(Lead e) =>
        new LeadDto(e.Id, e.Name, e.Email, e.Phone, e.CompanyId, e.Source, e.Status);
}
