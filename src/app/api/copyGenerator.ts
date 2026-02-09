import type { GenerateCopyParams } from './types';
import { isUsingRealApi, authFetchJson } from './apiClient';

// Extended copy types including new ones
export type ExtendedCopyTypeId = 
  | 'sales-email' 
  | 'follow-up' 
  | 'crm-note' 
  | 'deal-message' 
  | 'workflow-message'
  | 'linkedin-connect'
  | 'linkedin-inmail'
  | 'sms'
  | 'call-script'
  | 'meeting-agenda';

export interface RecipientContext {
  name?: string;
  email?: string;
  company?: string;
  title?: string;
  type?: 'lead' | 'contact' | 'deal';
  lastActivity?: string;
  dealStage?: string;
  dealValue?: string;
}

export interface GenerateCopyWithRecipientParams extends GenerateCopyParams {
  recipient?: RecipientContext;
}

export interface GenerateCopyResult {
  body: string;
  subject?: string;
}

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

Based on our conversation, here's what I think could help [Company Name]:
• [Key benefit 1 from demo]
• [Key benefit 2 from demo]

I'd love to schedule a short call to answer any questions and discuss implementation. Would [Day/Time] work for you?

Best regards,
[Your Name]`,
  'follow-up|Follow up after demo': `Hi [First Name],

Thanks for taking the time to meet with me yesterday. I wanted to follow up on our conversation and the next steps we discussed.

As promised, here's [resource/recap]. I'm happy to schedule another call if you'd like to dive deeper into any area.

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

We're excited about the opportunity to work together. Here's a brief recap of what we've agreed:
• [Point 1]
• [Point 2]

I've attached the final agreement. Please let me know if you have any questions or if you're ready to move forward.

Best regards,
[Your Name]`,
  'workflow-message|Check in on progress': `Hi [First Name],

I wanted to check in and see how things are going with [project/topic we discussed].

If there's anything we can do to support you or if you'd like to schedule a quick call, just let me know.

Best,
[Your Name]`,
  // LinkedIn Connect
  'linkedin-connect|Schedule a meeting': `Hi [First Name],

I noticed your work at [Company Name] and thought it would be great to connect.

I'd love to share some ideas that have helped similar companies achieve impressive results.

Looking forward to connecting!`,
  'linkedin-connect|Share resources': `Hi [First Name],

I came across your profile and thought you might find our recent research on [topic] valuable.

Would love to connect and share!`,
  // LinkedIn InMail
  'linkedin-inmail|Schedule a meeting': `Hi [First Name],

I hope this message finds you well. I've been following [Company Name]'s growth and believe we could add significant value to your team.

Would you be open to a 15-minute call to explore how we've helped similar companies?

Best regards,
[Your Name]`,
  // SMS
  'sms|Schedule a meeting': `Hi [First Name], this is [Your Name]. Quick question - do you have 15 min this week to chat about [topic]? Let me know what works!`,
  'sms|Follow up after demo': `Hi [First Name]! Thanks for the demo. Any questions I can help with? Happy to hop on a quick call.`,
  'sms|Check in on progress': `Hey [First Name], just checking in on [topic]. Any updates on your end?`,
  // Call Script
  'call-script|Schedule a meeting': `CALL SCRIPT: Schedule Meeting

OPENING:
"Hi [First Name], this is [Your Name] from [Company]. Do you have a quick moment?"

IF YES:
"Great! I'm reaching out because we've helped companies like [Company Name] [key benefit]. I'd love to share how in a brief 15-minute call. Would [Day] or [Day] work better for you?"

HANDLE OBJECTIONS:
- "I'm busy" → "Totally understand. What's a better time? Even 10 minutes could be valuable."
- "Send info" → "Happy to! What's most important for you to see - ROI data, case studies, or feature overview?"

CLOSE:
"Perfect, I'll send a calendar invite for [time]. Looking forward to it!"`,
  // Meeting Agenda
  'meeting-agenda|Follow up after demo': `MEETING AGENDA: Follow-up Discussion

Date: [Date]
Time: [Time]
Attendees: [Names]

OBJECTIVES:
1. Address questions from demo
2. Discuss implementation timeline
3. Align on next steps

AGENDA:
1. Recap of demo highlights (5 min)
2. Q&A and concerns (15 min)
3. Implementation overview (10 min)
4. Pricing and timeline discussion (10 min)
5. Next steps and action items (5 min)

PREPARATION:
- Review demo notes
- Prepare implementation timeline
- Have pricing options ready`,
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

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

/** Generate copy (real API or mock). */
export async function generateCopy(params: GenerateCopyParams): Promise<string> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<{ copy: string }>('/api/copy/generate', {
      method: 'POST',
      body: JSON.stringify({
        copyTypeId: params.copyTypeId,
        goal: params.goal,
        context: params.context ?? null,
        length: params.length ?? 'medium',
        brandName: params.brandName ?? null,
        brandTone: params.brandTone ?? null,
      }),
    });
    return res?.copy ?? '';
  }
  await delay(1500 + Math.random() * 500);
  const key = `${params.copyTypeId}|${params.goal}`;
  let text = COPY_BY_TYPE_AND_GOAL[key] ?? DEFAULT_COPY;
  if (params.brandName) {
    text = text.replace(/\[Company Name\]/g, params.brandName);
  }
  if (params.context?.trim()) {
    text = text + `\n\n--- Context provided: ${params.context.trim().slice(0, 200)} ---`;
  }
  if (params.length === 'short') {
    text = text.split('\n\n').slice(0, 4).join('\n\n') + '\n\nBest,\n[Your Name]';
  } else if (params.length === 'long') {
    text = text + "\n\nI'm also happy to share case studies or arrange a deeper technical discussion if that would be helpful.";
  }
  return text;
}

/** Generate copy with recipient context for personalization. */
export async function generateCopyWithRecipient(params: GenerateCopyWithRecipientParams): Promise<GenerateCopyResult> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<{ body: string; subject?: string }>('/api/copy/generate-with-recipient', {
      method: 'POST',
      body: JSON.stringify({
        copyTypeId: params.copyTypeId,
        goal: params.goal,
        context: params.context ?? null,
        length: params.length ?? 'medium',
        brandName: params.brandName ?? null,
        brandTone: params.brandTone ?? null,
        recipient: params.recipient ?? null,
      }),
    });
    return { body: res?.body ?? '', subject: res?.subject };
  }
  
  // Mock implementation
  await delay(1500 + Math.random() * 500);
  let text = await generateCopy(params);
  
  // Personalize with recipient data
  if (params.recipient) {
    if (params.recipient.name) {
      const firstName = params.recipient.name.split(' ')[0] || '';
      text = text.replace(/\[First Name\]/g, firstName);
      text = text.replace(/\[Contact Name\]/g, params.recipient.name);
    }
    if (params.recipient.company) {
      text = text.replace(/\[Company Name\]/g, params.recipient.company);
    }
  }
  
  // Generate subject for email types
  let subject: string | undefined;
  if (params.copyTypeId.includes('email')) {
    const company = params.recipient?.company ?? '[Company]';
    subject = params.goal === 'Schedule a meeting' 
      ? `Meeting request: ${company} partnership`
      : `Following up: ${company}`;
  }
  
  return { body: text, subject };
}

/** Rewrite copy with a different tone/style. */
export async function rewriteCopy(originalCopy: string, adjustment: 'shorter' | 'friendlier' | 'persuasive'): Promise<string> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<{ copy: string }>('/api/copy/rewrite', {
      method: 'POST',
      body: JSON.stringify({
        originalCopy,
        adjustment,
      }),
    });
    return res?.copy ?? originalCopy;
  }
  
  // Mock implementation
  await delay(1000 + Math.random() * 500);
  
  if (adjustment === 'shorter') {
    return makeShorter(originalCopy);
  } else if (adjustment === 'friendlier') {
    return makeFriendlier(originalCopy);
  } else if (adjustment === 'persuasive') {
    return makePersuasive(originalCopy);
  }
  
  return originalCopy;
}

function makeShorter(text: string): string {
  const lines = text.split('\n').filter(line => {
    const trimmed = line.trim();
    // Skip filler phrases
    if (trimmed.startsWith('I hope this') || trimmed.startsWith('I wanted to reach out')) {
      return false;
    }
    return true;
  });
  
  // Limit bullet points
  let bulletCount = 0;
  const result = lines.filter(line => {
    if (line.trim().startsWith('•') || line.trim().startsWith('-')) {
      bulletCount++;
      return bulletCount <= 2;
    }
    return true;
  });
  
  let final = result.join('\n').trim();
  if (!final.includes('Best') && !final.includes('[Your Name]')) {
    final += '\n\nBest,\n[Your Name]';
  }
  
  return final;
}

function makeFriendlier(text: string): string {
  let result = text;
  
  // Add greeting emoji
  if (result.startsWith('Hi ') || result.startsWith('Hello ')) {
    result = result.replace(/^(Hi|Hello) /, 'Hey ');
    const firstLineEnd = result.indexOf('\n');
    if (firstLineEnd > 0 && !result.slice(0, firstLineEnd).includes('👋')) {
      result = result.slice(0, firstLineEnd) + ' 👋' + result.slice(firstLineEnd);
    }
  }
  
  // Replace formal phrases
  result = result.replace(/I hope this email finds you well/gi, "Hope you're having a great day");
  result = result.replace(/I wanted to reach out/gi, 'Wanted to connect');
  result = result.replace(/Best regards/gi, 'Cheers');
  result = result.replace(/Looking forward to connecting/gi, "Can't wait to chat");
  result = result.replace(/Please let me know/gi, 'Just let me know');
  result = result.replace(/Would you be available/gi, 'Any chance you\'re free');
  
  // Add emojis to bullet points
  result = result.replace(/• Increase productivity/gi, '• 🚀 Boost productivity');
  result = result.replace(/• Reduce operational costs/gi, '• 💰 Cut costs');
  result = result.replace(/• Streamline workflows/gi, '• ⚡ Smoother workflows');
  
  return result;
}

function makePersuasive(text: string): string {
  let result = text;
  
  // Strengthen opening
  result = result.replace(
    /I wanted to reach out to see if you'd be interested in/gi,
    "I'm reaching out because I believe you're missing out on"
  );
  
  // Add urgency
  result = result.replace(/next week/gi, 'this week');
  result = result.replace(/Would you be available/gi, 'Are you available');
  
  // Strengthen value props
  result = result.replace(/by 40%/gi, 'by 40% within 90 days');
  result = result.replace(/by 25%/gi, 'by 25% in the first quarter');
  
  // Strengthen closing
  result = result.replace(
    /Looking forward to connecting/gi,
    'The sooner we connect, the sooner you\'ll see results. I look forward to your response'
  );
  
  // Add FOMO
  if (!result.includes('competitors') && result.length < 1500) {
    const closingIndex = result.lastIndexOf('Best');
    if (closingIndex > 0) {
      result = result.slice(0, closingIndex) + 
        "\nYour competitors aren't waiting. Neither should you.\n\n" + 
        result.slice(closingIndex);
    }
  }
  
  return result;
}

// ============================================
// MULTI-LANGUAGE SUPPORT
// ============================================

export type SupportedLanguage = 
  | 'en' | 'es' | 'fr' | 'de' | 'it' | 'pt' | 'nl' 
  | 'zh' | 'ja' | 'ko' | 'ar' | 'ru' | 'hi' | 'pl' 
  | 'tr' | 'sv' | 'no' | 'da' | 'fi';

export const SUPPORTED_LANGUAGES: { code: SupportedLanguage; name: string }[] = [
  { code: 'en', name: 'English' },
  { code: 'es', name: 'Spanish' },
  { code: 'fr', name: 'French' },
  { code: 'de', name: 'German' },
  { code: 'it', name: 'Italian' },
  { code: 'pt', name: 'Portuguese' },
  { code: 'nl', name: 'Dutch' },
  { code: 'zh', name: 'Chinese (Simplified)' },
  { code: 'ja', name: 'Japanese' },
  { code: 'ko', name: 'Korean' },
  { code: 'ar', name: 'Arabic' },
  { code: 'ru', name: 'Russian' },
  { code: 'hi', name: 'Hindi' },
  { code: 'pl', name: 'Polish' },
  { code: 'tr', name: 'Turkish' },
  { code: 'sv', name: 'Swedish' },
  { code: 'no', name: 'Norwegian' },
  { code: 'da', name: 'Danish' },
  { code: 'fi', name: 'Finnish' },
];

export interface GenerateCopyMultiLanguageParams extends GenerateCopyWithRecipientParams {
  targetLanguage: SupportedLanguage;
}

/** Generate copy in a specific language. */
export async function generateCopyInLanguage(params: GenerateCopyMultiLanguageParams): Promise<GenerateCopyResult> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<{ body: string; subject?: string }>('/api/copy/generate-multilang', {
      method: 'POST',
      body: JSON.stringify({
        copyTypeId: params.copyTypeId,
        goal: params.goal,
        brandTone: params.brandTone ?? null,
        length: params.length ?? 'medium',
        targetLanguage: params.targetLanguage,
        recipient: params.recipient ?? null,
        crmObject: null,
      }),
    });
    return { body: res?.body ?? '', subject: res?.subject };
  }
  
  // Mock: return English with a note
  const result = await generateCopyWithRecipient(params);
  const langName = SUPPORTED_LANGUAGES.find(l => l.code === params.targetLanguage)?.name ?? params.targetLanguage;
  return {
    body: result.body + `\n\n[Note: Multi-language generation requires AI. Please translate to ${langName}.]`,
    subject: result.subject,
  };
}

// ============================================
// ANALYTICS
// ============================================

