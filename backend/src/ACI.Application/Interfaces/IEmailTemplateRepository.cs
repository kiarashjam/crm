using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>A workspace's prepared emails, each with its versions.</summary>
public interface IEmailTemplateRepository
{
    Task<IReadOnlyList<EmailTemplate>> ListAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>One of the workspace's, tracked, with its versions; null when it has none by that id.</summary>
    Task<EmailTemplate?> GetAsync(Guid id, Guid organizationId, CancellationToken ct = default);

    Task<bool> NameTakenAsync(Guid organizationId, string name, Guid? exceptId, CancellationToken ct = default);

    Task AddAsync(EmailTemplate template, CancellationToken ct = default);

    /// <summary>Saves changes to a template read with <see cref="GetAsync"/>, its versions replaced.</summary>
    Task SaveAsync(EmailTemplate template, IReadOnlyList<EmailTemplateVersion> versions, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Gives the workspace the starter set and records that it has been given, in one save;
    /// false when another request gave it first.
    /// </summary>
    Task<bool> SeedAsync(Guid organizationId, IReadOnlyList<EmailTemplate> starters, DateTime at, CancellationToken ct = default);
}
