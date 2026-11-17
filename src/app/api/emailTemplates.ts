// The workspace's prepared emails, each in French and English, for the email composer.
import { authFetchJson, servesDemoData } from './apiClient';

export type EmailLanguage = 'fr' | 'en';

export interface EmailTemplateVersion {
  language: EmailLanguage;
  subject: string;
  body: string;
}

export interface EmailTemplate {
  id: string;
  name: string;
  category?: string | null;
  description?: string | null;
  /** One of the starter set the workspace was given (it may have been changed since). */
  starter: boolean;
  versions: EmailTemplateVersion[];
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface EmailTemplateList {
  templates: EmailTemplate[];
  /** The languages a prepared email can be in, in the order offered. */
  languages: EmailLanguage[];
  /** The language to start in when the person has not chosen one. */
  defaultLanguage: EmailLanguage;
  /** The placeholders filled for each recipient when the email is sent. */
  placeholders: string[];
}

export interface SaveEmailTemplate {
  name: string;
  category?: string | null;
  description?: string | null;
  versions: EmailTemplateVersion[];
}

export const EMAIL_LANGUAGES: { code: EmailLanguage; label: string; short: string }[] = [
  { code: 'fr', label: 'Français', short: 'FR' },
  { code: 'en', label: 'English', short: 'EN' },
];

const EMPTY: EmailTemplateList = {
  templates: [], languages: ['fr', 'en'], defaultLanguage: 'en',
  placeholders: ['firstName', 'lastName', 'name', 'email', 'company', 'senderName', 'workspace'],
};

/** The workspace's prepared emails; the first read gives a new workspace its starter set. */
export async function getEmailTemplates(): Promise<EmailTemplateList> {
  if (servesDemoData()) return EMPTY;
  return (await authFetchJson<EmailTemplateList>('/api/email-templates')) ?? EMPTY;
}

export async function createEmailTemplate(request: SaveEmailTemplate): Promise<EmailTemplate> {
  return authFetchJson<EmailTemplate>('/api/email-templates', { method: 'POST', body: JSON.stringify(request) });
}

export async function updateEmailTemplate(id: string, request: SaveEmailTemplate): Promise<EmailTemplate> {
  return authFetchJson<EmailTemplate>(`/api/email-templates/${id}`, { method: 'PUT', body: JSON.stringify(request) });
}

export async function deleteEmailTemplate(id: string): Promise<void> {
  await authFetchJson<void>(`/api/email-templates/${id}`, { method: 'DELETE' });
}
