namespace ACI.Application.Interfaces;

/// <summary>
/// Wakes this instance's campaign scheduler when someone has just pressed Send, so the
/// campaign starts going out within moments rather than at the next poll. Other instances
/// find it at their next poll; the claim makes sure only one of them sends it.
/// </summary>
public interface ICampaignSignal
{
    void Notify();
}
