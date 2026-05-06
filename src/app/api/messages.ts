/** Centralized API / user-facing messages (for toasts and UI). */

export const messages = {
  copy: {
    generated: 'Copy generated.',
    sentToCrm: 'Copy sent to CRM.',
    copied: 'Copied to clipboard.',
  },
  settings: {
    saved: 'Settings saved.',
  },
  success: {
    leadCreated: 'Lead created.',
    leadUpdated: 'Lead updated.',
    leadDeleted: 'Lead deleted.',
    leadConverted: 'Lead converted.',
    dealCreated: 'Deal created.',
    dealUpdated: 'Deal updated.',
    dealDeleted: 'Deal deleted.',
    dealMoved: 'Deal moved.',
    companyCreated: 'Company created.',
    companyUpdated: 'Company updated.',
    companyDeleted: 'Company deleted.',
    contactCreated: 'Contact created.',
    contactUpdated: 'Contact updated.',
    contactDeleted: 'Contact deleted.',
    taskCreated: 'Task created.',
    taskUpdated: 'Task updated.',
    taskDeleted: 'Task deleted.',
    activityLogged: 'Activity logged.',
    activityDeleted: 'Activity deleted.',
  },
  errors: {
    generic: 'Something went wrong. Please try again.',
    genericWithHelp: 'Something went wrong. Please try again. Need help?',
    loadFailed: 'Failed to load. Please try again.',
  },
  validation: {
    nameRequired: 'Name is required.',
    nameAndEmailRequired: 'Name and email are required.',
    nameAndValueRequired: 'Name and value are required.',
    titleRequired: 'Title is required.',
    subjectOrBodyRequired: 'Subject or body is required.',
    selectContactOrDeal: 'Select at least one: Create contact or Create deal.',
  },
  auth: {
    signedIn: 'Signed in.',
    accountCreated: 'Account created.',
    demoMode: 'Demo mode',
    twoFactorCodeRequired: 'Two-factor code required',
    passwordResetEmailSent:
      'If an account exists for that email, you will receive reset instructions shortly.',
    passwordResetComplete: 'Your password was updated. You can sign in now.',
    passwordResetRequiresApi: 'Password reset requires the app to be connected to the Cadence API (set VITE_API_URL).',
    passwordResetMissingToken: 'This reset link is missing a token. Open the link from your email or request a new reset.',
  },
  twoFa: {
    enabled: '2FA enabled.',
    disabled: '2FA disabled.',
    scanSecretConfirm: 'Scan the secret in your authenticator, then confirm the code.',
  },
  task: {
    completed: 'Task completed.',
    reopened: 'Task reopened.',
  },
  help: {
    linkText: 'Need help?',
    path: '/help',
  },
} as const;
