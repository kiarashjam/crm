using ACI.Application.DTOs;
using ACI.Application.Interfaces;

namespace ACI.Application.Services;

public class CopyGeneratorService : ICopyGeneratorApplicationService
{
    private readonly ICopyGenerator _generator;

    public CopyGeneratorService(ICopyGenerator generator) => _generator = generator;

    public async Task<string> GenerateAsync(Guid userId, GenerateCopyRequest request, CancellationToken ct = default)
    {
        return await _generator.GenerateAsync(
            request.CopyTypeId,
            request.Goal,
            request.Context,
            request.Length ?? "medium",
            request.CompanyName,
            request.BrandTone,
            ct);
    }
}
