using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class CompanyRepository : ICompanyRepository
{
    private readonly AppDbContext _db;

    public CompanyRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Company>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.Companies.Where(c => c.UserId == userId).OrderBy(c => c.Name).ToListAsync(ct);

    public async Task<Company?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        await _db.Companies.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

    public async Task<Company> AddAsync(Company company, CancellationToken ct = default)
    {
        _db.Companies.Add(company);
        await _db.SaveChangesAsync(ct);
        return company;
    }

    public async Task<Company?> UpdateAsync(Company company, Guid userId, CancellationToken ct = default)
    {
        var existing = await _db.Companies.FirstOrDefaultAsync(c => c.Id == company.Id && c.UserId == userId, ct);
        if (existing == null) return null;
        existing.Name = company.Name;
        await _db.SaveChangesAsync(ct);
        return existing;
    }
}
