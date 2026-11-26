// Les pages en français, écrites pour la Suisse romande : vouvoiement, facture QR, nLPD.
// Seulement ce que CadenceE fait aujourd’hui : pas de prix, pas d’autre client que le
// Pavillon 46, pas de note ni de récompense.

import { frPricing } from './pricing';
import type { LanguageContent } from './types';
import { withFrenchSpacing } from './typography';

const fr: LanguageContent = {
  template: {
    featuresEyebrow: 'Fonctionnalités',
    stepsEyebrow: 'Comment ça marche',
    faqEyebrow: 'FAQ',
    solutionsEyebrow: 'Solutions',
    stepLabel: 'Étape',
  },
  pages: {
    home: {
      eyebrow: 'CRM suisse',
      h1: 'Le CRM suisse qui gère votre club',
      h1Accent: 'et vos ventes',
      lead: 'CadenceE réunit dans un seul outil tout ce qui fait tourner un club privé, un établissement ou une équipe de vente : prospects et contrats signés en ligne, membres et événements, notes de bar et facturation avec facture QR et TWINT, e-mails et assistant IA. Conçu en Suisse, en français, en allemand et en anglais.',
      heroCardTitle: 'Dans un seul espace de travail',
      heroPoints: [
        'Prospects suivis en cinq phases, jusqu’à l’acompte',
        'Contrats signés en ligne, acompte encaissé à la signature',
        'Membres, foyers, renouvellements et événements',
        'Notes de bar, cotisations, relevés et paiements suisses',
        'E-mails, rapports et assistant IA basé sur Claude',
      ],
      intro: {
        heading: 'Un seul CRM, du premier contact au relevé mensuel',
        paragraphs: [
          'Un club privé, un restaurant ou une équipe commerciale jongle souvent avec cinq outils : un tableur pour les demandes, un logiciel pour les contrats, un autre pour les membres, une caisse pour le bar et une plateforme d’e-mailing qui ne connaît aucun des autres. À chaque passage de l’un à l’autre, un prospect se perd ou un paiement s’oublie.',
          'CadenceE remplace cet assemblage par une seule fiche par personne et par entreprise. La demande arrivée par votre site devient un prospect ; le prospect signe son contrat et paie son acompte ; puis la même fiche devient un membre actif, avec son portail, sa note et son relevé mensuel. Rien n’est saisi deux fois.',
          'CadenceE est utilisé chaque jour au Pavillon 46, club privé à La Croix-sur-Lutry (VD), aux côtés duquel il a été conçu.',
        ],
      },
      showSolutions: {
        heading: 'Ce que CadenceE fait pour vous',
        intro: 'Chaque page détaille un domaine. Choisissez celui qui vous concerne.',
      },
      sections: [
        {
          heading: 'Pensé pour la Suisse',
          paragraphs: [
            'Facture QR et TWINT pour les paiements, export comptable vers Bexio et Abacus, application en français, en allemand et en anglais au choix de chacun, et demandes d’accès ou d’effacement traitées selon la nLPD et le RGPD. Découvrez notre [CRM suisse](page:swiss).',
          ],
          link: 'swiss',
        },
        {
          heading: 'Une IA qui propose, vous décidez',
          paragraphs: [
            'L’[assistant IA](page:ai), basé sur Claude, propose des modifications que vous confirmez, rédige vos e-mails commerciaux et prépare chaque matin le brief de votre journée. Rien n’est enregistré sans l’accord d’une personne.',
          ],
          link: 'ai',
        },
      ],
      features: {
        heading: 'L’essentiel de CadenceE',
        intro: 'De l’accueil à la comptabilité, toute l’équipe travaille sur les mêmes fiches.',
        items: [
          { icon: 'leads', title: 'Prospects en cinq phases', text: 'Prise de contact, rendez-vous, contrat, signature, acompte.' },
          { icon: 'signature', title: 'Signature électronique', text: 'Contrats signés en ligne, sans créer de compte.' },
          { icon: 'members', title: 'Membres et foyers', text: 'Candidatures, renouvellements avec score de risque, adhésions d’entreprise.' },
          { icon: 'reservations', title: 'Événements et réservations', text: 'Listes d’attente, privatisations, visites et pointages.' },
          { icon: 'tabs', title: 'Notes et caisse', text: 'Notes de bar et de restaurant, comptes membres, caisse Lightspeed.' },
          { icon: 'payments', title: 'Paiements suisses', text: 'Carte, TWINT, SEPA et facture QR, avec rapprochement bancaire.' },
          { icon: 'accounting', title: 'Export comptable', text: 'Vers Bexio ou Abacus, depuis un grand livre jamais modifié.' },
          { icon: 'automations', title: 'Automatisation des e-mails', text: 'Séquences, automatisations, newsletters et campagnes.' },
          { icon: 'ai', title: 'Assistant IA', text: 'Basé sur Claude : il propose, rédige et prépare votre brief.' },
        ],
      },
      steps: {
        heading: 'Démarrer avec CadenceE',
        items: [
          { title: 'Créez votre espace', text: 'Inscrivez-vous, nommez votre espace de travail et invitez vos collègues avec le rôle qui leur convient.' },
          { title: 'Importez vos données', text: 'Prospects, contacts et membres depuis un tableur, avec une simulation d’abord et une annulation possible.' },
          { title: 'Branchez vos canaux', text: 'Formulaire de demande ou webhook sur votre site, modèles de contrat et moyens de paiement.' },
          { title: 'Travaillez au même endroit', text: 'Du prospect au membre et du membre au relevé, avec des rapports et un brief chaque matin.' },
        ],
      },
      faq: {
        heading: 'Questions fréquentes sur CadenceE',
        items: [
          { q: 'Qu’est-ce que CadenceE ?', a: 'Un CRM conçu en Suisse qui gère tout un club et ses ventes : prospects, contrats avec signature électronique, membres, événements, facturation, e-mails, rapports et assistant IA, dans un seul outil.' },
          { q: 'À qui s’adresse CadenceE ?', a: 'Aux clubs privés et clubs de membres, aux restaurants, hôtels et lieux d’événements, et aux équipes de vente qui suivent chaque prospect jusqu’à l’acompte. Le Pavillon 46, club privé à La Croix-sur-Lutry, l’utilise chaque jour.' },
          { q: 'CadenceE est-il disponible en français ?', a: 'Oui. L’application existe en français, en allemand et en anglais, et chacun choisit sa langue. Les e-mails préparés existent en français et en anglais.' },
          { q: 'Quels moyens de paiement sont acceptés ?', a: 'Carte, TWINT, SEPA et facture QR. L’acompte peut être encaissé à la signature du contrat, par carte ou par facture QR. Voir [facturation et paiements](page:billing).' },
          { q: 'Peut-on importer nos données existantes ?', a: 'Oui. Prospects, contacts, entreprises et membres s’importent depuis un tableur ; chaque import commence par une simulation et peut être annulé.' },
          { q: 'L’IA modifie-t-elle les données toute seule ?', a: 'Non. L’assistant propose des modifications, et une personne confirme chacune d’elles avant tout enregistrement.' },
        ],
      },
      related: ['private-clubs', 'sales', 'swiss'],
      cta: {
        heading: 'Gérez votre club et vos ventes avec un seul CRM',
        text: 'Créez votre espace de travail, ou demandez une démonstration.',
      },
    },

    features: {
      eyebrow: 'Toutes les fonctionnalités',
      h1: 'Les fonctionnalités du CRM',
      h1Accent: 'pour clubs, hôtellerie et équipes de vente',
      lead: 'CadenceE réunit la vente et la vie du club dans un seul CRM : prospects et contrats, membres et événements, notes et facturation, e-mails et rapports, avec un assistant IA qui n’agit qu’avec votre accord. Voici tout ce qu’il fait, domaine par domaine.',
      heroCardTitle: 'Dans un seul espace',
      heroPoints: [
        'Prospects suivis en cinq phases, jusqu’à l’acompte',
        'Contrats signés en ligne, acompte à la signature',
        'Membres, foyers, renouvellements et événements',
        'Notes, cotisations, relevés et paiements suisses',
        'E-mails, rapports et assistant IA basé sur Claude',
      ],
      intro: {
        heading: 'Un CRM au lieu de cinq outils',
        paragraphs: [
          'La plupart des clubs et des établissements travaillent avec un patchwork : un tableur pour les demandes, un outil pour les contrats, un autre pour les membres, une caisse pour le bar et une plateforme d’e-mailing qui ignore tout le reste. Chaque transmission entre eux est une occasion d’oublier un prospect ou un paiement.',
          'CadenceE remplace ce patchwork par une fiche unique par personne et par entreprise. La demande reçue par votre site devient un prospect, le prospect signe et paie son acompte, et la même fiche devient un membre actif, avec portail, note et relevé mensuel.',
          'Un espace de travail peut utiliser la partie vente, la partie club ou les deux, et l’application entière existe en français, en allemand et en anglais. Chaque domaine ci-dessous renvoie à une page qui le détaille.',
        ],
      },
      sections: [
        {
          heading: 'Prospects et ventes',
          paragraphs: [
            'Le [suivi des prospects en cinq phases](page:sales) accompagne chaque opportunité de la prise de contact au rendez-vous, au contrat, à la signature et à l’acompte : l’équipe sait toujours quelle est la prochaine étape. Les prospects reçoivent un score, sont attribués, protégés contre les doublons, et gardent la trace de qui les a recommandés ou de quel partenaire ils viennent.',
          ],
          bullets: [
            'Score et règles d’attribution',
            'Protection contre les doublons à la saisie et à l’import',
            'Imports avec simulation et annulation',
            'Formulaires de demande publics et webhook pour votre site',
          ],
          link: 'sales',
        },
        {
          heading: 'Contrats, signature électronique et acomptes',
          paragraphs: [
            'Les contrats sont générés depuis vos modèles et envoyés en [signature électronique](page:e-signature). Le signataire n’a pas besoin de compte. L’acompte est encaissé à la signature, par carte ou facture QR, et un contrat signé sur papier peut simplement être téléversé.',
          ],
          bullets: [
            'Modèles de contrat remplis depuis la fiche',
            'Signature sans compte pour le signataire',
            'Acompte à la signature, carte ou facture QR',
            'Téléversement des contrats signés sur papier',
          ],
          link: 'e-signature',
        },
        {
          heading: 'Membres et adhésions',
          paragraphs: [
            'Une fois le contrat signé, le prospect devient membre actif en une étape, avec un e-mail de bienvenue et l’accès au portail membre. La [gestion des membres](page:membership) couvre les membres, les foyers, les candidatures, les renouvellements avec score de risque et les adhésions d’entreprise.',
          ],
          link: 'membership',
        },
        {
          heading: 'Événements, réservations et hôtellerie-restauration',
          paragraphs: [
            'Événements et réservations avec liste d’attente, privatisations, visites et pointages, notes de bar et de restaurant, et connexion à la caisse Lightspeed : la partie [hôtellerie-restauration](page:hospitality) d’un club ou d’un établissement vit dans le même CRM que ses membres et ses clients.',
          ],
          link: 'hospitality',
        },
        {
          heading: 'Facturation et paiements',
          paragraphs: [
            'Comptes membres, cotisations récurrentes et relevés PDF mensuels, payés par carte, TWINT, SEPA ou facture QR. Les fichiers bancaires sont rapprochés des montants dus, et l’export comptable part vers Bexio ou Abacus depuis un grand livre qu’on contre-passe mais qu’on ne modifie jamais. Voir [facturation et paiements](page:billing).',
          ],
          bullets: [
            'Cotisations récurrentes et comptes membres',
            'Relevés PDF mensuels',
            'Carte, TWINT, SEPA et facture QR',
            'Export vers Bexio et Abacus',
          ],
          link: 'billing',
        },
        {
          heading: 'E-mails et prospection',
          paragraphs: [
            'Une seule boîte d’envoi expédie tous les e-mails, réessaie quand un serveur refuse et gère les rebonds. Séquences et automatisations relancent à votre place, les newsletters peuvent inviter les prospects à réserver un rendez-vous, et les campagnes respectent le consentement avec un lien de désinscription en un clic. En savoir plus sur l’[automatisation des e-mails](page:email).',
          ],
          link: 'email',
        },
        {
          heading: 'Assistant IA',
          paragraphs: [
            'L’[assistant IA](page:ai), basé sur Claude, lit votre espace de travail et propose des modifications qu’une personne confirme. Le rédacteur commercial IA écrit vos e-mails dans votre ton, et le brief du matin liste ce qui demande votre attention aujourd’hui.',
          ],
          link: 'ai',
        },
        {
          heading: 'Rapports, protection des données et plateforme',
          paragraphs: [
            'Rapports et prévisions, journal d’audit de chaque modification, fusion des doublons, et demandes d’accès ou d’effacement selon le RGPD et la [nLPD suisse](page:swiss). Connexion avec Google, Microsoft ou une clé d’accès (passkey), double authentification, rôles et autorisations pour chacun, plusieurs espaces de travail, et une page État du système qui surveille que tout fonctionne.',
          ],
          bullets: [
            'Rapports, prévisions et journal d’audit',
            'Demandes RGPD et nLPD',
            'Connexion Google, Microsoft ou passkey',
            'Rôles, autorisations et plusieurs espaces',
          ],
          link: 'swiss',
        },
      ],
      features: {
        heading: 'Conçu pour toute l’équipe',
        intro: 'De l’accueil au service financier, chacun travaille sur les mêmes fiches.',
        items: [
          { icon: 'leads', title: 'Prospects en cinq phases', text: 'Chaque prospect a une phase et une prochaine étape.' },
          { icon: 'signature', title: 'Contrats signés en ligne', text: 'Depuis vos modèles, sans compte pour le signataire.' },
          { icon: 'members', title: 'Membres et foyers', text: 'Candidatures, renouvellements, adhésions d’entreprise.' },
          { icon: 'reservations', title: 'Événements et réservations', text: 'Listes d’attente, privatisations, visites et pointages.' },
          { icon: 'tabs', title: 'Notes et caisse', text: 'Notes de bar, comptes membres, caisse Lightspeed.' },
          { icon: 'payments', title: 'Paiements suisses', text: 'Carte, TWINT, SEPA et facture QR, rapprochement bancaire.' },
          { icon: 'accounting', title: 'Export comptable', text: 'Vers Bexio ou Abacus, depuis un grand livre jamais modifié.' },
          { icon: 'automations', title: 'Automatisation des e-mails', text: 'Séquences, automatisations, newsletters et campagnes.' },
          { icon: 'ai', title: 'Assistant IA', text: 'Basé sur Claude : il propose, rédige et prépare le brief.' },
          { icon: 'loyalty', title: 'Fidélité, cartes cadeaux, dons', text: 'Récompensez les habitués, vendez des cartes cadeaux, enregistrez les dons.' },
          { icon: 'roles', title: 'Rôles et autorisations', text: 'Chacun voit et modifie ce que son rôle permet.' },
          { icon: 'languages', title: 'Français, allemand, anglais', text: 'Toute l’application en trois langues, au choix de chacun.' },
        ],
      },
      steps: {
        heading: 'Mettre une équipe en route sur CadenceE',
        items: [
          { title: 'Créez votre espace', text: 'Inscrivez-vous, nommez l’espace de travail et invitez vos collègues avec leur rôle.' },
          { title: 'Importez vos données', text: 'Prospects, contacts et membres depuis un tableur : simulation d’abord, annulation possible ensuite.' },
          { title: 'Branchez vos canaux', text: 'Formulaire ou webhook sur votre site, modèles de contrat et moyens de paiement.' },
          { title: 'Travaillez au même endroit', text: 'Du prospect à la signature, du membre au relevé, avec rapports et brief du matin.' },
        ],
      },
      faq: {
        heading: 'Questions sur les fonctionnalités',
        items: [
          { q: 'CadenceE est-il un CRM commercial ou un logiciel de gestion de club ?', a: 'Les deux. Un espace de travail peut utiliser la partie vente (prospects, affaires, contrats), la partie club (membres, événements, notes, facturation) ou les deux ensemble. Ainsi, un prospect devient membre sans changer de logiciel.' },
          { q: 'Dans quelles langues CadenceE est-il disponible ?', a: 'En français, en allemand et en anglais, au choix de chaque personne. Les e-mails préparés existent en français et en anglais.' },
          { q: 'Peut-on importer nos données existantes ?', a: 'Oui. Prospects, contacts, entreprises et membres s’importent depuis un tableur. Chaque import commence par une simulation qui montre le résultat, et un import peut être annulé.' },
          { q: 'CadenceE gère-t-il les paiements ?', a: 'Oui : carte, TWINT, SEPA et facture QR, acompte à la signature du contrat, et rapprochement des fichiers bancaires avec les montants dus. Voir [facturation et paiements](page:billing).' },
          { q: 'L’IA modifie-t-elle les données toute seule ?', a: 'Non. L’[assistant IA](page:ai) propose des modifications, et une personne confirme chacune avant tout enregistrement.' },
          { q: 'Comment l’accès est-il contrôlé ?', a: 'Connexion par e-mail et mot de passe, Google, Microsoft ou clé d’accès, double authentification possible, rôles et autorisations pour chacun, et un journal d’audit de chaque modification.' },
        ],
      },
      related: ['private-clubs', 'sales', 'swiss'],
      cta: {
        heading: 'Découvrez CadenceE avec vos propres données',
        text: 'Créez un espace de travail et importez une première liste, ou demandez une démonstration.',
      },
    },

    'private-clubs': {
      eyebrow: 'CRM pour club privé',
      h1: 'Le CRM conçu pour les',
      h1Accent: 'clubs privés et clubs de membres',
      lead: 'De la première demande au relevé mensuel, CadenceE gère un club privé au même endroit : candidatures, contrats et acomptes, membres et foyers, événements, notes de bar et renouvellements. Conçu en Suisse, avec un club.',
      heroCardTitle: 'Ce qu’un club gère dans CadenceE',
      heroPoints: [
        'Demandes et candidatures depuis votre site',
        'Contrats d’adhésion signés en ligne, avec acompte',
        'Membres, foyers et adhésions d’entreprise',
        'Événements, réservations et privatisations',
        'Notes de bar, cotisations et relevés mensuels',
      ],
      intro: {
        heading: 'Pourquoi un club privé a besoin de son propre CRM',
        paragraphs: [
          'Un club privé ne vend pas un produit, mais une place dans une communauté. Le chemin vers l’adhésion est long et personnel : on entend parler du club, on demande une visite, on rencontre l’équipe, on pose sa candidature, on signe, on paie l’acompte d’entrée, puis on revient pour les dîners, les événements et le bar.',
          'Les CRM génériques s’arrêtent à la vente signée, et les logiciels de club commencent souvent au membre. CadenceE couvre tout le parcours sur une seule fiche. Il a été conçu aux côtés du Pavillon 46, club privé à La Croix-sur-Lutry (VD), qui l’utilise chaque jour pour vendre ses adhésions et faire vivre le club.',
        ],
      },
      sections: [
        {
          heading: 'De la demande au membre, sur une seule fiche',
          paragraphs: [
            'Une demande arrive par le formulaire ou le webhook de votre site et devient un prospect. Le [suivi en cinq phases](page:sales) le mène de la prise de contact au rendez-vous, au contrat, à la signature et à l’acompte, et garde la trace de la personne qui l’a recommandé : les parrainages de vos membres ne se perdent plus.',
            'Une fois le contrat signé et l’acompte payé, une seule étape fait du prospect un membre actif. L’e-mail de bienvenue part et le portail membre s’ouvre, sans rien ressaisir.',
          ],
          bullets: [
            'Formulaires publics de demande et de candidature',
            'Parrain et partenaire sur chaque prospect',
            'Contrats d’adhésion depuis vos modèles, [signés en ligne](page:e-signature)',
            'Conversion en membre actif en une étape',
          ],
        },
        {
          heading: 'Adhésions, foyers et renouvellements',
          paragraphs: [
            'Les membres sont regroupés en foyers, et une entreprise peut détenir une adhésion pour plusieurs de ses collaborateurs. Les candidatures se traitent dans une seule liste. Chaque renouvellement porte un score de risque : l’équipe appelle les membres qui s’éloignent avant qu’ils ne partent. En savoir plus sur la [gestion des membres](page:membership).',
          ],
          bullets: [
            'Foyers et adhésions d’entreprise',
            'Candidatures traitées au même endroit',
            'Renouvellements avec score de risque',
            'Un portail pour chaque membre',
          ],
        },
        {
          heading: 'La vie du club : événements, visites et notes',
          paragraphs: [
            'Les événements et réservations ont une liste d’attente, et les salons peuvent être privatisés pour les occasions des membres. Les visites et pointages montrent qui vient et à quelle fréquence. Les consommations au bar et au restaurant vont sur la note, avec la connexion à la caisse Lightspeed, puis sur le compte du membre.',
            'En fin de mois, chaque membre reçoit un relevé PDF et paie par carte, TWINT, SEPA ou facture QR. Voir [facturation et paiements](page:billing).',
          ],
        },
        {
          heading: 'La discrétion, d’office',
          paragraphs: [
            'La liste des membres est le bien le plus sensible d’un club. CadenceE donne à chaque collaborateur un rôle avec les seules autorisations nécessaires, inscrit chaque modification au journal d’audit, et accepte la connexion par clé d’accès et la double authentification. Les demandes d’accès et d’effacement sont traitées selon le RGPD et la nLPD.',
          ],
        },
      ],
      features: {
        heading: 'Tout ce dont un club privé a besoin',
        items: [
          { icon: 'applications', title: 'Candidatures', text: 'Recevez les candidatures par un formulaire public et décidez dans une seule liste.' },
          { icon: 'contract', title: 'Contrats d’adhésion', text: 'Générés depuis vos modèles, envoyés en signature électronique.' },
          { icon: 'deposit', title: 'Acompte d’entrée', text: 'Encaissé à la signature, par carte ou facture QR.' },
          { icon: 'households', title: 'Foyers', text: 'Conjoints et enfants regroupés sous un même foyer.' },
          { icon: 'corporate', title: 'Adhésions d’entreprise', text: 'Une entreprise détient l’adhésion de plusieurs collaborateurs.' },
          { icon: 'renewals', title: 'Renouvellements à risque', text: 'Voyez qui risque de ne pas renouveler, tant qu’il est temps d’agir.' },
          { icon: 'events', title: 'Événements et listes d’attente', text: 'Publiez, réservez et remplissez les places libérées.' },
          { icon: 'private-event', title: 'Privatisations', text: 'Les événements privés des membres, de la demande à la facture.' },
          { icon: 'tabs', title: 'Notes et comptes membres', text: 'Les notes de bar et de restaurant passent sur le compte du membre.' },
          { icon: 'statements', title: 'Relevés mensuels', text: 'Un relevé PDF pour chaque membre, chaque mois.' },
          { icon: 'loyalty', title: 'Fidélité, cartes cadeaux, dons', text: 'Récompensez la fidélité, vendez des cartes cadeaux, enregistrez les dons.' },
          { icon: 'portal', title: 'Portail membre', text: 'Les membres se connectent avec un lien reçu par e-mail.' },
        ],
      },
      steps: {
        heading: 'Comment un club passe à CadenceE',
        items: [
          { title: 'Préparez le club', text: 'Créez l’espace de travail, ajoutez vos modèles de contrat et vos moyens de paiement, invitez l’équipe.' },
          { title: 'Importez membres et prospects', text: 'Reprenez vos membres et vos demandes en cours depuis un tableur, avec une simulation d’abord.' },
          { title: 'Ouvrez les portes en ligne', text: 'Placez le formulaire de demande ou de candidature sur votre site : l’intérêt arrive directement dans le CRM.' },
          { title: 'Faites vivre la saison', text: 'Suivez les prospects jusqu’à la signature, accueillez les nouveaux membres, organisez les événements, envoyez les relevés.' },
        ],
      },
      faq: {
        heading: 'CRM pour club privé : questions fréquentes',
        items: [
          { q: 'Qui utilise CadenceE aujourd’hui ?', a: 'Le Pavillon 46, club privé à La Croix-sur-Lutry (VD), utilise CadenceE chaque jour pour vendre ses adhésions et gérer le club. CadenceE a été conçu à ses côtés.' },
          { q: 'Les futurs membres peuvent-ils postuler en ligne ?', a: 'Oui. Le club publie un formulaire de candidature et un formulaire de demande ; les envois arrivent comme candidatures ou prospects, protégés contre les doublons, et l’équipe décide depuis une seule liste.' },
          { q: 'Comment un prospect devient-il membre ?', a: 'Il passe par la prise de contact, le rendez-vous, le contrat, la signature et l’acompte. Une fois le contrat signé et l’acompte payé, une étape suffit pour en faire un membre actif, envoyer l’e-mail de bienvenue et ouvrir le portail membre.' },
          { q: 'Peut-on facturer le bar et le restaurant aux membres ?', a: 'Oui. Les consommations vont sur la note, avec la connexion à la caisse Lightspeed, puis sur le compte du membre. Chaque mois, le membre reçoit un relevé PDF et paie par carte, TWINT, SEPA ou facture QR.' },
          { q: 'Les données des membres restent-elles confidentielles ?', a: 'Chaque collaborateur a un rôle et des autorisations précises, chaque modification est inscrite au journal d’audit, et la connexion accepte les clés d’accès et la double authentification. Les demandes d’accès et d’effacement suivent le RGPD et la nLPD.' },
          { q: 'CadenceE fonctionne-t-il en français et en allemand ?', a: 'Oui. Toute l’application existe en français, en allemand et en anglais, et chacun choisit sa langue. Voir notre [CRM suisse](page:swiss).' },
        ],
      },
      related: ['membership', 'billing', 'hospitality'],
      cta: {
        heading: 'Gérez votre club avec un seul CRM',
        text: 'Créez l’espace de travail de votre club, ou demandez une démonstration à l’équipe qui l’a conçu.',
      },
    },

    membership: {
      eyebrow: 'Logiciel de gestion des membres',
      h1: 'Le logiciel de gestion des membres',
      h1Accent: 'pensé pour les clubs',
      lead: 'Tous vos membres, foyers et adhésions d’entreprise au même endroit, les renouvellements anticipés grâce à un score de risque, et un portail pour chaque membre. CadenceE gère l’adhésion et l’argent qui va avec.',
      heroCardTitle: 'Les membres dans CadenceE',
      heroPoints: [
        'Membres, foyers et adhésions d’entreprise',
        'Candidatures traitées dans une seule liste',
        'Renouvellements avec score de risque',
        'Portail membre avec connexion par lien e-mail',
        'Cotisations récurrentes et relevés mensuels',
      ],
      intro: {
        heading: 'Un membre, c’est une relation, pas une ligne de tableur',
        paragraphs: [
          'Un tableur sait lister des membres. Il ne vous dit pas que l’adhésion d’une famille se renouvelle le mois prochain, que l’un d’eux n’est plus venu depuis le printemps, ou que son dernier relevé est encore ouvert. Un logiciel de gestion des membres, lui, devrait le faire.',
          'CadenceE relie la fiche du membre à tout ce qui l’entoure : son foyer, l’entreprise qui paie une adhésion collective, les événements réservés, les visites, la note et les cotisations. L’équipe voit l’ensemble, et chaque membre voit le sien dans son portail.',
        ],
      },
      sections: [
        {
          heading: 'Membres, foyers et adhésions d’entreprise',
          paragraphs: [
            'Chaque membre a une fiche unique avec ses coordonnées, son adhésion, sa langue et son historique. Conjoints et enfants se regroupent en foyer, pour que la communication et la facturation suivent la famille plutôt que la personne. Une entreprise peut détenir une adhésion qui couvre plusieurs de ses collaborateurs.',
          ],
          bullets: [
            'Une fiche par membre, avec tout l’historique',
            'Foyers pour les familles et les couples',
            'Adhésions d’entreprise',
            'Protection et fusion des doublons',
          ],
        },
        {
          heading: 'Candidatures et nouveaux membres',
          paragraphs: [
            'Les futurs membres postulent via un formulaire de candidature public sur votre site. Les candidatures arrivent dans une seule liste, où le comité ou l’équipe décide. Quand l’adhésion se vend par le [suivi commercial](page:sales), un contrat signé et un acompte payé font du prospect un membre actif en une étape, avec un e-mail de bienvenue.',
          ],
        },
        {
          heading: 'Des renouvellements avant les résiliations',
          paragraphs: [
            'Chaque renouvellement a un score de risque : vous savez quels membres risquent de partir tant qu’il est encore temps de les appeler. Renouvellements, visites et consommations sont côte à côte, ce qui facilite la conversation : vous savez quand ils sont venus pour la dernière fois et ce qu’ils ont apprécié.',
          ],
        },
        {
          heading: 'Cotisations, relevés et portail membre',
          paragraphs: [
            'Les cotisations récurrentes sont facturées à échéance, avec les notes et les autres frais, et chaque membre reçoit un relevé PDF mensuel. Les membres paient par carte, TWINT, SEPA ou facture QR (voir [facturation et paiements](page:billing)). Dans le portail membre, ils se connectent avec un lien reçu par e-mail, sans mot de passe à retenir.',
          ],
        },
      ],
      features: {
        heading: 'Les outils de gestion des membres',
        items: [
          { icon: 'members', title: 'Fiches membres', text: 'Coordonnées, adhésion, langue et historique au même endroit.' },
          { icon: 'households', title: 'Foyers', text: 'La famille regroupée pour la communication et la facturation.' },
          { icon: 'corporate', title: 'Adhésions d’entreprise', text: 'Plusieurs collaborateurs sous une même entreprise.' },
          { icon: 'applications', title: 'Candidatures en ligne', text: 'Un formulaire public alimente une liste de décision.' },
          { icon: 'renewals', title: 'Renouvellements à risque', text: 'Un score signale les membres qui risquent de partir.' },
          { icon: 'portal', title: 'Portail membre', text: 'Connexion par lien e-mail, sans mot de passe.' },
          { icon: 'dues', title: 'Cotisations récurrentes', text: 'Facturées à échéance, avec les notes et les frais.' },
          { icon: 'statements', title: 'Relevés PDF', text: 'Un relevé mensuel pour chaque membre.' },
          { icon: 'visits', title: 'Visites et pointages', text: 'Qui vient, et à quelle fréquence.' },
          { icon: 'events', title: 'Événements et réservations', text: 'Les membres réservent, avec liste d’attente si c’est complet.' },
          { icon: 'newsletter', title: 'Communication aux membres', text: 'Newsletters et campagnes avec consentement et désinscription en un clic.' },
          { icon: 'privacy', title: 'Demandes de protection des données', text: 'Accès et effacement selon le RGPD et la nLPD.' },
        ],
      },
      steps: {
        heading: 'Du tableur au logiciel de gestion des membres',
        items: [
          { title: 'Importez vos membres', text: 'Téléversez votre liste : une simulation montre ce qui sera créé avant tout enregistrement, et l’import peut être annulé.' },
          { title: 'Regroupez et classez', text: 'Créez les foyers, reliez les adhésions d’entreprise et définissez les cotisations récurrentes.' },
          { title: 'Ouvrez le portail', text: 'Invitez les membres au portail, où ils se connectent avec un lien reçu par e-mail.' },
          { title: 'Suivez les renouvellements', text: 'Classez-les par score de risque et parlez aux membres avant qu’ils ne décident de partir.' },
        ],
      },
      faq: {
        heading: 'Gestion des membres : questions fréquentes',
        items: [
          { q: 'Qu’est-ce qu’un logiciel de gestion des membres ?', a: 'Un logiciel qui réunit les membres d’un club, leurs adhésions, leurs renouvellements et leurs paiements. CadenceE y ajoute en amont un suivi commercial, pour accompagner les futurs membres dès la première demande. Voir [CRM pour club privé](page:private-clubs).' },
          { q: 'Une famille peut-elle partager une adhésion ?', a: 'Les membres peuvent être regroupés en foyer : conjoints et enfants sont traités ensemble pour la communication et la facturation.' },
          { q: 'Gérez-vous les adhésions d’entreprise ?', a: 'Oui. Une entreprise peut détenir une adhésion qui couvre plusieurs collaborateurs, chacun avec sa propre fiche membre.' },
          { q: 'À quoi sert le score de risque des renouvellements ?', a: 'Il indique la probabilité qu’un membre ne renouvelle pas, pour que l’équipe concentre ses appels et son attention sur ces membres avant l’échéance.' },
          { q: 'Les membres ont-ils besoin d’un mot de passe pour le portail ?', a: 'Non. Ils se connectent au portail membre avec un lien envoyé par e-mail.' },
          { q: 'Peut-on importer notre liste de membres actuelle ?', a: 'Oui. L’import commence par une simulation qui montre le résultat, et il peut être annulé en cas d’erreur.' },
        ],
      },
      related: ['private-clubs', 'billing', 'email'],
      cta: {
        heading: 'Offrez à vos membres l’attention qu’ils attendent',
        text: 'Créez un espace de travail et importez votre liste de membres, ou demandez une démonstration.',
      },
    },

    hospitality: {
      eyebrow: 'CRM hôtellerie-restauration',
      h1: 'Le CRM de l’hôtellerie et de la restauration',
      h1Accent: 'pour restaurants, hôtels et lieux d’événements',
      lead: 'CadenceE relie le client, la réservation et l’addition : réservations avec liste d’attente, privatisations, notes de bar et de restaurant connectées à la caisse Lightspeed, et e-mails aux clients, dans un seul CRM conçu en Suisse.',
      heroCardTitle: 'Pour les équipes d’accueil',
      heroPoints: [
        'Réservations et événements avec liste d’attente',
        'Privatisations, de la demande à l’acompte',
        'Notes de bar et de restaurant, caisse Lightspeed',
        'Comptes clients et relevés mensuels',
        'Newsletters aux clients, avec consentement',
      ],
      intro: {
        heading: 'Connaître vos clients, pas seulement vos couverts',
        paragraphs: [
          'Dans l’hôtellerie-restauration, les meilleurs clients reviennent. Mais ce que vous savez d’eux est éparpillé : la réservation dans un système, l’addition dans la caisse, la demande d’événement dans une boîte mail et la liste de la newsletter ailleurs encore.',
          'CadenceE rassemble tout autour du client. Les réservations, les événements privés, les consommations et les e-mails d’un habitué tiennent sur une seule fiche : l’équipe le reconnaît, l’invite à revenir et le facture correctement. C’est le CRM du Pavillon 46, club avec restaurant et bar à La Croix-sur-Lutry (VD).',
        ],
      },
      sections: [
        {
          heading: 'Réservations, événements et listes d’attente',
          paragraphs: [
            'Publiez vos événements, prenez les réservations et gardez une liste d’attente quand c’est complet : une annulation devient une réservation plutôt qu’une place vide. Les visites et pointages enregistrent qui est vraiment venu, et les rapports d’hôtellerie s’affichent à côté des chiffres de vente et d’adhésion.',
          ],
        },
        {
          heading: 'Privatisations, de la demande à l’acompte',
          paragraphs: [
            'Dîners privés, apéritifs et événements d’entreprise commencent par une demande. CadenceE les suit comme toute opportunité : le [suivi en cinq phases](page:sales) mène la demande au rendez-vous, au contrat, à la signature et à l’acompte, et le contrat se signe en ligne grâce à la [signature électronique](page:e-signature).',
          ],
          bullets: [
            'Demandes d’événement depuis votre site',
            'Contrats depuis vos modèles',
            'Acompte à la signature, carte ou facture QR',
            'L’événement sur la fiche du client',
          ],
        },
        {
          heading: 'Notes, caisse et comptes clients',
          paragraphs: [
            'Les consommations au bar et au restaurant vont sur la note. La connexion à la caisse Lightspeed fait remonter les ventes dans le CRM, et les habitués ou les membres peuvent avoir un compte réglé chaque mois sur relevé PDF. Les paiements arrivent par carte, TWINT, SEPA ou facture QR ; voir [facturation et paiements](page:billing).',
          ],
        },
        {
          heading: 'Ce que CadenceE n’est pas',
          paragraphs: [
            'CadenceE est un CRM, pas un logiciel de gestion hôtelière (PMS) : il ne gère ni l’inventaire des chambres ni les réservations des plateformes. Il travaille avec votre caisse grâce à la connexion Lightspeed au lieu de la remplacer. Son rôle, c’est la relation autour du séjour ou du repas : la demande, l’événement, le compte et le suivi.',
          ],
        },
      ],
      features: {
        heading: 'Fonctionnalités pour l’hôtellerie-restauration',
        items: [
          { icon: 'reservations', title: 'Réservations', text: 'Pour les événements et les dîners, avec liste d’attente.' },
          { icon: 'private-event', title: 'Privatisations', text: 'Demande, contrat, acompte et facture des événements privés.' },
          { icon: 'tabs', title: 'Notes de bar et de restaurant', text: 'Les consommations enregistrées par client ou membre.' },
          { icon: 'pos', title: 'Caisse Lightspeed', text: 'Les ventes de la caisse remontent dans le CRM.' },
          { icon: 'house-account', title: 'Comptes clients', text: 'Les habitués règlent une fois par mois.' },
          { icon: 'statements', title: 'Relevés PDF mensuels', text: 'Un relevé clair pour chaque compte.' },
          { icon: 'visits', title: 'Visites et pointages', text: 'Qui est venu, quand, et combien de fois.' },
          { icon: 'loyalty', title: 'Fidélité', text: 'Récompensez les clients qui reviennent.' },
          { icon: 'gift-card', title: 'Cartes cadeaux', text: 'Vendez des cartes cadeaux et suivez leur solde.' },
          { icon: 'newsletter', title: 'Newsletters clients', text: 'Nouvelles et invitations, avec consentement et désinscription en un clic.' },
          { icon: 'payments', title: 'Paiements suisses', text: 'Carte, TWINT, SEPA et facture QR.' },
          { icon: 'reports', title: 'Rapports', text: 'Les rapports d’hôtellerie à côté des ventes et des adhésions.' },
        ],
      },
      steps: {
        heading: 'Démarrer CadenceE dans votre établissement',
        items: [
          { title: 'Préparez l’établissement', text: 'Créez l’espace de travail, ajoutez vos types d’événement et vos moyens de paiement, connectez Lightspeed.' },
          { title: 'Reprenez vos clients', text: 'Importez clients et habitués depuis un tableur, avec simulation et annulation.' },
          { title: 'Recevez les demandes en ligne', text: 'Placez le formulaire sur votre site : les demandes de privatisation arrivent comme prospects.' },
          { title: 'Servez, facturez, relancez', text: 'Réservations et notes, relevés mensuels, et newsletters pour faire revenir vos clients.' },
        ],
      },
      faq: {
        heading: 'CRM hôtellerie-restauration : questions fréquentes',
        items: [
          { q: 'CadenceE est-il un logiciel de réservation pour restaurant ?', a: 'CadenceE prend les réservations pour les événements et les dîners, avec liste d’attente, à côté de l’historique et de l’addition du client. C’est d’abord un CRM, construit autour de la relation client.' },
          { q: 'CadenceE remplace-t-il notre caisse ?', a: 'Non. CadenceE se connecte à la caisse Lightspeed pour que les ventes remontent dans le CRM, et gère les notes et les comptes clients ; la caisse reste votre caisse.' },
          { q: 'Peut-il gérer les chambres d’un hôtel ?', a: 'Non. CadenceE n’est pas un PMS et ne gère pas l’inventaire des chambres. Les hôtels l’utilisent pour les événements privés, les clients entreprises, la relation client et la facturation des comptes.' },
          { q: 'Comment les privatisations sont-elles gérées ?', a: 'Une demande d’événement devient un prospect et suit les cinq phases : prise de contact, rendez-vous, contrat, signature et acompte. Le contrat se signe en ligne et l’acompte est encaissé à la signature.' },
          { q: 'Quels moyens de paiement les clients peuvent-ils utiliser ?', a: 'Carte, TWINT, SEPA et facture QR.' },
          { q: 'Peut-on envoyer des e-mails à nos clients ?', a: 'Oui. Newsletters et campagnes partent d’une seule boîte d’envoi, avec consentement et désinscription en un clic. Voir l’[automatisation des e-mails](page:email).' },
        ],
      },
      related: ['private-clubs', 'e-signature', 'billing'],
      cta: {
        heading: 'Réunissez vos clients, vos événements et vos additions',
        text: 'Créez l’espace de travail de votre établissement, ou demandez une démonstration.',
      },
    },

    sales: {
      eyebrow: 'CRM commercial',
      h1: 'Le CRM commercial qui suit chaque prospect',
      h1Accent: 'jusqu’à l’acompte',
      lead: 'CadenceE suit chaque prospect en cinq phases (prise de contact, rendez-vous, contrat, signature et acompte), avec score, attribution et protection contre les doublons, un tableau des affaires et un rédacteur IA pour les e-mails entre deux étapes.',
      heroCardTitle: 'Les cinq phases',
      heroPoints: [
        'Prise de contact : premier échange et relances',
        'Rendez-vous : visite, appel ou démonstration',
        'Contrat : généré depuis votre modèle',
        'Signature : en ligne, sans compte',
        'Acompte : payé à la signature, carte ou facture QR',
      ],
      intro: {
        heading: 'Un pipeline qui se termine par un encaissement',
        paragraphs: [
          'Beaucoup de pipelines s’arrêtent à « gagné ». En réalité, une vente n’est terminée que lorsque le contrat est signé et l’acompte arrivé. Le suivi des prospects de CadenceE part de cette réalité : cinq phases, chacune avec une prochaine étape claire, jusqu’à l’acompte.',
          'Comme les contrats, la signature électronique et les paiements vivent dans le même CRM, le pipeline avance quand le client signe et paie. L’équipe consacre son temps aux conversations plutôt qu’à la mise à jour d’un tableur.',
        ],
      },
      sections: [
        {
          heading: 'Cinq phases, une prochaine étape à la fois',
          paragraphs: [
            'Prise de contact, rendez-vous, contrat, signature, acompte. Chaque prospect se trouve dans une phase, avec ses tâches et ses activités, si bien que n’importe qui dans l’équipe peut le reprendre. Le tableau des affaires présente les opportunités en colonnes qu’on fait glisser, et chaque collègue garde ses vues enregistrées à portée de clic.',
          ],
          bullets: [
            'Prospects et affaires sur un tableau',
            'Tâches et activités sur chaque prospect',
            'Vues enregistrées pour chacun',
            'Calendrier des rendez-vous et relances',
          ],
        },
        {
          heading: 'Score, attribution et données propres',
          paragraphs: [
            'Les prospects reçoivent un score pour traiter d’abord les plus prometteurs, et des règles d’attribution envoient chaque nouveau prospect à la bonne personne. La protection contre les doublons repère la même personne ou la même entreprise arrivée deux fois, et les doublons peuvent être fusionnés. Chaque prospect garde la trace de qui l’a recommandé et par quel partenaire il est venu.',
          ],
        },
        {
          heading: 'Des prospects qui arrivent tout seuls',
          paragraphs: [
            'Des formulaires de demande publics et un webhook pour votre site créent les prospects directement. Les listes existantes s’importent depuis un tableur : une simulation montre d’abord le résultat, et l’import peut être annulé.',
          ],
        },
        {
          heading: 'Le prochain e-mail, rédigé en quelques secondes',
          paragraphs: [
            'Le rédacteur commercial IA rédige e-mails, relances et notes dans votre ton. Les séquences envoient les relances selon un calendrier, et les newsletters peuvent inviter les prospects à réserver un rendez-vous sur une page de réservation. Voir l’[automatisation des e-mails](page:email) et l’[assistant IA](page:ai).',
          ],
        },
      ],
      features: {
        heading: 'Fonctionnalités commerciales',
        items: [
          { icon: 'leads', title: 'Suivi en cinq phases', text: 'Prise de contact, rendez-vous, contrat, signature, acompte.' },
          { icon: 'deals', title: 'Tableau des affaires', text: 'Faites glisser les opportunités d’une étape à l’autre.' },
          { icon: 'score', title: 'Score des prospects', text: 'Concentrez-vous sur ceux qui ont le plus de chances de signer.' },
          { icon: 'partner', title: 'Recommandations et partenaires', text: 'Qui a recommandé chaque prospect, et par quel partenaire.' },
          { icon: 'duplicates', title: 'Protection contre les doublons', text: 'Un prospect n’est pas saisi deux fois, et les doublons se fusionnent.' },
          { icon: 'import', title: 'Imports avec simulation', text: 'Reprenez vos listes sans risque, avec annulation possible.' },
          { icon: 'forms', title: 'Formulaires de demande', text: 'Des formulaires publics qui créent des prospects.' },
          { icon: 'webhook', title: 'Webhook pour votre site', text: 'Envoyez des prospects depuis votre site ou un autre outil.' },
          { icon: 'signature', title: 'Signature électronique', text: 'Contrats signés en ligne, sans compte.' },
          { icon: 'deposit', title: 'Acompte à la signature', text: 'Par carte ou facture QR.' },
          { icon: 'writer', title: 'Rédacteur commercial IA', text: 'E-mails et relances rédigés dans votre ton.' },
          { icon: 'forecast', title: 'Rapports et prévisions', text: 'Ce qui va probablement signer, et quand.' },
        ],
      },
      steps: {
        heading: 'Comment travaille une équipe de vente dans CadenceE',
        items: [
          { title: 'Capter', text: 'Les prospects arrivent des formulaires, du webhook, des imports ou de l’équipe elle-même.' },
          { title: 'Qualifier et attribuer', text: 'Le score et les règles d’attribution placent chaque prospect devant la bonne personne.' },
          { title: 'Rencontrer et proposer', text: 'Fixez le rendez-vous, puis envoyez un contrat depuis votre modèle.' },
          { title: 'Signer et encaisser', text: 'Le client signe en ligne et paie l’acompte : le prospect atteint sa dernière phase.' },
        ],
      },
      faq: {
        heading: 'CRM commercial : questions fréquentes',
        items: [
          { q: 'Quelles sont les cinq phases ?', a: 'Prise de contact, rendez-vous, contrat, signature et acompte. Le prospect avance à chaque étape franchie, et la dernière phase se termine quand l’acompte est payé.' },
          { q: 'Les prospects peuvent-ils arriver depuis notre site ?', a: 'Oui, par un formulaire de demande public ou par un webhook. Les deux créent directement des prospects, protégés contre les doublons.' },
          { q: 'Comment fonctionne l’attribution ?', a: 'Des règles d’attribution envoient chaque nouveau prospect au bon collègue, et un prospect peut être réattribué à tout moment.' },
          { q: 'Que fait le rédacteur commercial IA ?', a: 'Il rédige e-mails, relances et notes dans le ton de votre marque, pour qu’une personne les relise et les envoie. Voir l’[assistant IA](page:ai).' },
          { q: 'Peut-on voir des prévisions ?', a: 'Oui. Les rapports comprennent une prévision du pipeline à côté de l’activité de l’équipe.' },
          { q: 'Que se passe-t-il après la signature ?', a: 'Dans un club, un prospect qui a signé et payé devient membre actif en une étape, avec e-mail de bienvenue et portail membre. Voir [CRM pour club privé](page:private-clubs).' },
        ],
      },
      related: ['e-signature', 'email', 'ai'],
      cta: {
        heading: 'Suivez chaque prospect jusqu’à l’acompte',
        text: 'Créez un espace de travail et importez vos prospects, ou demandez une démonstration.',
      },
    },

    'e-signature': {
      eyebrow: 'Contrats et signature électronique',
      h1: 'Des contrats en signature électronique',
      h1Accent: 'avec l’acompte à la signature',
      lead: 'Générez vos contrats depuis vos modèles, faites-les signer en ligne et encaissez l’acompte dans la foulée, par carte ou facture QR. Le signataire n’a besoin d’aucun compte, et un contrat signé sur papier peut aussi être téléversé.',
      heroCardTitle: 'Du modèle à l’acompte',
      heroPoints: [
        'Contrats générés depuis vos modèles',
        'Signés en ligne, sans compte pour le signataire',
        'Acompte par carte ou facture QR',
        'Contrats papier téléversés',
        'Le prospect avance tout seul',
      ],
      intro: {
        heading: 'L’étape la plus lente de la plupart des ventes',
        paragraphs: [
          'Le client a dit oui. Ensuite, le contrat est rédigé dans un traitement de texte, exporté en PDF, envoyé, imprimé, signé, scanné et renvoyé, et quelqu’un pense à réclamer l’acompte une semaine plus tard. Chaque étape laisse à la vente le temps de refroidir.',
          'CadenceE réduit tout cela à un lien. Le contrat est généré depuis votre modèle avec les données du prospect, le client le lit et le signe en ligne, et l’acompte est demandé au moment de la signature. Le prospect passe à la phase suivante dès que c’est fait.',
        ],
      },
      sections: [
        {
          heading: 'Des contrats depuis vos modèles',
          paragraphs: [
            'Rédigez chaque contrat une seule fois comme modèle, avec des champs pour le nom du client, l’adhésion ou l’événement, les dates et les montants. CadenceE les remplit depuis la fiche du prospect : chaque contrat est juste du premier coup.',
          ],
        },
        {
          heading: 'Signer sans créer de compte',
          paragraphs: [
            'Le signataire ouvre un lien sécurisé, lit le contrat et signe en ligne. Il ne crée pas de compte et n’installe rien, ce qu’attend un client qui signe une adhésion ou une réservation d’événement.',
          ],
        },
        {
          heading: 'L’acompte, encaissé à la signature',
          paragraphs: [
            'Un contrat peut demander un acompte à la signature. Le client paie par carte ou avec une facture QR, et le paiement est enregistré sur le contrat. Le [suivi commercial](page:sales) atteint sa dernière phase, l’acompte, sans relance.',
          ],
          bullets: [
            'Paiement par carte à la signature',
            'Facture QR pour un virement',
            'Paiement enregistré sur le contrat',
            'Rapproché plus tard avec votre fichier bancaire',
          ],
        },
        {
          heading: 'Le papier fonctionne toujours',
          paragraphs: [
            'Certains clients préfèrent le papier, et certains contrats se signent en personne. Téléversez l’exemplaire signé : CadenceE l’enregistre comme tout contrat signé, et le pipeline comme la fiche client restent complets. Dans un club, le prospect signé devient ensuite membre : voir [CRM pour club privé](page:private-clubs).',
          ],
        },
      ],
      features: {
        heading: 'Fonctionnalités contrats',
        items: [
          { icon: 'contract', title: 'Modèles de contrat', text: 'Réutilisables, remplis depuis la fiche du prospect.' },
          { icon: 'signature', title: 'Signature électronique', text: 'Un lien sécurisé pour lire et signer.' },
          { icon: 'portal', title: 'Aucun compte requis', text: 'Le signataire ne crée pas de compte.' },
          { icon: 'deposit', title: 'Acompte à la signature', text: 'Par carte ou facture QR, enregistré sur le contrat.' },
          { icon: 'paper', title: 'Contrats papier', text: 'Téléversez les contrats signés à la main.' },
          { icon: 'leads', title: 'Pipeline à jour', text: 'Le prospect passe seul aux phases signature et acompte.' },
          { icon: 'members', title: 'Vers l’adhésion', text: 'Dans un club, le prospect signé et payé devient membre actif en une étape.' },
          { icon: 'audit', title: 'Journal d’audit', text: 'Chaque modification est enregistrée, avec son auteur.' },
          { icon: 'outbox', title: 'Envoyé depuis votre boîte d’envoi', text: 'Les e-mails de contrat partent du CRM, avec nouvelles tentatives.' },
        ],
      },
      steps: {
        heading: 'Comment un contrat se signe',
        items: [
          { title: 'Préparer', text: 'Choisissez le modèle ; CadenceE remplit les données du prospect.' },
          { title: 'Envoyer', text: 'Le client reçoit un lien par e-mail.' },
          { title: 'Signer et payer', text: 'Il lit, signe en ligne et paie l’acompte par carte ou facture QR.' },
          { title: 'Terminé', text: 'Le contrat est conservé sur la fiche et le prospect avance.' },
        ],
      },
      faq: {
        heading: 'Signature électronique : questions fréquentes',
        items: [
          { q: 'Le signataire a-t-il besoin d’un compte CadenceE ?', a: 'Non. Il ouvre le lien reçu, lit le contrat et signe en ligne, sans créer de compte.' },
          { q: 'Peut-on encaisser un acompte à la signature ?', a: 'Oui. L’acompte se paie à la signature par carte ou par facture QR, et il est enregistré sur le contrat.' },
          { q: 'Et si un client signe sur papier ?', a: 'Téléversez l’exemplaire signé. CadenceE l’enregistre comme signé : le prospect et la fiche client restent complets.' },
          { q: 'Les contrats sont-ils générés depuis des modèles ?', a: 'Oui. Vous rédigez chaque type de contrat une fois comme modèle, et CadenceE y place les données du client.' },
          { q: 'Que se passe-t-il après la signature ?', a: 'Le prospect passe aux phases signature et acompte. Dans un club, un prospect signé et payé devient membre actif en une étape, avec un e-mail de bienvenue.' },
          { q: 'Les modifications sont-elles tracées ?', a: 'Oui. Le journal d’audit enregistre les modifications : vous savez qui a fait quoi, et quand.' },
        ],
      },
      related: ['sales', 'billing', 'private-clubs'],
      cta: {
        heading: 'Contrat signé et acompte payé, en un seul lien',
        text: 'Créez un espace de travail et envoyez votre premier contrat, ou demandez une démonstration.',
      },
    },

    swiss: {
      eyebrow: 'CRM suisse',
      h1: 'Un CRM suisse qui parle',
      h1Accent: 'français, allemand et anglais',
      lead: 'CadenceE a été conçu en Suisse pour les entreprises suisses : paiements par facture QR et TWINT, export comptable vers Bexio et Abacus, application en français, en allemand et en anglais, et demandes de protection des données traitées selon la nLPD.',
      heroCardTitle: 'Fait pour la Suisse',
      heroPoints: [
        'Facture QR et TWINT',
        'Carte et SEPA également',
        'Export comptable vers Bexio et Abacus',
        'Français, allemand et anglais',
        'Demandes selon la nLPD et le RGPD',
      ],
      intro: {
        heading: 'Pourquoi une entreprise suisse a besoin d’un CRM suisse',
        paragraphs: [
          'Les CRM internationaux sont pensés pour d’autres marchés. Ils facturent d’une manière que les clients suisses ne paient pas, supposent une seule langue par équipe et laissent l’export comptable à un consultant. Pour un club ou une entreprise en Suisse, ces lacunes deviennent du travail manuel chaque mois.',
          'CadenceE part des habitudes suisses. Membres et clients paient avec la facture QR ou TWINT, la fiduciaire reçoit un export pour Bexio ou Abacus, l’accueil travaille en français pendant que la comptabilité travaille en allemand, et les demandes au titre de la loi fédérale sur la protection des données font partie du produit.',
        ],
      },
      sections: [
        {
          heading: 'Paiements suisses : facture QR et TWINT',
          paragraphs: [
            'Les relevés peuvent porter une facture QR, le bulletin que toutes les applications bancaires suisses savent lire. Membres et clients peuvent aussi payer avec TWINT, par carte ou par SEPA, et l’acompte à la signature d’un contrat accepte la carte et la facture QR. Les fichiers bancaires sont rapprochés des montants dus : vous voyez qui a payé sans pointer les lignes à la main. Voir [facturation et paiements](page:billing).',
          ],
        },
        {
          heading: 'Export comptable vers Bexio et Abacus',
          paragraphs: [
            'L’export comptable part vers Bexio ou Abacus, deux logiciels de comptabilité très répandus en Suisse. Le grand livre du CRM se corrige uniquement par contre-passation, jamais par modification : ce qui a été exporté reste vrai, et chaque correction reste visible.',
          ],
        },
        {
          heading: 'Français, allemand et anglais, pour chacun',
          paragraphs: [
            'Toute l’application existe en français, en allemand et en anglais, et chacun choisit sa langue. Une collègue à Lausanne et un collègue à Zurich travaillent sur le même prospect, chacun dans sa langue. Les montants suivent les réglages régionaux de l’espace de travail, par exemple CHF 1’234.50, et les e-mails préparés existent en français et en anglais.',
          ],
        },
        {
          heading: 'Protection des données selon la nLPD et le RGPD',
          paragraphs: [
            'Les demandes d’accès (export des données d’une personne) et d’effacement se traitent dans le CRM, selon le RGPD et la nLPD. Chaque modification est inscrite au journal d’audit, les rôles et autorisations limitent qui voit quoi, et la connexion accepte les clés d’accès et la double authentification.',
          ],
          bullets: [
            'Demandes d’accès et d’effacement',
            'Journal d’audit de chaque modification',
            'Rôles et autorisations',
            'Clés d’accès et double authentification',
          ],
        },
      ],
      features: {
        heading: 'Fonctionnalités suisses',
        items: [
          { icon: 'payments', title: 'Facture QR', text: 'Des relevés avec une facture QR lisible par toutes les banques suisses.' },
          { icon: 'swiss', title: 'TWINT', text: 'Membres et clients paient avec TWINT.' },
          { icon: 'bank', title: 'Rapprochement bancaire', text: 'Les fichiers bancaires rapprochés des montants ouverts.' },
          { icon: 'accounting', title: 'Export Bexio', text: 'Export comptable pour Bexio.' },
          { icon: 'ledger', title: 'Export Abacus', text: 'Export pour Abacus, depuis un grand livre jamais modifié.' },
          { icon: 'languages', title: 'Trois langues', text: 'Français, allemand et anglais, au choix de chacun.' },
          { icon: 'privacy', title: 'Demandes nLPD et RGPD', text: 'Accès et effacement traités dans le CRM.' },
          { icon: 'audit', title: 'Journal d’audit', text: 'Chaque modification enregistrée.' },
          { icon: 'two-factor', title: 'Clés d’accès et double authentification', text: 'Une connexion sûre pour toute l’équipe.' },
        ],
      },
      steps: {
        heading: 'Configurer CadenceE pour la Suisse',
        items: [
          { title: 'Créez l’espace de travail', text: 'Choisissez les réglages régionaux ; chaque collègue choisit ensuite sa langue.' },
          { title: 'Ajoutez les moyens de paiement', text: 'Facture QR, TWINT, carte et SEPA pour les membres et les clients.' },
          { title: 'Branchez la comptabilité', text: 'Choisissez l’export pour Bexio ou pour Abacus.' },
          { title: 'Travaillez dans vos langues', text: 'Invitez l’équipe : chacun travaille en français, en allemand ou en anglais.' },
        ],
      },
      faq: {
        heading: 'CRM suisse : questions fréquentes',
        items: [
          { q: 'CadenceE prend-il en charge la facture QR ?', a: 'Oui. Les relevés peuvent porter une facture QR, et l’acompte à la signature d’un contrat peut être payé avec une facture QR.' },
          { q: 'Les membres peuvent-ils payer avec TWINT ?', a: 'Oui. TWINT fait partie des moyens de paiement, avec la carte, SEPA et la facture QR.' },
          { q: 'Vers quels logiciels de comptabilité l’export fonctionne-t-il ?', a: 'Bexio et Abacus.' },
          { q: 'L’application existe-t-elle en français et en allemand ?', a: 'Oui, en français, en allemand et en anglais, au choix de chacun. Les e-mails préparés existent en français et en anglais.' },
          { q: 'Comment CadenceE protège-t-il les données ?', a: 'Les demandes d’accès et d’effacement sont traitées selon le RGPD et la nLPD, chaque modification est inscrite au journal d’audit, et l’accès est contrôlé par des rôles et des autorisations.' },
          { q: 'Les montants sont-ils affichés en francs suisses ?', a: 'Oui. Les montants suivent les réglages régionaux de l’espace de travail, par exemple CHF 1’234.50 dans un espace suisse.' },
          { q: 'Qui a conçu CadenceE ?', a: 'CadenceE a été conçu en Suisse, avec le Pavillon 46, club privé à La Croix-sur-Lutry (VD), qui l’utilise chaque jour.' },
        ],
      },
      related: ['billing', 'private-clubs', 'features'],
      cta: {
        heading: 'Un CRM qui fonctionne comme la Suisse',
        text: 'Créez un espace de travail en français, en allemand ou en anglais, ou demandez une démonstration.',
      },
    },

    billing: {
      eyebrow: 'Facturation et paiements',
      h1: 'Facturation et paiements pour clubs,',
      h1Accent: 'sans la course de fin de mois',
      lead: 'Cotisations récurrentes, comptes membres, notes de bar et relevés PDF mensuels dans un seul CRM, payés par carte, TWINT, SEPA ou facture QR, rapprochés de votre fichier bancaire et exportés vers Bexio ou Abacus.',
      heroCardTitle: 'La facturation dans CadenceE',
      heroPoints: [
        'Cotisations récurrentes à échéance',
        'Notes et comptes membres',
        'Relevés PDF mensuels',
        'Carte, TWINT, SEPA et facture QR',
        'Rapprochement bancaire, export Bexio et Abacus',
      ],
      intro: {
        heading: 'Facturer un club, c’est plus qu’envoyer des factures',
        paragraphs: [
          'Un membre doit sa cotisation annuelle, un dîner de jeudi dernier, deux verres au bar et un billet pour l’événement du mois prochain. Une bonne facturation additionne tout correctement, envoie un seul relevé clair et sait quand il a été payé.',
          'CadenceE le fait à partir des fiches que le club utilise déjà. Cotisations, notes, événements et privatisations s’inscrivent sur le compte du membre. Les paiements reviennent par carte, TWINT, SEPA ou facture QR, et le fichier bancaire est rapproché : chaque compte s’équilibre tout seul.',
        ],
      },
      sections: [
        {
          heading: 'Cotisations récurrentes et frais',
          paragraphs: [
            'Les cotisations sont facturées automatiquement à leur échéance. Les autres frais (événements, privatisations, suppléments) s’ajoutent au même compte, pour que le membre reçoive un seul relevé plutôt qu’une pile de factures. Les cotisations sont liées à l’[adhésion](page:membership) et suivent donc les renouvellements et les foyers.',
          ],
        },
        {
          heading: 'Notes, caisse et comptes membres',
          paragraphs: [
            'Les consommations au bar et au restaurant vont sur la note, avec une connexion à la caisse Lightspeed qui fait remonter les ventes. Membres et habitués peuvent avoir un compte réglé chaque mois. Voir le [CRM hôtellerie-restauration](page:hospitality) pour le côté clients.',
          ],
        },
        {
          heading: 'Relevés et paiements',
          paragraphs: [
            'En fin de mois, chaque compte reçoit un relevé PDF. Les membres paient par carte, TWINT, SEPA ou avec la facture QR du relevé. L’acompte à la signature d’un contrat se paie de la même manière, par carte ou facture QR.',
          ],
        },
        {
          heading: 'Rapprochement bancaire, grand livre fiable et comptabilité',
          paragraphs: [
            'Téléversez le fichier bancaire : CadenceE rapproche les paiements reçus des montants dus. Le grand livre ne se modifie jamais, il se contre-passe : une erreur se corrige par une écriture visible, pas en réécrivant l’historique. L’export comptable part vers Bexio ou Abacus, et la fiduciaire part de chiffres propres.',
          ],
          bullets: [
            'Rapprochement du fichier bancaire',
            'Grand livre corrigé par contre-passation',
            'Export vers Bexio',
            'Export vers Abacus',
          ],
        },
      ],
      features: {
        heading: 'Fonctionnalités de facturation et de paiement',
        items: [
          { icon: 'dues', title: 'Cotisations récurrentes', text: 'Facturées à échéance.' },
          { icon: 'tabs', title: 'Notes de bar et de restaurant', text: 'Les consommations par membre ou par client.' },
          { icon: 'pos', title: 'Caisse Lightspeed', text: 'Les ventes de la caisse remontent dans le CRM.' },
          { icon: 'house-account', title: 'Comptes membres', text: 'Réglés une fois par mois.' },
          { icon: 'statements', title: 'Relevés PDF mensuels', text: 'Un relevé clair par compte.' },
          { icon: 'payments', title: 'Carte et SEPA', text: 'Les moyens de paiement que vos membres utilisent déjà.' },
          { icon: 'swiss', title: 'TWINT et facture QR', text: 'Les paiements suisses intégrés.' },
          { icon: 'bank', title: 'Rapprochement bancaire', text: 'Les paiements rapprochés des montants dus.' },
          { icon: 'ledger', title: 'Grand livre par contre-passation', text: 'Les corrections sont des écritures, jamais des modifications.' },
          { icon: 'accounting', title: 'Bexio et Abacus', text: 'L’export comptable pour votre fiduciaire.' },
          { icon: 'gift-card', title: 'Cartes cadeaux', text: 'Vendez et encaissez des cartes cadeaux.' },
          { icon: 'donation', title: 'Dons', text: 'Enregistrez les dons à côté des autres revenus.' },
        ],
      },
      steps: {
        heading: 'Le déroulement d’un mois de facturation',
        items: [
          { title: 'Les frais s’accumulent', text: 'Cotisations, notes, événements et suppléments s’inscrivent sur chaque compte.' },
          { title: 'Les relevés partent', text: 'Chaque compte reçoit un relevé PDF avec sa facture QR.' },
          { title: 'Les paiements arrivent', text: 'Par carte, TWINT, SEPA ou facture QR.' },
          { title: 'Les comptes se bouclent', text: 'Le fichier bancaire est rapproché et l’export part vers Bexio ou Abacus.' },
        ],
      },
      faq: {
        heading: 'Facturation d’un club : questions fréquentes',
        items: [
          { q: 'Quels moyens de paiement sont pris en charge ?', a: 'Carte, TWINT, SEPA et facture QR.' },
          { q: 'Peut-on facturer les notes de bar aux membres ?', a: 'Oui. Les notes passent sur le compte du membre et figurent sur le relevé mensuel. La connexion à la caisse Lightspeed fait remonter les ventes.' },
          { q: 'Peut-on corriger une erreur ?', a: 'Oui, par contre-passation. Le grand livre n’est jamais modifié : l’historique reste complet et chaque compte s’explique de lui-même.' },
          { q: 'CadenceE s’intègre-t-il à un logiciel de comptabilité ?', a: 'CadenceE exporte vers Bexio et Abacus. Voir notre [CRM suisse](page:swiss).' },
          { q: 'Comment les paiements bancaires sont-ils rapprochés ?', a: 'Téléversez le fichier bancaire : CadenceE rapproche les paiements reçus des montants dus.' },
          { q: 'Peut-on encaisser un acompte à la signature d’un contrat ?', a: 'Oui, par carte ou facture QR. Voir la [signature électronique](page:e-signature).' },
        ],
      },
      related: ['swiss', 'membership', 'hospitality'],
      cta: {
        heading: 'Simplifiez votre fin de mois',
        text: 'Créez un espace de travail et configurez cotisations et moyens de paiement, ou demandez une démonstration.',
      },
    },

    email: {
      eyebrow: 'Automatisation des e-mails',
      h1: 'L’automatisation des e-mails dans votre CRM,',
      h1Accent: 'consentement compris',
      lead: 'Tous les e-mails depuis une seule boîte d’envoi, avec nouvelles tentatives et gestion des rebonds. Des séquences et des automatisations qui relancent pour vous, des newsletters qui invitent à réserver un rendez-vous, et des campagnes avec consentement et désinscription en un clic.',
      heroCardTitle: 'Les e-mails dans CadenceE',
      heroPoints: [
        'Une boîte d’envoi, avec nouvelles tentatives et rebonds gérés',
        'Séquences et automatisations',
        'Newsletters avec page de réservation',
        'Campagnes avec consentement',
        'Désinscription en un clic dans chaque e-mail',
      ],
      intro: {
        heading: 'Des e-mails qui connaissent vos clients',
        paragraphs: [
          'Quand l’e-mailing vit dans un outil marketing séparé, il ignore ce que sait le CRM : que le prospect a signé hier, que le membre ne veut plus de newsletter ou que le dernier message est revenu en erreur. Les messages partent vers les mauvaises personnes, au mauvais moment.',
          'Dans CadenceE, l’e-mail fait partie du CRM. Chaque message, réponse individuelle, étape de séquence ou newsletter, passe par la même boîte d’envoi et s’inscrit sur la fiche de son destinataire. Le consentement et les désinscriptions sont respectés partout, automatiquement.',
        ],
      },
      sections: [
        {
          heading: 'Une boîte d’envoi, une distribution soignée',
          paragraphs: [
            'Tous les e-mails partent d’une seule boîte d’envoi. Quand un serveur refuse temporairement, le message est renvoyé ; quand une adresse rebondit, c’est noté pour que vous cessiez d’y écrire. Vous voyez ce qui est parti, vers qui, et ce qu’il est devenu.',
          ],
        },
        {
          heading: 'Séquences et automatisations',
          paragraphs: [
            'Les séquences envoient une série de relances selon un calendrier. Les automatisations réagissent à ce qui se passe dans le CRM, comme un nouveau prospect venu du site ou un contrat signé : le bon message part sans que personne n’ait à y penser.',
          ],
        },
        {
          heading: 'Des newsletters qui remplissent l’agenda',
          paragraphs: [
            'Les newsletters s’adressent aux membres, aux clients ou aux prospects. Une newsletter peut inviter les prospects à réserver un rendez-vous sur une page de réservation : l’intérêt devient un créneau dans l’agenda, et le prospect avance dans le [suivi commercial](page:sales).',
          ],
        },
        {
          heading: 'Des campagnes avec consentement',
          paragraphs: [
            'Les campagnes ne touchent que les personnes qui ont accepté de les recevoir, et chaque message porte un lien de désinscription en un clic, effectif partout à la fois. Les e-mails préparés existent en français et en anglais, et le [rédacteur IA](page:ai) aide à en écrire de nouveaux.',
          ],
          bullets: [
            'Consentement enregistré sur chaque fiche',
            'Désinscription en un clic',
            'E-mails préparés en français et en anglais',
            'Brouillons IA pour les nouveaux messages',
          ],
        },
      ],
      features: {
        heading: 'Fonctionnalités e-mail',
        items: [
          { icon: 'outbox', title: 'Une boîte d’envoi', text: 'Chaque e-mail part d’un seul endroit et s’inscrit sur la fiche.' },
          { icon: 'audit', title: 'Nouvelles tentatives et rebonds', text: 'Refus temporaires renvoyés, rebonds enregistrés.' },
          { icon: 'sequences', title: 'Séquences', text: 'Des relances selon un calendrier.' },
          { icon: 'automations', title: 'Automatisations', text: 'Des messages déclenchés par ce qui se passe dans le CRM.' },
          { icon: 'newsletter', title: 'Newsletters', text: 'Aux membres, aux clients et aux prospects.' },
          { icon: 'booking', title: 'Page de réservation', text: 'Les prospects réservent un rendez-vous depuis une newsletter.' },
          { icon: 'campaigns', title: 'Campagnes', text: 'Des envois aux groupes que vous choisissez.' },
          { icon: 'consent', title: 'Consentement et désinscription', text: 'Consentement respecté, désinscription en un clic.' },
          { icon: 'languages', title: 'E-mails en français et en anglais', text: 'Des e-mails préparés dans les deux langues.' },
          { icon: 'writer', title: 'Rédacteur IA', text: 'Des brouillons dans votre ton, à relire.' },
        ],
      },
      steps: {
        heading: 'Comment fonctionnent les e-mails dans CadenceE',
        items: [
          { title: 'Configurez l’envoi', text: 'Ajoutez l’adresse d’envoi de votre espace et choisissez vos modèles.' },
          { title: 'Écrivez les séquences', text: 'Rédigez les relances une fois et décidez quand chacune part.' },
          { title: 'Automatisez', text: 'Nouveaux prospects, signatures ou renouvellements déclenchent le bon message.' },
          { title: 'Envoyez avec consentement', text: 'Newsletters et campagnes vont aux personnes qui ont accepté, et chaque envoi s’inscrit sur leur fiche.' },
        ],
      },
      faq: {
        heading: 'Automatisation des e-mails : questions fréquentes',
        items: [
          { q: 'Que se passe-t-il quand un e-mail rebondit ?', a: 'Le rebond est enregistré sur la fiche, pour que vous cessiez d’écrire à une adresse qui ne fonctionne pas. Les refus temporaires sont renvoyés automatiquement.' },
          { q: 'Quelle différence entre une séquence et une automatisation ?', a: 'Une séquence est une série de relances envoyées selon un calendrier. Une automatisation réagit à un événement dans le CRM, comme un nouveau prospect ou un contrat signé, et envoie le bon message à ce moment-là.' },
          { q: 'Comment le consentement est-il géré ?', a: 'Campagnes et newsletters ne partent qu’aux personnes qui ont accepté de les recevoir, et chaque e-mail porte un lien de désinscription en un clic, effectif immédiatement.' },
          { q: 'Les prospects peuvent-ils réserver un rendez-vous depuis une newsletter ?', a: 'Oui. Une newsletter peut les inviter à réserver un rendez-vous sur une page de réservation.' },
          { q: 'Dans quelles langues sont les e-mails préparés ?', a: 'En français et en anglais. L’application elle-même existe en français, en allemand et en anglais.' },
          { q: 'L’IA peut-elle aider à rédiger ?', a: 'Oui. Le rédacteur commercial IA propose des brouillons dans votre ton, qu’une personne relit avant l’envoi. Voir l’[assistant IA](page:ai).' },
        ],
      },
      related: ['sales', 'ai', 'membership'],
      cta: {
        heading: 'Le bon e-mail, à la bonne personne',
        text: 'Créez un espace de travail et ajoutez votre adresse d’envoi, ou demandez une démonstration.',
      },
    },

    ai: {
      eyebrow: 'Assistant IA pour CRM',
      h1: 'Un assistant IA pour votre CRM',
      h1Accent: 'qui demande avant d’agir',
      lead: 'L’assistant de CadenceE est basé sur Claude. Il lit votre espace de travail, propose des modifications qu’une personne confirme, rédige vos e-mails commerciaux dans votre ton et prépare chaque matin le brief de ce qui demande votre attention.',
      heroCardTitle: 'L’IA dans CadenceE',
      heroPoints: [
        'Basé sur Claude, d’Anthropic',
        'Il propose ; une personne confirme',
        'Rédacteur commercial IA pour e-mails et notes',
        'Un brief chaque matin',
        'Intégré au CRM, pas un outil à part',
      ],
      intro: {
        heading: 'Une IA qui aide, sans prendre le contrôle',
        paragraphs: [
          'Dans un CRM, l’IA peut faire gagner des heures, ou semer le désordre. Un assistant qui modifie les fiches tout seul peut mettre à jour le mauvais prospect, écraser une note ou envoyer ce que personne n’a validé. Pour un club qui détient les données personnelles de ses membres, c’est inacceptable.',
          'L’assistant de CadenceE fait autrement : il lit et il rédige, et il laisse les décisions aux personnes. Chaque modification qu’il suggère s’affiche comme une proposition, qu’une personne confirme avant tout enregistrement.',
        ],
      },
      sections: [
        {
          heading: 'Un assistant qui propose, sans imposer',
          paragraphs: [
            'Demandez à l’assistant de mettre de l’ordre dans une fiche, de préparer les suites d’un rendez-vous ou de mettre à jour plusieurs fiches à la fois. Il détermine ce qui doit changer et présente les changements comme des propositions. Rien n’est écrit avant la confirmation d’une personne, et les changements confirmés figurent au journal d’audit comme tous les autres.',
          ],
        },
        {
          heading: 'Le rédacteur commercial IA',
          paragraphs: [
            'Le rédacteur prépare e-mails commerciaux, relances, messages d’affaire et notes dans le ton de votre marque : professionnel, chaleureux ou persuasif. Vous choisissez l’objectif et la longueur, ajustez le brouillon d’un clic et l’envoyez depuis la boîte d’envoi du CRM. Voir l’[automatisation des e-mails](page:email).',
          ],
        },
        {
          heading: 'Un brief, chaque matin',
          paragraphs: [
            'Chaque matin, chacun reçoit le brief de sa journée dans l’espace de travail : les prospects, les tâches et les rendez-vous qui demandent son attention. Il est construit à partir des fiches que cette personne a le droit de voir, avec un lien vers chacune.',
          ],
        },
        {
          heading: 'Basé sur Claude',
          paragraphs: [
            'L’assistant fonctionne avec Claude, le modèle d’IA conçu par Anthropic, et travaille à l’intérieur de CadenceE sur les fiches de votre espace. Il se trouve à côté des autres [fonctionnalités du CRM](page:features), et non dans un outil séparé qui garderait une copie de vos données.',
          ],
        },
      ],
      features: {
        heading: 'Fonctionnalités IA',
        items: [
          { icon: 'ai', title: 'Assistant IA', text: 'Demandez en langage courant, recevez des propositions à confirmer.' },
          { icon: 'audit', title: 'Des changements que vous confirmez', text: 'Rien n’est enregistré sans accord, et tout est journalisé.' },
          { icon: 'writer', title: 'Rédacteur commercial IA', text: 'E-mails, relances et notes dans votre ton.' },
          { icon: 'brief', title: 'Brief du matin', text: 'Les priorités du jour, préparées chaque matin.' },
          { icon: 'leads', title: 'Prochaines actions suggérées', text: 'Ce qu’il faut faire ensuite sur un prospect ou une affaire.' },
          { icon: 'roles', title: 'Vos règles', text: 'Le brief respecte ce que chacun a le droit de voir.' },
        ],
      },
      steps: {
        heading: 'Travailler avec l’assistant',
        items: [
          { title: 'Demandez', text: 'Décrivez ce dont vous avez besoin, avec vos mots.' },
          { title: 'Vérifiez', text: 'L’assistant affiche les modifications qu’il propose.' },
          { title: 'Confirmez', text: 'Acceptez ce qui est juste ; rien d’autre n’est enregistré.' },
          { title: 'Enchaînez', text: 'Le rédacteur prépare l’e-mail, et demain matin, le brief vous attend.' },
        ],
      },
      faq: {
        heading: 'Assistant IA : questions fréquentes',
        items: [
          { q: 'Quel modèle d’IA CadenceE utilise-t-il ?', a: 'L’assistant est basé sur Claude, conçu par Anthropic.' },
          { q: 'L’IA peut-elle modifier mes données sans demander ?', a: 'Non. L’assistant ne fait que proposer. Une personne confirme chaque modification avant l’enregistrement, et les changements confirmés sont inscrits au journal d’audit.' },
          { q: 'Que fait le rédacteur commercial IA ?', a: 'Il rédige e-mails commerciaux, relances, messages d’affaire et notes dans le ton et la longueur choisis, pour qu’une personne les relise et les envoie.' },
          { q: 'Qu’est-ce que le brief du matin ?', a: 'Un résumé quotidien de la journée de chacun dans l’espace de travail, construit à partir des fiches que la personne a le droit de voir.' },
          { q: 'L’IA est-elle un outil séparé ?', a: 'Non. L’assistant, le rédacteur et le brief font partie de CadenceE et travaillent sur les fiches de votre espace.' },
          { q: 'Sommes-nous obligés d’utiliser l’IA ?', a: 'Non. Le CRM fonctionne sans elle ; l’assistant est là quand il fait gagner du temps. Voir toutes les [fonctionnalités](page:features).' },
        ],
      },
      related: ['sales', 'email', 'features'],
      cta: {
        heading: 'Laissez l’IA lire et rédiger',
        text: 'Créez un espace de travail et essayez l’assistant sur vos propres prospects, ou demandez une démonstration.',
      },
    },

    pricing: frPricing,
  },
};

export default withFrenchSpacing(fr);
