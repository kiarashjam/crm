using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ICopyGeneratorApplicationService
{
    Task<string> GenerateAsync(Guid userId, GenerateCopyRequest request, CancellationToken ct = default);
}
