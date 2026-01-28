import type { GenerateCopyParams } from './types';

const COPY_BY_TYPE_AND_GOAL: Record<string, string> = {
  'sales-email|Schedule a meeting': `Hi [First Name],

I hope this email finds you well! I wanted to reach out to see if you'd be interested in scheduling a brief call to discuss how our solutions can help [Company Name] achieve your goals.

We've helped companies similar to yours:
• Increase productivity by 40%
• Reduce operational costs by 25%
• Streamline workflows and save time

Would you be available for a 15-minute call next week? I'd love to learn more about your current challenges and share how we can help.

Looking forward to connecting!

Best regards,
[Your Name]`,
  'sales-email|Follow up after demo': `Hi [First Name],

Thank you for taking the time to meet with me and see our demo. I wanted to follow up with a quick summary and next steps.

Based on our conversation, here’s what I think could help [Company Name]:
• [Key benefit 1 from demo]
• [Key benefit 2 from demo]

I’d love to schedule a short call to answer any questions and discuss implementation. Would [Day/Time] work for you?

Best regards,
[Your Name]`,
  'follow-up|Follow up after demo': `Hi [First Name],

Thanks for taking the time to meet with me yesterday. I wanted to follow up on our conversation and the next steps we discussed.

As promised, here’s [resource/recap]. I’m happy to schedule another call if you’d like to dive deeper into any area.

When would be a good time to connect again?

Best,
[Your Name]`,
  'crm-note|Request feedback': `Call with [Contact Name] – [Date]

Summary:
- Discussed [topic]. They are interested in [X].
- Key pain points: [A], [B].
- Next step: [action] by [date].

Action items:
- [ ] [Task 1]
- [ ] [Task 2]`,
  'deal-message|Close the deal': `Hi [First Name],

I wanted to provide you with a quick update on our proposal for [Company Name].

We’re excited about the opportunity to work together. Here’s a brief recap of what we’ve agreed:
• [Point 1]
• [Point 2]

I’ve attached the final agreement. Please let me know if you have any questions or if you’re ready to move forward.

Best regards,
[Your Name]`,
  'workflow-message|Check in on progress': `Hi [First Name],

I wanted to check in and see how things are going with [project/topic we discussed].

If there’s anything we can do to support you or if you’d like to schedule a quick call, just let me know.

Best,
[Your Name]`,
};

const DEFAULT_COPY = `Hi [First Name],

I hope this email finds you well! I wanted to reach out to see if you'd be interested in scheduling a brief call to discuss how our solutions can help [Company Name] achieve your goals.

We've helped companies similar to yours:
• Increase productivity by 40%
• Reduce operational costs by 25%
• Streamline workflows and save time

Would you be available for a 15-minute call next week? I'd love to learn more about your current challenges and share how we can help.

Looking forward to connecting!

Best regards,
[Your Name]`;

/** Generate copy (demo: returns predefined or parameterized text after delay). */
export async function generateCopy(params: GenerateCopyParams): Promise<string> {
  await delay(1500 + Math.random() * 500);
  const key = `${params.copyTypeId}|${params.goal}`;
  let text = COPY_BY_TYPE_AND_GOAL[key] ?? DEFAULT_COPY;
  if (params.companyName) {
    text = text.replace(/\[Company Name\]/g, params.companyName);
  }
  if (params.context?.trim()) {
    text = text + `\n\n--- Context provided: ${params.context.trim().slice(0, 200)} ---`;
  }
  if (params.length === 'short') {
    text = text.split('\n\n').slice(0, 4).join('\n\n') + '\n\nBest,\n[Your Name]';
  } else if (params.length === 'long') {
    text = text + '\n\nI’m also happy to share case studies or arrange a deeper technical discussion if that would be helpful.';
  }
  return text;
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}
