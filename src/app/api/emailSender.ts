import { authFetchJson, isUsingRealApi } from './apiClient';

export interface SendEmailRequest {
  to: string;
  toName?: string;
  subject: string;
  body: string;
  isHtml?: boolean;
  replyTo?: string;
  cc?: string[];
  bcc?: string[];
  copyHistoryId?: string;
  copyTypeId?: string;
}

export interface SendEmailResult {
  success: boolean;
  messageId?: string;
  error?: string;
  sentAt: string;
}

export interface SmtpSettings {
  id?: string;
  host: string;
  port: number;
  username: string;
  password: string;
  useSsl: boolean;
  fromEmail?: string;
  fromName?: string;
  lastTestedAt?: string;
  isValid: boolean;
}

export interface TestSmtpResult {
  success: boolean;
  error?: string;
  latencyMs: number;
}

// Mock data for development
let mockSettings: SmtpSettings | null = null;

const USE_MOCK = () => !isUsingRealApi(); // Use mock when not connected to real API

/**
 * Send an email using configured SMTP settings
 */
export async function sendEmail(request: SendEmailRequest): Promise<SendEmailResult> {
  if (USE_MOCK()) {
    // Simulate sending
    await new Promise(r => setTimeout(r, 1000));
    
    if (!mockSettings || !mockSettings.isValid) {
      return {
        success: false,
        error: 'SMTP settings not configured. Please configure your email settings first.',
        sentAt: new Date().toISOString(),
      };
    }
    
    return {
      success: true,
      messageId: `mock-${Date.now()}`,
      sentAt: new Date().toISOString(),
    };
  }
  
  return authFetchJson<SendEmailResult>('/api/emailsender/send', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

/**
 * Get current SMTP settings (password masked)
 */
export async function getSmtpSettings(): Promise<SmtpSettings | null> {
  if (USE_MOCK()) {
    return mockSettings ? { ...mockSettings, password: '********' } : null;
  }
  
  try {
    return await authFetchJson<SmtpSettings>('/api/emailsender/settings');
  } catch {
    return null;
  }
}

/**
 * Save SMTP settings
 */
export async function saveSmtpSettings(settings: SmtpSettings): Promise<SmtpSettings> {
  if (USE_MOCK()) {
    await new Promise(r => setTimeout(r, 500));
    mockSettings = {
      ...settings,
      id: 'mock-settings',
      lastTestedAt: new Date().toISOString(),
      isValid: true, // Assume valid for mock
    };
    return { ...mockSettings, password: '********' };
  }
  
  return authFetchJson<SmtpSettings>('/api/emailsender/settings', {
    method: 'POST',
    body: JSON.stringify(settings),
  });
}

/**
 * Test SMTP connection without saving
 */
export async function testSmtpConnection(settings: Omit<SmtpSettings, 'id' | 'lastTestedAt' | 'isValid'>): Promise<TestSmtpResult> {
  if (USE_MOCK()) {
    await new Promise(r => setTimeout(r, 1500));
    
    // Simulate connection test
    const isValid = Boolean(settings.host.includes('.') && settings.port > 0 && settings.username && settings.password);
    
    return {
      success: isValid,
      error: isValid ? undefined : 'Invalid SMTP settings. Please check your configuration.',
      latencyMs: Math.floor(Math.random() * 200) + 100,
    };
  }
  
  return authFetchJson<TestSmtpResult>('/api/emailsender/test', {
    method: 'POST',
    body: JSON.stringify(settings),
  });
}

/**
 * Common SMTP configurations
 */
export const COMMON_SMTP_CONFIGS = {
  gmail: {
    host: 'smtp.gmail.com',
    port: 587,
    useSsl: true,
    note: 'Use an App Password, not your regular password',
  },
  outlook: {
    host: 'smtp.office365.com',
    port: 587,
    useSsl: true,
    note: 'Use your Microsoft account password',
  },
  yahoo: {
    host: 'smtp.mail.yahoo.com',
    port: 587,
    useSsl: true,
    note: 'Use an App Password',
  },
  sendgrid: {
    host: 'smtp.sendgrid.net',
    port: 587,
    useSsl: true,
    note: 'Use "apikey" as username and your API key as password',
  },
  mailgun: {
    host: 'smtp.mailgun.org',
    port: 587,
    useSsl: true,
    note: 'Use your SMTP credentials from Mailgun dashboard',
  },
};
