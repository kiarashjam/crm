import type { GuideContent } from '../types';

// Comment choisir un CRM pour un club, un établissement ou une PME en Suisse : partir des processus,
// une grille notée (données, e-mails, paiements suisses, nLPD, dépendance, IA, coût), le test,
// les questions aux éditeurs, les signaux d’alerte et un calendrier. Neutre ; CadenceE est une option.
const guide: GuideContent = {
  eyebrow: 'Choisir un logiciel',
  h1: 'Comment choisir un CRM pour un club ou une PME en Suisse',
  lead: 'Le bon CRM correspond à votre façon de travailler, à vos données, aux paiements et aux langues de la Suisse, à la protection des données et à votre budget sur plusieurs années. Voici une méthode, une grille notée, les questions à poser aux éditeurs, les signaux d’alerte et un calendrier réaliste.',
  readingMinutes: 9,
  keyTakeaways: [
    'Partez de votre façon de travailler, de la demande au paiement et au renouvellement, pas d’une liste de fonctionnalités.',
    'Limitez les critères indispensables et notez chaque produit avec la même grille pondérée.',
    'En Suisse, vérifiez facture QR et TWINT, export Bexio ou Abacus, français et allemand, et le respect de la nLPD.',
    'Testez avec vos propres scénarios et un échantillon de vos données, export compris, avant de signer.',
    'Comparez le coût total sur trois ans et prévoyez six à dix semaines pour décider.',
  ],
  sections: [
    {
      id: 'partir-de-vos-processus',
      heading: 'Partir de vos processus, pas des fonctionnalités',
      paragraphs: [
        'Un projet CRM déçoit quand on choisit le logiciel d’abord et qu’on découvre ses besoins ensuite. Chaque démonstration convainc, car elle montre l’idée que l’éditeur se fait de votre travail : décrivez donc votre façon de travailler avant de regarder un produit.',
      ],
      steps: [
        { title: 'Dessiner le parcours client', text: 'Un club : demande, visite, candidature, contrat, acompte, adhésion, renouvellement. Un établissement : demande, offre, contrat, événement, facture. Une équipe de vente : prospect, rendez-vous, offre, signature, paiement.' },
        { title: 'Lister les personnes et les rôles', text: 'Qui saisit, qui relance, qui valide, qui facture, qui consulte seulement.' },
        { title: 'Recenser les irritants', text: 'Demandes perdues, doublons, factures en retard, rapports refaits à la main chaque mois.' },
        { title: 'Écrire cinq à dix scénarios', text: 'De courtes histoires comme « un membre recommande un ami, qui visite, postule et paie un acompte ». Elles deviendront votre script de test.' },
      ],
      tip: 'Impliquez les personnes qui utiliseront le CRM chaque jour, pas seulement la direction : si l’accueil ou les vendeurs ne l’adoptent pas, le meilleur choix sur le papier échoue.',
    },
    {
      id: 'indispensable-ou-souhaitable',
      heading: 'Critères indispensables et critères souhaitables',
      paragraphs: [
        'Classez chaque exigence dans l’une de deux listes. Un critère indispensable est ce sans quoi vous ne pouvez pas travailler, ou qui découle d’une obligation légale ou contractuelle ; un critère souhaitable fait gagner du temps, mais vous pourriez vous en passer. Gardez une dizaine d’indispensables : avec trente, aucun produit ne passe. Exemples typiques :',
      ],
      bullets: [
        'Club privé : membres et foyers, candidatures, renouvellements, cotisations récurrentes et relevés.',
        'Établissement : demandes d’événements, contrats et acomptes, réservations, notes et lien avec la caisse.',
        'Petite équipe de vente : un pipeline clair, tâches et rappels, e-mails depuis le CRM, rapports simples.',
        'Tous en Suisse : les langues de l’équipe, les paiements suisses, un export comptable et une protection des données solide.',
      ],
    },
    {
      id: 'grille-de-choix-crm',
      heading: 'La grille de choix d’un CRM, avec notes',
      paragraphs: [
        'Pondérez chaque critère de 1 (souhaitable) à 3 (indispensable), notez chaque produit de 0 (absent) à 3 (répond pleinement au besoin) après l’avoir vu fonctionner, et multipliez. Un indispensable noté 0 élimine le produit, quel que soit son total.',
      ],
      subsections: [
        {
          heading: 'Modèle de données : prospects, membres, événements',
          paragraphs: [
            'Un CRM commercial pense en prospects, entreprises et affaires. Un club a aussi besoin de membres, de foyers, d’adhésions d’entreprise, de candidatures et de renouvellements ; un établissement, d’événements, de réservations et de listes d’attente. Des membres casés dans des « affaires » passent en démo, puis gênent chaque jour.',
          ],
        },
        {
          heading: 'E-mails et relances',
          paragraphs: [
            'E-mails individuels enregistrés sur la fiche, séquences de relance, newsletters, gestion des rebonds, consentement enregistré et lien de désinscription dans les e-mails marketing, comme l’exige le droit suisse. Notre guide sur l’[e-mail de relance commerciale](page:guide-sales-follow-up) détaille un bon processus.',
          ],
        },
        {
          heading: 'Paiements et spécificités suisses',
          paragraphs: [
            'La facture QR (qui a remplacé les bulletins de versement orange et rouges en 2022), TWINT, carte et SEPA, montants en francs suisses, rapprochement bancaire et un export que votre fiduciaire peut reprendre dans Bexio ou Abacus. Vérifiez les langues de l’interface personne par personne, italien compris si besoin. Voir aussi [la facture QR pour les clubs](page:guide-qr-bill).',
          ],
        },
        {
          heading: 'Protection des données, hébergement et accès',
          paragraphs: [
            'Avec la nLPD, en vigueur depuis septembre 2023, vous restez responsable des données de votre CRM. Un hébergement en Suisse n’est pas obligatoire, mais en règle générale les données ne peuvent aller que dans des pays à protection adéquate, comme ceux de l’UE et de l’EEE, ou avec des garanties telles que des clauses contractuelles types. Demandez où sont les données, qui sont les sous-traitants et si un contrat de sous-traitance est fourni, et vérifiez rôles, journal d’audit et double authentification. Voir notre [checklist nLPD](page:guide-fadp-checklist).',
          ],
        },
        {
          heading: 'Import, export et dépendance',
          paragraphs: [
            'L’arrivée doit être sûre : aperçu ou simulation, détection des doublons, annulation. Le départ doit être simple : toutes vos données dans un format ouvert comme le CSV, à tout moment, sans frais supplémentaires. Un CRM qu’on ne peut pas quitter peut augmenter ses prix.',
          ],
        },
        {
          heading: 'Intégrations',
          paragraphs: [
            'Listez ce qui doit être relié : formulaires du site, domaine e-mail et agenda, caisse ou système de réservation, comptabilité, signature électronique. Préférez ce qui fonctionne d’emblée à la promesse d’une API ; si une intégration exige un consultant, comptez-le dans le coût.',
          ],
        },
        {
          heading: 'Intelligence artificielle',
          paragraphs: [
            'Demandez ce que fait l’IA (rédiger, résumer, suggérer la prochaine étape), si elle peut modifier des fiches ou envoyer des e-mails sans confirmation humaine, quel fournisseur traite vos données et si on peut la désactiver.',
          ],
        },
        {
          heading: 'Coût total et modèles de prix',
          paragraphs: [
            'Les CRM se facturent par utilisateur, par contact, par palier de fonctionnalités ou par espace de travail, souvent avec des options pour le volume d’e-mails, l’IA ou des modules. Ajoutez mise en place, migration, formation, intégrations, temps interne et hausses après la première année, et comparez les totaux sur trois ans pour la taille visée.',
          ],
        },
        {
          heading: 'Support et éditeur',
          paragraphs: [
            'Qui répond en cas de panne, dans quelle langue, en combien de temps, et est-ce écrit dans le contrat ? Demandez à quel rythme le produit évolue et ce que deviennent vos données si l’éditeur est vendu ou ferme.',
          ],
        },
      ],
    },
    {
      id: 'tester-un-crm',
      heading: 'Tester un CRM avec des scénarios réels',
      paragraphs: [
        'La démonstration d’un éditeur montre le produit sous son meilleur jour ; votre test doit le montrer en train de faire votre travail. Retenez deux ou trois produits et passez-les au même script.',
      ],
      steps: [
        { title: 'Préparer le script', text: 'Transformez vos scénarios en étapes avec résultat attendu : « importer 50 membres », « envoyer un contrat et enregistrer l’acompte », « trouver les renouvellements du mois prochain ».' },
        { title: 'Prendre un échantillon de vos données', text: 'Quelques dizaines de fiches dans votre format réel, avec le moins de données personnelles possible.' },
        { title: 'Laisser les utilisateurs aux commandes', text: 'Ceux qui utiliseront le CRM chaque jour déroulent eux-mêmes les scénarios.' },
        { title: 'Tester la sortie', text: 'Exportez ce que vous avez importé et vérifiez que rien ne manque.' },
        { title: 'Noter le jour même', text: 'Remplissez la grille à chaud et notez chaque contournement.' },
      ],
      tip: 'Limitez le test à deux ou trois semaines. Si CadenceE fait partie de votre sélection, vous pouvez [essayer la démo](/login?demo=1), un espace avec des données d’exemple et sans compte, avant d’importer quoi que ce soit.',
    },
    {
      id: 'questions-aux-editeurs',
      heading: 'Les questions à poser aux éditeurs de CRM',
      paragraphs: ['Posez-les par écrit, pour comparer les réponses côte à côte :'],
      bullets: [
        'Où sont stockées nos données, et quels sous-traitants y ont accès ?',
        'Fournissez-vous un contrat de sous-traitance conforme à la nLPD et, si nécessaire, au RGPD ?',
        'Comment exporter toutes nos données, dans quel format et à quel coût ?',
        'Que comprend le prix, qu’est-ce qui est en sus, et quel est le délai de résiliation ?',
        'Lesquels de nos indispensables fonctionnent aujourd’hui, et lesquels sont seulement prévus ?',
        'En quelles langues sont l’interface, les e-mails et le support ?',
        'L’IA peut-elle modifier des données ou envoyer des messages sans confirmation humaine ?',
        'Qui nous aide à migrer, et peut-on annuler un import raté ?',
      ],
    },
    {
      id: 'signaux-d-alerte',
      heading: 'Les signaux d’alerte au moment de choisir',
      paragraphs: ['Aucun n’est forcément rédhibitoire, mais chacun mérite une réponse écrite avant de signer :'],
      bullets: [
        'Des indispensables qui sont « dans la feuille de route » ou « faisables avec l’API ».',
        'Aucun moyen d’exporter vous-même toutes vos données.',
        'Un rabais valable seulement si vous signez cette semaine.',
        'Des réponses floues sur le lieu de stockage ou les sous-traitants.',
        'Une longue durée minimale avec reconduction automatique.',
        'Une IA qui modifie des fiches ou envoie des e-mails sans confirmation.',
      ],
    },
    {
      id: 'calendrier-de-decision',
      heading: 'Un calendrier de décision réaliste',
      paragraphs: [
        'Pour un club, un établissement ou une petite équipe de vente, six à dix semaines entre la première étape et la signature sont réalistes. Désignez une personne responsable de la décision.',
      ],
      steps: [
        { title: 'Semaines 1 et 2 : processus et exigences', text: 'Parcours, scénarios, indispensables et fourchette de budget.' },
        { title: 'Semaine 3 : de la longue liste à la sélection', text: 'Comparez cinq ou six produits aux indispensables sur le papier ; gardez-en deux ou trois.' },
        { title: 'Semaines 4 à 6 : démonstrations et tests', text: 'Déroulez le script, recueillez les réponses écrites, notez.' },
        { title: 'Semaine 7 : références et contrat', text: 'Parlez à des clients, lisez le contrat et le contrat de sous-traitance, vérifiez les conditions de sortie.' },
        { title: 'Semaine 8 : décider et planifier', text: 'Choisissez, fixez une date de mise en service, planifiez migration et formation.' },
      ],
      tip: 'Comptez ensuite deux à six semaines de migration, selon l’état de vos données. Notre guide [passer d’Excel à un CRM](page:guide-excel-to-crm) explique comment les préparer.',
    },
    {
      id: 'la-place-de-cadencee',
      heading: 'Où se situe CadenceE',
      paragraphs: [
        'CadenceE est une option pour les clubs privés, l’hôtellerie-restauration et les équipes de vente en Suisse. Conçu en Suisse avec un club privé, il couvre le parcours de la demande au paiement sur une seule fiche : un [suivi des prospects en cinq phases](page:sales) jusqu’à l’acompte, des contrats signés en ligne, membres et foyers, événements et notes, et [les paiements et exports suisses](page:swiss) avec facture QR, TWINT, Bexio et Abacus.',
        'L’application existe en français, en allemand et en anglais, au choix de chacun ; les demandes d’export et d’effacement sont traitées selon la nLPD et le RGPD ; les listes s’exportent en CSV ; et l’assistant IA propose des modifications qu’une personne confirme. Ce n’est pas un logiciel de gestion hôtelière. Passez-le à la même grille que les autres, en commençant par ses [fonctionnalités](page:features).',
      ],
    },
  ],
  faq: {
    heading: 'Choisir un CRM : questions fréquentes',
    items: [
      { q: 'Comment choisir un CRM pour une PME ?', a: 'Décrivez votre parcours client du premier contact au paiement, fixez une courte liste d’indispensables, notez deux ou trois produits avec la même grille et testez-les avec vos scénarios et vos données.' },
      { q: 'Quels sont les principaux critères de choix d’un CRM ?', a: 'L’adéquation à vos données, la facilité d’utilisation, les e-mails et relances, les paiements et l’export comptable, la protection des données, l’export et la dépendance, les intégrations, le support et le coût total sur plusieurs années.' },
      { q: 'Une entreprise suisse doit-elle héberger son CRM en Suisse ?', a: 'Pas selon la loi. La nLPD admet un stockage dans des pays à protection adéquate, comme ceux de l’UE et de l’EEE, ou avec des garanties comme des clauses contractuelles types. Certains contrats ou secteurs exigent un hébergement suisse : vérifiez les vôtres.' },
      { q: 'Combien de temps faut-il pour choisir un CRM ?', a: 'Six à dix semaines pour une petite organisation, plus deux à six semaines pour migrer les données et former l’équipe.' },
      { q: 'Comment comparer les prix des CRM ?', a: 'Comparez les totaux sur trois ans pour le nombre d’utilisateurs et de contacts prévus, mise en place, migration, formation, options et hausses comprises, plutôt que le prix d’entrée par utilisateur.' },
      { q: 'Un club a-t-il besoin d’un CRM ou d’un logiciel de gestion des membres ?', a: 'Un club qui vend activement des adhésions a besoin des deux : un pipeline commercial et la gestion des membres. Un seul système fait d’un prospect un membre sans ressaisie. Voir [CRM pour club privé](page:private-clubs).' },
    ],
  },
  cta: {
    heading: 'Passez CadenceE à votre grille',
    text: 'Déroulez vos propres scénarios dans CadenceE, de la première demande au contrat signé, à l’acompte payé et au relevé mensuel d’un membre, et notez-le comme tout autre CRM de votre sélection.',
  },
};

export default guide;
