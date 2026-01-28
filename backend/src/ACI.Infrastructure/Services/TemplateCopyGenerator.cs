using ACI.Application.Interfaces;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Template-based copy generator (replace with AI/LLM integration when ready).
/// </summary>
public sealed class TemplateCopyGenerator : ICopyGenerator
{
    private static readonly Dictionary<string, string> CopyByTypeAndGoal = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sales-email|Schedule a meeting"] = @"Hi [First Name],

I hope this email finds you well! I wanted to reach out to see if you'd be interested in scheduling a brief call to discuss how our solutions can help [Company Name] achieve your goals.

We've helped companies similar to yours:
• Increase productivity by 40%
• Reduce operational costs by 25%
• Streamline workflows and save time

Would you be available for a 15-minute call next week? I'd love to learn more about your current challenges and share how we can help.

Looking forward to connecting!

Best regards,
[Your Name]",
        ["sales-email|Follow up after demo"] = @"Hi [First Name],

Thank you for taking the time to meet with me and see our demo. I wanted to follow up with a quick summary and next steps.

Based on our conversation, here's what I think could help [Company Name]:
• [Key benefit 1 from demo]
• [Key benefit 2 from demo]

I'd love to schedule a short call to answer any questions and discuss implementation. Would [Day/Time] work for you?

Best regards,
[Your Name]",
        ["follow-up|Follow up after demo"] = @"Hi [First Name],

Thanks for taking the time to meet with me yesterday. I wanted to follow up on our conversation and the next steps we discussed.

As promised, here's [resource/recap]. I'm happy to schedule another call if you'd like to dive deeper into any area.

When would be a good time to connect again?

Best,
[Your Name]",
        ["crm-note|Request feedback"] = @"Call with [Contact Name] – [Date]

Summary:
- Discussed [topic]. They are interested in [X].
- Key pain points: [A], [B].
- Next step: [action] by [date].

Action items:
- [ ] [Task 1]
- [ ] [Task 2]",
        ["deal-message|Close the deal"] = @"Hi [First Name],

I wanted to provide you with a quick update on our proposal for [Company Name].

We're excited about the opportunity to work together. Here's a brief recap of what we've agreed:
• [Point 1]
• [Point 2]

I've attached the final agreement. Please let me know if you have any questions or if you're ready to move forward.

Best regards,
[Your Name]",
        ["workflow-message|Check in on progress"] = @"Hi [First Name],

I wanted to check in and see how things are going with [project/topic we discussed].

If there's anything we can do to support you or if you'd like to schedule a quick call, just let me know.

Best,
[Your Name]",
    };

    private const string DefaultCopy = @"Hi [First Name],

I hope this email finds you well! I wanted to reach out to see if you'd be interested in scheduling a brief call to discuss how our solutions can help [Company Name] achieve your goals.

We've helped companies similar to yours:
• Increase productivity by 40%
• Reduce operational costs by 25%
• Streamline workflows and save time

Would you be available for a 15-minute call next week? I'd love to learn more about your current challenges and share how we can help.

Looking forward to connecting!

Best regards,
[Your Name]";

    public Task<string> GenerateAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? companyName,
        string? brandTone,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var key = $"{copyTypeId}|{goal}";
        var text = CopyByTypeAndGoal.TryGetValue(key, out var copy) ? copy : DefaultCopy;

        if (!string.IsNullOrWhiteSpace(companyName))
            text = text.Replace("[Company Name]", companyName, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(context))
        {
            var ctx = context.Trim();
            text += $"\n\n--- Context provided: {(ctx.Length > 200 ? ctx[..200] : ctx)} ---";
        }

        if (string.Equals(length, "short", StringComparison.OrdinalIgnoreCase))
            text = string.Join("\n\n", text.Split("\n\n").Take(4)) + "\n\nBest,\n[Your Name]";
        else if (string.Equals(length, "long", StringComparison.OrdinalIgnoreCase))
            text += "\n\nI'm also happy to share case studies or arrange a deeper technical discussion if that would be helpful.";

        return Task.FromResult(text);
    }
}
