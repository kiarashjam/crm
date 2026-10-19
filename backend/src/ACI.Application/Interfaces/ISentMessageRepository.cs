using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ISentMessageRepository
{
    Task AddAsync(SentMessage message, CancellationToken ct = default);
}
