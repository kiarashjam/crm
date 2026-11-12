import type { settings as en } from '../en/settings';
import type { Translation } from '../../catalog';

export const settings: Translation<typeof en> = {
  'regional.title': 'Monnaie, format des nombres et fuseau horaire',
  'regional.intro': 'Les nouvelles affaires sont saisies dans cette monnaie, sauf si quelqu’un en choisit une autre. Les affaires gardent leur monnaie : la changer ne convertit rien. Les montants s’écrivent dans le format des nombres, les dates dans l’ordre de sa région, et les rapports, prévisions, dates de clôture et listes d’activités comptent les jours et les mois dans le fuseau horaire. Les pages du club gardent la monnaie du club définie dans les réglages Hospitality.',
  'regional.currency': 'Monnaie',
  'regional.locale': 'Format des nombres',
  'regional.timeZone': 'Fuseau horaire',
  'regional.csv': 'Exports CSV',
  'regional.csvComma': 'Virgule entre les colonnes (,)',
  'regional.csvSemicolon': 'Point-virgule entre les colonnes (;), comme Excel l’attend en Suisse',
  'regional.csvHint': 'Excel configuré pour la Suisse sépare un fichier CSV aux points-virgules ; avec des virgules, il s’ouvre en une seule colonne. Chaque export de cet espace de travail suit ce choix et commence par la marque qui indique à Excel qu’il est en UTF-8.',
  'regional.preview': 'Les montants s’affichent ainsi : <b>{money}</b>, les dates ainsi : <b>{date}</b>.',
  'regional.previewNow': 'Il est actuellement <b>{now}</b> là-bas.',
  'regional.saved': 'Monnaie, format des nombres, fuseau horaire et exports CSV de l’espace de travail enregistrés.',
  'regional.notSaved': 'Les réglages n’ont pas été enregistrés.',
  'regional.needsManage': 'Pour les modifier, il faut gérer les réglages de l’espace de travail, ce que peuvent faire le propriétaire et les responsables.',
  'regional.locale.de-CH': 'allemand (Suisse)',
  'regional.locale.fr-CH': 'français (Suisse)',
  'regional.locale.it-CH': 'italien (Suisse)',
  'regional.locale.en-CH': 'anglais (Suisse)',
  'regional.locale.de-DE': 'allemand (Allemagne)',
  'regional.locale.de-AT': 'allemand (Autriche)',
  'regional.locale.fr-FR': 'français (France)',
  'regional.locale.it-IT': 'italien (Italie)',
  'regional.locale.en-GB': 'anglais (Royaume-Uni)',
  'regional.locale.en-US': 'anglais (États-Unis)',
};
