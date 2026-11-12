// Français de Suisse romande.
import type { common as en } from '../en/common';
import type { Translation } from '../../catalog';

export const common: Translation<typeof en> = {
  'common.loading': 'Chargement…',
  'common.tryAgain': 'Réessayer',
  'common.checkConnection': 'Veuillez vérifier votre connexion.',
  'common.sending': 'Envoi…',
  'common.saving': 'Enregistrement…',
  'common.save': 'Enregistrer',
  'common.cancel': 'Annuler',
  'common.close': 'Fermer',
  'common.edit': 'Modifier',
  'common.delete': 'Supprimer',
  'common.add': 'Ajouter',
  'common.yes': 'Oui',
  'common.no': 'Non',
  'common.none': 'Aucun',
  'common.or': 'ou',
  'common.search': 'Rechercher',
  'common.export': 'Exporter',
  'common.nothingToExport': 'Rien à exporter',
  'common.exportedRows': { one: '{count} ligne exportée', other: '{count} lignes exportées' },
  'common.couldNotLoad': 'Le chargement a échoué.',
  'common.notSaved': 'Ce n’a pas été enregistré.',

  'language.label': 'Langue',
  'language.automatic': 'Automatique',
  'language.automaticHint': 'Suit la langue de votre espace de travail, sinon celle de votre navigateur.',
  'language.pickerLabel': 'Choisir une langue',
  'language.saved': 'Langue enregistrée.',
  'language.notice': 'Cadence suit désormais la langue de votre espace de travail. Choisissez celle dans laquelle vous souhaitez le lire :',
  'language.noticeDismiss': 'Fermer',
};
