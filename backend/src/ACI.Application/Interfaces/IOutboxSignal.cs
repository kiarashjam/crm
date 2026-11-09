namespace ACI.Application.Interfaces;

/// <summary>
/// Wakes this instance's outbox dispatcher when something has just been queued, so an
/// automation's email or a webhook goes out within moments rather than at the next
/// poll. Other instances find it at their next poll.
/// </summary>
public interface IOutboxSignal
{
    void Notify();
}
