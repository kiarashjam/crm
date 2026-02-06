using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ICopyGeneratorApplicationService
{
    Task<string> GenerateAsync(Guid userId, GenerateCopyRequest request, CancellationToken ct = default);
    
    Task<GenerateCopyWithSubjectResponse> GenerateWithRecipientAsync(
        Guid userId, 
        GenerateCopyWithRecipientRequest request, 
        CancellationToken ct = default);
    
    Task<string> RewriteAsync(Guid userId, RewriteCopyRequest request, CancellationToken ct = default);
    
    Task<GenerateCopyWithSubjectResponse> GenerateInLanguageAsync(
        Guid userId, 
        GenerateCopyMultiLanguageRequest request, 
        CancellationToken ct = default);
}
