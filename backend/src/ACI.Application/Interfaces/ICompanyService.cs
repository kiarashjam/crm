using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ICompanyService
{
    Task<IReadOnlyList<CompanyDto>> GetCompaniesAsync(Guid userId, CancellationToken ct = default);
    Task<CompanyDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<CompanyDto?> CreateAsync(Guid userId, CreateCompanyRequest request, CancellationToken ct = default);
    Task<CompanyDto?> UpdateAsync(Guid id, Guid userId, UpdateCompanyRequest request, CancellationToken ct = default);
}
