/** Centralized API / user-facing messages (for toasts and UI). */

export const messages = {
  connection: {
    connected: 'CRM connected successfully.',
    disconnected: 'Disconnected from CRM.',
  },
  copy: {
    generated: 'Copy generated.',
    sentToCrm: 'Copy sent to CRM.',
    copied: 'Copied to clipboard.',
  },
  settings: {
    saved: 'Settings saved.',
  },
  errors: {
    generic: 'Something went wrong. Please try again.',
    notConnected: 'Connect your CRM first.',
  },
} as const;
