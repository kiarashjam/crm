namespace ACI.Application.Interfaces;

public interface IOutboundWebhookPublisher
{
    Task PublishAsync(Guid? organizationId, string eventName, object payload, CancellationToken ct = default);
}
