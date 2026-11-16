/**
 * Centralized API / user-facing messages (for toasts and UI).
 *
 * Each one is read when it is shown, in the language the app is in at that moment
 * (i18n/current.ts), so a toast after switching to French is in French. The words are
 * in the catalogues under "msg.".
 */

import { translate } from '@/app/i18n/current';
import type { MessageKey } from '@/app/i18n/catalog';

/** An object whose every property reads its message when it is used. */
function words<K extends string>(keys: Record<K, MessageKey>): Readonly<Record<K, string>> {
  const out = {} as Record<K, string>;
  for (const name of Object.keys(keys) as K[]) {
    Object.defineProperty(out, name, { enumerable: true, get: () => translate(keys[name]) });
  }
  return out;
}

export const messages = {
  copy: words({
    generated: 'msg.copy.generated',
    sentToCrm: 'msg.copy.sentToCrm',
    copied: 'msg.copy.copied',
  }),
  settings: words({
    saved: 'msg.settings.saved',
  }),
  success: words({
    leadCreated: 'msg.success.leadCreated',
    leadUpdated: 'msg.success.leadUpdated',
    leadDeleted: 'msg.success.leadDeleted',
    leadConverted: 'msg.success.leadConverted',
    dealCreated: 'msg.success.dealCreated',
    dealUpdated: 'msg.success.dealUpdated',
    dealDeleted: 'msg.success.dealDeleted',
    dealMoved: 'msg.success.dealMoved',
    companyCreated: 'msg.success.companyCreated',
    companyUpdated: 'msg.success.companyUpdated',
    companyDeleted: 'msg.success.companyDeleted',
    contactCreated: 'msg.success.contactCreated',
    contactUpdated: 'msg.success.contactUpdated',
    contactDeleted: 'msg.success.contactDeleted',
    taskCreated: 'msg.success.taskCreated',
    taskUpdated: 'msg.success.taskUpdated',
    taskDeleted: 'msg.success.taskDeleted',
    activityLogged: 'msg.success.activityLogged',
    activityDeleted: 'msg.success.activityDeleted',
  }),
  errors: words({
    generic: 'msg.errors.generic',
    genericWithHelp: 'msg.errors.genericWithHelp',
    loadFailed: 'msg.errors.loadFailed',
  }),
  validation: words({
    nameRequired: 'msg.validation.nameRequired',
    nameAndEmailRequired: 'msg.validation.nameAndEmailRequired',
    nameAndValueRequired: 'msg.validation.nameAndValueRequired',
    titleRequired: 'msg.validation.titleRequired',
    subjectOrBodyRequired: 'msg.validation.subjectOrBodyRequired',
    selectContactOrDeal: 'msg.validation.selectContactOrDeal',
  }),
  auth: words({
    signedIn: 'msg.auth.signedIn',
    accountCreated: 'msg.auth.accountCreated',
    demoMode: 'msg.auth.demoMode',
    twoFactorCodeRequired: 'msg.auth.twoFactorCodeRequired',
    passwordResetEmailSent: 'msg.auth.passwordResetEmailSent',
    passwordResetComplete: 'msg.auth.passwordResetComplete',
    passwordResetRequiresApi: 'msg.auth.passwordResetRequiresApi',
    passwordResetMissingToken: 'msg.auth.passwordResetMissingToken',
  }),
  twoFa: words({
    enabled: 'msg.twoFa.enabled',
    disabled: 'msg.twoFa.disabled',
    scanSecretConfirm: 'msg.twoFa.scanSecretConfirm',
  }),
  task: words({
    completed: 'msg.task.completed',
    reopened: 'msg.task.reopened',
  }),
  help: {
    get linkText() {
      return translate('msg.help.linkText');
    },
    path: '/help',
  },
};
