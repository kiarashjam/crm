import type { GuideContent } from '../types';

// Faits techniques vérifiés auprès de SIX (Swiss Implementation Guidelines for the QR-bill, v2.3,
// et six-group.com/qr-bill), de PostFinance (fiche 460.445) et du portail PME de la
// Confédération (kmu.admin.ch) en novembre 2026.
const guide: GuideContent = {
  eyebrow: 'Facturation du club',
  h1: 'La facture QR pour les clubs et associations en Suisse',
  lead: 'Comment fonctionne la facture QR, quel compte et quelle référence choisir, ce qui doit y figurer, et comment envoyer les cotisations en PDF et savoir qui a payé sans pointer les relevés à la main.',
  readingMinutes: 9,
  keyTakeaways: [
    'La facture QR est le seul bulletin de paiement suisse : les bulletins orange et rouges ne sont plus traités depuis le 30 septembre 2022.',
    'Un QR-IBAN va avec une référence QR de 27 chiffres ; un IBAN ordinaire avec une référence créancier (RF…) ou sans référence.',
    'C’est la référence structurée qui permet d’attribuer chaque paiement au bon membre, automatiquement.',
    'Depuis le 22 novembre 2025, les adresses doivent être structurées : rue, numéro, NPA et localité dans des champs séparés.',
    'Envoyée en PDF, la facture se paie en la scannant dans l’app bancaire ; le fichier camt de la banque vous dit ensuite qui a payé.',
  ],
  sections: [
    {
      id: 'qu-est-ce-que-la-facture-qr',
      heading: 'Ce qu’est la facture QR, et ce qu’elle a remplacé',
      paragraphs: [
        'La facture QR (QR-facture dans le vocabulaire officiel) est le bulletin de paiement standard en Suisse et au Liechtenstein. Introduite le 30 juin 2020, elle a remplacé le bulletin de versement rouge (BV) et le bulletin orange avec numéro de référence (BVR). Depuis le 30 septembre 2022, banques et PostFinance ne traitent plus les anciens bulletins, ni en ligne ni au guichet, et les ordres permanents liés se sont arrêtés.',
        'Le Swiss QR Code, un carré marqué d’une petite croix suisse, contient toutes les données de paiement : l’app bancaire le lit en une seconde. Les règles sont fixées par SIX ; la dernière révision, en vigueur depuis le 22 novembre 2025, a rendu les adresses structurées obligatoires.',
      ],
    },
    {
      id: 'section-paiement-et-recepisse',
      heading: 'La section paiement et le récépissé',
      paragraphs: [
        'Le bas d’une facture QR comporte deux parties. La section paiement, à droite, est au format A6 paysage (148 × 105 mm) et porte le code QR. Le récépissé, à gauche, mesure 62 × 105 mm : c’est ce que garde la personne qui paie au guichet postal. Sur papier, les deux sont perforés, sans publicité et avec un verso vierge. Les intitulés peuvent être en français, en allemand, en italien ou en anglais.',
      ],
      bullets: [
        'Compte / Payable à : l’IBAN ou le QR-IBAN, avec le nom et l’adresse du club',
        'Référence : la référence QR ou la référence créancier, s’il y en a une',
        'Informations supplémentaires : un court message, comme « Cotisation 2027 »',
        'Payable par : le nom et l’adresse du membre, ou un cadre vide',
        'Monnaie et Montant : CHF ou EUR, et le montant ou un cadre vide',
      ],
    },
    {
      id: 'qr-iban-ou-iban',
      heading: 'QR-IBAN avec référence QR, ou IBAN avec référence créancier',
      paragraphs: [
        'C’est la seule vraie décision à prendre. Compte et type de référence vont ensemble : les mélanger est la première cause de facture QR refusée.',
      ],
      subsections: [
        {
          heading: 'QR-IBAN et référence QR (QRR)',
          paragraphs: [
            'Le QR-IBAN est un numéro distinct que votre banque attribue à un compte existant. Son identifiant bancaire (le QR-IID) se situe entre 30000 et 31999 : son cinquième caractère est donc toujours un 3. Il ne sert qu’à recevoir des paiements, avec une référence QR de 27 chiffres dont le dernier est un chiffre de contrôle, et qui contient souvent les numéros de membre et de facture.',
          ],
        },
        {
          heading: 'IBAN et référence créancier (SCOR)',
          paragraphs: [
            'Avec l’IBAN ordinaire du club, vous utilisez la référence créancier internationale (ISO 11649) : RF, deux chiffres de contrôle, de 5 à 25 caractères au total, lettres admises. Même rôle que la référence QR, comprise aussi à l’étranger.',
          ],
        },
        {
          heading: 'IBAN sans référence (NON)',
          paragraphs: [
            'Une facture QR avec le seul IBAN et un message comme « Cotisation 2027, Rossi » est valable, mais le message est le seul indice de qui a payé : chaque paiement se rapproche à la main. Avec 30 membres, ça passe ; avec 300, c’est pénible.',
          ],
        },
      ],
      tip: 'Demandez à votre banque si elle attribue un QR-IBAN au compte du club, et à quel coût, fichiers camt compris. Sinon, un IBAN ordinaire avec des références créancier fait aussi bien l’affaire.',
    },
    {
      id: 'mentions-de-la-facture-qr',
      heading: 'Ce qui figure sur une facture QR : adresses, montants et monnaies',
      paragraphs: [
        'Le compte doit être un IBAN suisse ou liechtensteinois, commençant par CH ou LI, et le nom et l’adresse du club sont obligatoires. L’adresse du membre, et même le montant, sont facultatifs : sans eux, la section paiement affiche des cadres vides à remplir à la main, pratique pour un don ou une cotisation de soutien.',
        'Les adresses doivent désormais être structurées : rue, numéro, NPA, localité et pays dans des champs séparés. Les lignes d’adresse combinées n’ont été tolérées que jusqu’en novembre 2026 : une liste qui garde toute l’adresse dans une cellule doit d’abord être découpée. Le jeu de caractères élargi accepte aussi ă, ł ou ș.',
        'Seuls le franc suisse et l’euro sont admis, pour un montant de 0.01 à près d’un milliard, imprimé avec deux décimales (CHF 1 250.00). Le message et les informations de facture codées partagent 140 caractères. Pour un foyer, une seule facture avec une seule référence se rapproche plus facilement que plusieurs.',
      ],
    },
    {
      id: 'envoyer-les-cotisations-en-pdf',
      heading: 'Envoyer cotisations et relevés en PDF',
      paragraphs: [
        'La plupart des clubs envoient leurs factures QR par e-mail. Le PDF se paie en e-banking ou en mobile banking ; un symbole de ciseaux délimite la section paiement, pour qu’un membre qui préfère le guichet puisse l’imprimer et la découper.',
      ],
      steps: [
        { title: 'Nettoyer la liste des membres', text: 'Découpez les adresses en champs et vérifiez les e-mails. Sortir une liste d’un tableur est expliqué dans [passer d’Excel à un CRM](page:guide-excel-to-crm).' },
        { title: 'Donner sa référence à chaque facture', text: 'Une référence par membre et par facture, générée avec son chiffre de contrôle, jamais tapée à la main.' },
        { title: 'Générer le PDF', text: 'La facture en haut, la section paiement en bas, imprimée à 100 % et jamais ajustée à la page.' },
        { title: 'Envoyer et enregistrer', text: 'Envoyez à chacun son propre PDF et notez ce que chacun doit, pour savoir ce que le fichier bancaire doit contenir.' },
        { title: 'Relancer', text: 'Après l’échéance, envoyez un rappel courtois avec la même référence : un paiement tardif se rapprochera quand même.' },
      ],
    },
    {
      id: 'comment-les-membres-paient',
      heading: 'Comment les membres paient une facture QR',
      paragraphs: ['Une facture QR bien faite fonctionne quelle que soit la façon de payer.'],
      bullets: [
        'Mobile banking : le membre scanne le code QR avec l’app de sa banque et confirme',
        'E-banking : le membre téléverse le PDF ou lit le code avec un lecteur ou une caméra, selon la banque',
        'Guichet postal : le membre apporte la section paiement imprimée, et les frais de guichet sont débités au club',
        'Par courrier : la section paiement part à la banque du membre avec un ordre de paiement',
        'TWINT : depuis novembre 2026, l’app TWINT paie les factures QR des émetteurs qui y participent, reconnaissables au logo TWINT',
      ],
    },
    {
      id: 'rapprochement-avec-les-fichiers-camt',
      heading: 'Rapprocher les paiements avec les fichiers camt',
      paragraphs: [
        'Les banques annoncent les paiements reçus dans les formats camt de la norme ISO 20022 : camt.054, l’avis de crédit, et camt.053, le relevé de compte. Chaque crédit, téléchargé en XML depuis l’e-banking, indique le montant, la date, le donneur d’ordre et, s’il y en avait une, la référence.',
        'Le logiciel lit la référence et marque la facture correspondante comme payée. Restent quelques exceptions (virement sans référence, paiement partiel, double paiement), les seules lignes à vérifier à la main. Dans CadenceE, vous téléversez le fichier bancaire et les paiements reçus sont rapprochés de ce que chaque membre doit : voir [facturation et paiements pour clubs](page:billing).',
      ],
    },
    {
      id: 'alternatives',
      heading: 'Les alternatives : TWINT, carte, prélèvement et eBill',
      paragraphs: [
        'Pour les cotisations et les relevés, la facture QR reste la référence, car tous les membres peuvent la payer. Pour le reste :',
      ],
      bullets: [
        'TWINT : immédiat et familier, idéal pour les billets d’événement, les notes de bar et les petits montants',
        'Carte : pratique pour les membres à l’étranger et pour un acompte payé en ligne',
        'Prélèvement : SIX arrête LSV+ et BDD le 30 septembre 2028 et n’accepte plus de nouveaux émetteurs depuis le 1er janvier 2026 ; le successeur est eBill Direct Debit, et le CH-DD de PostFinance n’est pas concerné',
        'eBill : la facture arrive dans l’e-banking du membre, qui l’approuve, idéal pour les cotisations récurrentes ; le club s’inscrit auprès de sa banque ou d’un prestataire',
      ],
    },
    {
      id: 'erreurs-frequentes',
      heading: 'Les erreurs fréquentes',
      paragraphs: [
        'La plupart de ces erreurs disparaissent quand les factures sont générées depuis une fiche membre propre, et non recopiées du fichier de l’an dernier.',
      ],
      bullets: [
        'Une référence QR avec un IBAN ordinaire, ou une référence créancier avec un QR-IBAN : la facture est refusée',
        'Des lignes d’adresse combinées d’un ancien modèle, encore utilisées après novembre 2025',
        'La même référence pour tous les membres, ce qui la rend inutile pour le rapprochement',
        'Un montant corrigé dans le texte du PDF, mais pas dans le code QR',
        'Une impression en « ajuster à la page », qui réduit la section paiement sous sa taille réglementaire',
        'Le compte privé du trésorier au lieu de celui du club',
      ],
    },
    {
      id: 'automatiser-avec-un-crm',
      heading: 'Comment un CRM automatise la facture QR',
      paragraphs: [
        'À la main, il faut exporter la liste, générer les factures ailleurs, les envoyer une à une et pointer le relevé : autant d’occasions de manquer un paiement.',
        'Dans CadenceE, la fiche du membre contient l’adresse, le foyer et la cotisation. Les cotisations récurrentes sont facturées à échéance, le relevé PDF de chaque membre peut porter une facture QR, le fichier bancaire est rapproché des montants dus et l’export comptable part vers Bexio ou Abacus. Les membres peuvent aussi payer par carte, TWINT ou SEPA ; les autres spécificités suisses sont sur la page [CRM suisse](page:swiss).',
        'La facturation n’est qu’une partie de la [gestion des membres](page:membership). Les rappels et les renouvellements sont traités dans [renouvellement et fidélisation des membres](page:guide-member-retention), la tenue d’une liste de membres conforme dans la [check-list nLPD pour les clubs](page:guide-fadp-checklist). Pour voir un relevé avec sa facture QR, [essayez la démo](/login?demo=1).',
      ],
    },
  ],
  faq: {
    heading: 'Facture QR : questions fréquentes',
    items: [
      { q: 'Peut-on encore utiliser les bulletins de versement orange ou rouges ?', a: 'Non. Banques et PostFinance ne les traitent plus depuis le 30 septembre 2022, ordres permanents compris. La facture QR est le seul bulletin de paiement suisse.' },
      { q: 'Une association a-t-elle besoin d’un QR-IBAN ?', a: 'Non. Le QR-IBAN n’est nécessaire que pour la référence QR. Avec l’IBAN ordinaire du club, une référence créancier (RF…) permet aussi le rapprochement automatique.' },
      { q: 'Quelle différence entre référence QR et référence créancier ?', a: 'La référence QR (27 chiffres) ne fonctionne qu’avec un QR-IBAN et reste suisse et liechtensteinoise. La référence créancier (RF, 5 à 25 caractères) s’utilise avec un IBAN ordinaire et suit une norme internationale. Les deux permettent un rapprochement automatique.' },
      { q: 'Peut-on envoyer une facture QR par e-mail ?', a: 'Oui. En PDF, elle se paie en e-banking ou en mobile banking. La section paiement et le récépissé sont délimités par un symbole de ciseaux, pour qu’un membre puisse les imprimer et les découper s’il paie au guichet.' },
      { q: 'Une facture QR peut-elle être en euros, ou sans montant ?', a: 'Elle peut être en francs suisses ou en euros, pas dans une autre monnaie. Le montant et l’adresse du payeur sont facultatifs : des cadres vides les remplacent, pratique pour les dons.' },
      { q: 'Qu’est-ce qui a changé en novembre 2025 ?', a: 'Depuis le 22 novembre 2025, les adresses doivent être structurées, avec rue, numéro, NPA et localité dans des champs séparés, et davantage de caractères accentués sont admis. Les adresses combinées n’ont été tolérées que jusqu’en novembre 2026.' },
    ],
  },
  cta: {
    heading: 'Cotisations envoyées, paiements rapprochés',
    text: 'CadenceE place une facture QR sur le relevé de chaque membre et rapproche votre fichier bancaire de ce que chacun doit : le trésorier voit tout de suite qui a payé.',
  },
};

export default guide;
