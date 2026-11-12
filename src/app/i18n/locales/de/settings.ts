import type { settings as en } from '../en/settings';
import type { Translation } from '../../catalog';

export const settings: Translation<typeof en> = {
  'regional.title': 'Währung, Zahlenformat und Zeitzone',
  'regional.intro': 'Neue Deals werden in dieser Währung erfasst, sofern niemand eine andere wählt. Deals behalten ihre Währung: Eine Änderung rechnet nichts um. Beträge erscheinen im Zahlenformat, Daten in der Reihenfolge seiner Region, und Berichte, Prognosen, Abschlussdaten und die Aktivitätslisten zählen Tage und Monate in der Zeitzone. Die Club-Seiten behalten die Club-Währung aus den Hospitality-Einstellungen.',
  'regional.currency': 'Währung',
  'regional.locale': 'Zahlenformat',
  'regional.timeZone': 'Zeitzone',
  'regional.csv': 'CSV-Exporte',
  'regional.csvComma': 'Komma zwischen den Spalten (,)',
  'regional.csvSemicolon': 'Semikolon zwischen den Spalten (;), wie Excel in der Schweiz es erwartet',
  'regional.csvHint': 'Excel mit Schweizer Einstellungen trennt eine CSV-Datei an Semikolons; mit Kommas öffnet sie sich als eine einzige Spalte. Jeder Export dieses Arbeitsbereichs folgt der Wahl und beginnt mit dem Zeichen, an dem Excel UTF-8 erkennt.',
  'regional.preview': 'Beträge sehen so aus: <b>{money}</b>, Daten so: <b>{date}</b>.',
  'regional.previewNow': 'Dort ist es jetzt <b>{now}</b>.',
  'regional.saved': 'Währung, Zahlenformat, Zeitzone und CSV-Exporte des Arbeitsbereichs gespeichert.',
  'regional.notSaved': 'Die Einstellungen wurden nicht gespeichert.',
  'regional.needsManage': 'Ändern kann sie, wer die Einstellungen des Arbeitsbereichs verwaltet: die Inhaberin oder der Inhaber und die Manager.',
  'regional.locale.de-CH': 'Deutsch (Schweiz)',
  'regional.locale.fr-CH': 'Französisch (Schweiz)',
  'regional.locale.it-CH': 'Italienisch (Schweiz)',
  'regional.locale.en-CH': 'Englisch (Schweiz)',
  'regional.locale.de-DE': 'Deutsch (Deutschland)',
  'regional.locale.de-AT': 'Deutsch (Österreich)',
  'regional.locale.fr-FR': 'Französisch (Frankreich)',
  'regional.locale.it-IT': 'Italienisch (Italien)',
  'regional.locale.en-GB': 'Englisch (Vereinigtes Königreich)',
  'regional.locale.en-US': 'Englisch (Vereinigte Staaten)',
};
