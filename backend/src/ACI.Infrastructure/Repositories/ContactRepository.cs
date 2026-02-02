using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class ContactRepository : IContactRepository
{
    private readonly AppDbContext _db;

    public ContactRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Contact>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.Contacts.Where(c => c.UserId == userId).OrderBy(c => c.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Contact>> SearchAsync(Guid userId, string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetByUserIdAsync(userId, ct);
        var q = query.Trim().ToLowerInvariant();
        return await _db.Contacts
            .Where(c => c.UserId == userId && (c.Name.ToLower().Contains(q) || c.Email.ToLower().Contains(q)))
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<Contact?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        await _db.Contacts.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

    public async Task<Contact> AddAsync(Contact contact, CancellationToken ct = default)
    {
        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(ct);
        return contact;
    }

    public async Task<Contact?> UpdateAsync(Contact contact, Guid userId, CancellationToken ct = default)
    {
        var existing = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == contact.Id && c.UserId == userId, ct);
        if (existing == null) return null;
        existing.Name = contact.Name;
        existing.Email = contact.Email;
        existing.Phone = contact.Phone;
        existing.CompanyId = contact.CompanyId;
        await _db.SaveChangesAsync(ct);
        return existing;
    }
}
