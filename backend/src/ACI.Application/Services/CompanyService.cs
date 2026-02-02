using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _repository;

    public CompanyService(ICompanyRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<CompanyDto>> GetCompaniesAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await _repository.GetByUserIdAsync(userId, ct);
        return list.Select(c => new CompanyDto(c.Id, c.Name)).ToList();
    }

    public async Task<CompanyDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, ct);
        return entity == null ? null : new CompanyDto(entity.Id, entity.Name);
    }

    public async Task<CompanyDto?> CreateAsync(Guid userId, CreateCompanyRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return null;
        var entity = new Company
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
        };
        entity = await _repository.AddAsync(entity, ct);
        return new CompanyDto(entity.Id, entity.Name);
    }

    public async Task<CompanyDto?> UpdateAsync(Guid id, Guid userId, UpdateCompanyRequest request, CancellationToken ct = default)
    {
        var existing = await _repository.GetByIdAsync(id, userId, ct);
        if (existing == null) return null;
        if (request.Name != null) existing.Name = request.Name.Trim();
        existing = await _repository.UpdateAsync(existing, userId, ct);
        return existing == null ? null : new CompanyDto(existing.Id, existing.Name);
    }
}
