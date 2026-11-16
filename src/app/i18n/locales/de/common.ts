// Schweizer Hochdeutsch: «ss» statt «ß».
import type { common as en } from '../en/common';
import type { Translation } from '../../catalog';

export const common: Translation<typeof en> = {
  'common.loading': 'Wird geladen …',
  'common.tryAgain': 'Erneut versuchen',
  'common.checkConnection': 'Bitte prüfen Sie Ihre Verbindung.',
  'common.sending': 'Wird gesendet …',
  'common.saving': 'Wird gespeichert …',
  'common.save': 'Speichern',
  'common.cancel': 'Abbrechen',
  'common.close': 'Schliessen',
  'common.edit': 'Bearbeiten',
  'common.delete': 'Löschen',
  'common.add': 'Hinzufügen',
  'common.yes': 'Ja',
  'common.no': 'Nein',
  'common.none': 'Keine',
  'common.or': 'oder',
  'common.search': 'Suchen',
  'common.export': 'Exportieren',
  'common.nothingToExport': 'Nichts zu exportieren',
  'common.exportedRows': { one: '{count} Zeile exportiert', other: '{count} Zeilen exportiert' },
  'common.couldNotLoad': 'Dies konnte nicht geladen werden.',
  'common.notSaved': 'Das wurde nicht gespeichert.',

  'language.label': 'Sprache',
  'language.automatic': 'Automatisch',
  'language.automaticHint': 'Folgt der Sprache Ihres Arbeitsbereichs, sonst der Ihres Browsers.',
  'language.pickerLabel': 'Sprache wählen',
  'language.saved': 'Sprache gespeichert.',
  'language.notice': 'Cadence folgt jetzt der Sprache Ihres Arbeitsbereichs. Wählen Sie, in welcher Sprache Sie es lesen möchten:',
  'language.noticeDismiss': 'Schliessen',

  'date.dd': 'TT',
  'date.mm': 'MM',
  'date.yyyy': 'JJJJ',
  'date.pick': 'Datum wählen',
  'date.invalid': 'Kein Datum. Schreiben Sie es als {pattern}.',

  'load.failed': 'Konnte nicht geladen werden: {what}.',
  'load.stale': 'Konnte nicht aktualisiert werden: {what}. Angezeigt wird der zuletzt geladene Stand.',
  'load.noAnswer': 'Der Server war nicht erreichbar oder hat nicht geantwortet.',
  'load.retrying': 'Neuer Versuch …',
  'readOnly.banner': 'Nur-Lese-Zugriff auf <b>{org}</b> – Sie sehen hier alles, können aber nichts ändern.',
  'readOnly.thisOrg': 'diesen Arbeitsbereich',
};
