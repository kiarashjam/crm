// Settings: the workspace's regional formats and its CSV exports.
import type { Message } from '../../format';

export const settings = {
  'regional.title': 'Currency, number format and time zone',
  'regional.intro': 'New deals are priced in this currency unless someone picks another. Deals keep the currency they were priced in: changing it converts nothing. Amounts are written in the number format, dates in the order of its region, and reports, forecasts, deal close dates and the activity lists count days and months in the time zone. Club pages keep the club currency set in Hospitality settings.',
  'regional.currency': 'Currency',
  'regional.locale': 'Number format',
  'regional.timeZone': 'Time zone',
  'regional.csv': 'CSV exports',
  'regional.csvComma': 'Comma between columns (,)',
  'regional.csvSemicolon': 'Semicolon between columns (;), as Excel in Switzerland expects',
  'regional.csvHint': 'Excel set up for Switzerland splits a CSV on semicolons; with commas it opens as one column. Every export from this workspace uses the choice, and starts with the mark that tells Excel it is UTF-8.',
  'regional.preview': 'Amounts look like <b>{money}</b> and dates like <b>{date}</b>.',
  'regional.previewNow': 'It is now <b>{now}</b> there.',
  'regional.saved': 'Workspace currency, number format, time zone and CSV exports saved.',
  'regional.notSaved': 'The settings were not saved.',
  'regional.needsManage': "Changing these takes managing the workspace's settings, which the owner and managers have.",
  'regional.locale.de-CH': 'German (Switzerland)',
  'regional.locale.fr-CH': 'French (Switzerland)',
  'regional.locale.it-CH': 'Italian (Switzerland)',
  'regional.locale.en-CH': 'English (Switzerland)',
  'regional.locale.de-DE': 'German (Germany)',
  'regional.locale.de-AT': 'German (Austria)',
  'regional.locale.fr-FR': 'French (France)',
  'regional.locale.it-IT': 'Italian (Italy)',
  'regional.locale.en-GB': 'English (United Kingdom)',
  'regional.locale.en-US': 'English (United States)',
} satisfies Record<string, Message>;
