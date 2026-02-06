using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public class ABTestService : IABTestService
{
    // In-memory storage for demo (would use repository in production)
    private static readonly List<ABTest> _tests = new();
    
    public Task<IReadOnlyList<ABTestDto>> GetTestsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var userTests = _tests
            .Where(t => t.UserId == userId || (organizationId != null && t.OrganizationId == organizationId))
            .Select(t => MapToDto(t, includeVariants: false))
            .ToList();
        
        return Task.FromResult<IReadOnlyList<ABTestDto>>(userTests);
    }
    
    public Task<ABTestDto?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var test = _tests.FirstOrDefault(t => t.Id == id && t.UserId == userId);
        return Task.FromResult(test != null ? MapToDto(test, includeVariants: true) : null);
    }
    
    public Task<ABTestDto> CreateAsync(Guid userId, Guid? organizationId, CreateABTestRequest request, CancellationToken ct = default)
    {
        var test = new ABTest
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            UserId = userId,
            OrganizationId = organizationId,
            CopyTypeId = Enum.TryParse<ACI.Domain.Enums.CopyTypeId>(request.CopyTypeId.Replace("-", ""), true, out var typeId) ? typeId : ACI.Domain.Enums.CopyTypeId.SalesEmail,
            Goal = request.Goal,
            Status = ABTestStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            Variants = request.Variants.Select(v => new ABTestVariant
            {
                Id = Guid.NewGuid(),
                Name = v.Name,
                Subject = v.Subject,
                Body = v.Body,
            }).ToList(),
        };
        
        _tests.Add(test);
        return Task.FromResult(MapToDto(test, includeVariants: true));
    }
    
    public Task<ABTestDto> UpdateAsync(Guid userId, Guid id, UpdateABTestRequest request, CancellationToken ct = default)
    {
        var test = _tests.FirstOrDefault(t => t.Id == id && t.UserId == userId);
        if (test == null) throw new InvalidOperationException("Test not found");
        
        if (request.Name != null) test.Name = request.Name;
        if (request.Description != null) test.Description = request.Description;
        
        if (request.Status != null)
        {
            switch (request.Status.ToLowerInvariant())
            {
                case "start":
                    test.Status = ABTestStatus.Running;
                    test.StartedAtUtc = DateTime.UtcNow;
                    break;
                case "complete":
                    test.Status = ABTestStatus.Completed;
                    test.EndedAtUtc = DateTime.UtcNow;
                    // Pick winner (highest reply rate)
                    test.WinningVariantId = test.Variants.OrderByDescending(v => v.ReplyRate).FirstOrDefault()?.Id;
                    break;
                case "cancel":
                    test.Status = ABTestStatus.Cancelled;
                    test.EndedAtUtc = DateTime.UtcNow;
                    break;
            }
        }
        
        return Task.FromResult(MapToDto(test, includeVariants: true));
    }
    
    public Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var test = _tests.FirstOrDefault(t => t.Id == id && t.UserId == userId);
        if (test != null) _tests.Remove(test);
        return Task.CompletedTask;
    }
    
    public Task<ABTestVariantDto> TrackVariantEventAsync(Guid variantId, string eventType, CancellationToken ct = default)
    {
        ABTestVariant? variant = null;
        foreach (var test in _tests)
        {
            variant = test.Variants.FirstOrDefault(v => v.Id == variantId);
            if (variant != null) break;
        }
        
        if (variant == null) throw new InvalidOperationException("Variant not found");
        
        switch (eventType.ToLowerInvariant())
        {
            case "send":
                variant.SendCount++;
                break;
            case "open":
                variant.OpenCount++;
                break;
            case "click":
                variant.ClickCount++;
                break;
            case "reply":
                variant.ReplyCount++;
                break;
        }
        
        return Task.FromResult(MapVariantToDto(variant));
    }
    
    private static ABTestDto MapToDto(ABTest t, bool includeVariants) => new(
        t.Id,
        t.Name,
        t.Description,
        t.CopyTypeId.ToString(),
        t.Goal,
        t.Status.ToString(),
        t.CreatedAtUtc,
        t.StartedAtUtc,
        t.EndedAtUtc,
        t.WinningVariantId,
        includeVariants ? t.Variants.Select(MapVariantToDto).ToList() : null
    );
    
    private static ABTestVariantDto MapVariantToDto(ABTestVariant v) => new(
        v.Id,
        v.Name,
        v.Subject,
        v.Body,
        v.SendCount,
        v.OpenCount,
        v.ClickCount,
        v.ReplyCount,
        v.OpenRate,
        v.ClickRate,
        v.ReplyRate
    );
}
