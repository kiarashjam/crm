import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'S’organiser',
  h1: 'Passer d’Excel à un CRM : quand et comment remplacer vos tableurs',
  lead: 'Une comparaison honnête entre Excel et un CRM, les signes qu’un tableur ne suffit plus à votre club ou à votre équipe de vente, et un plan sur deux semaines pour changer sans rien perdre.',
  readingMinutes: 9,
  keyTakeaways: [
    'Un tableur suffit à une personne et quelques dizaines de contacts ; il peine dès que plusieurs personnes le partagent et que les relances comptent.',
    'Les signes d’alerte : doublons, versions concurrentes, aucun historique, relances qui ne tiennent qu’à la mémoire.',
    'Nettoyez avant d’importer : une ligne par personne, une colonne par information, des formats cohérents et un consentement noté explicitement.',
    'Importez avec une simulation d’abord et la possibilité d’annuler ; migrez d’abord les prospects ouverts et les membres actuels, les archives ensuite.',
    'Prévoyez deux semaines, un responsable, et vérifiez que vous pouvez tout réexporter avant de vous engager.',
  ],
  sections: [
    {
      id: 'signes-qu-excel-ne-suffit-plus',
      heading: 'Les signes qu’Excel ne suffit plus',
      paragraphs: [
        'Presque tous les clubs et équipes de vente commencent avec un tableur, et c’est raisonnable : Excel est sur chaque ordinateur et tout le monde le connaît. Les ennuis commencent quand le fichier devient l’endroit où vit l’entreprise.',
      ],
      bullets: [
        'Doublons : la même personne apparaît trois fois, écrite de trois façons, avec trois numéros différents',
        'Versions : « Membres_FINAL_v3_Anne.xlsx » et « Membres_FINAL_nouveau.xlsx » coexistent, et personne ne sait lequel fait foi',
        'Aucun historique : impossible de savoir qui a changé un numéro, quand a eu lieu le dernier appel, ni ce qui a été promis',
        'Aucun rappel : les relances dépendent de la mémoire, et un prospect chaud refroidit en deux semaines chargées',
        'Fichier partagé fragile : un tri sur une seule colonne mélange les lignes, un onglet disparaît, ou le fichier est écrasé',
        'Protection des données : toute la liste des membres circule en pièce jointe et dort sur des portables, sans contrôle de qui voit quoi',
      ],
      tip: 'Si trois de ces points vous parlent, le tableur vous coûte déjà des prospects, des paiements ou du temps. Préparez le changement maintenant, avant qu’un fichier perdu ne vous l’impose.',
    },
    {
      id: 'ce-qu-apporte-un-crm',
      heading: 'Ce qu’un CRM apporte à la place',
      paragraphs: [
        'Un CRM garde une fiche par personne et par entreprise, et y rattache tout : appels, e-mails, rendez-vous, contrats, paiements et notes. Au lieu d’une ligne, un historique fiable : chaque collègue reprend là où l’autre s’est arrêté.',
        'Chaque prospect a une phase et une prochaine étape datée : le CRM vous la rappelle à la place de votre mémoire. Les doublons sont repérés à la saisie et à l’import, chaque modification est enregistrée avec son auteur, et l’accès dépend du rôle de chacun : le personnel du bar ne voit pas ce que voit le comité d’admission.',
        'Dans CadenceE, par exemple, le [CRM commercial](page:sales) suit chaque prospect en cinq phases, de la prise de contact à l’acompte, et la [gestion des membres](page:membership) garde membres, foyers et renouvellements sur les mêmes fiches. Les e-mails partent du CRM et sont enregistrés sur la fiche du destinataire.',
      ],
    },
    {
      id: 'excel-ou-crm',
      heading: 'Excel ou CRM : la comparaison honnête',
      paragraphs: [
        'Un CRM n’est pas meilleur par principe : il demande mise en place et discipline, et une équipe qui ne l’ouvre jamais n’y gagne rien. La vraie question : votre travail a-t-il dépassé ce qu’une grille peut contenir ?',
      ],
      subsections: [
        {
          heading: 'Quand un tableur suffit encore',
          paragraphs: [
            'Une seule personne qui gère quelques dizaines de contacts, une liste d’invités ponctuelle, un budget ou une analyse rapide de chiffres : Excel fait tout cela très bien. Beaucoup d’équipes le gardent d’ailleurs pour analyser des exports du CRM : un tableau croisé dynamique reste imbattable pour une question inhabituelle.',
          ],
        },
        {
          heading: 'Quand un CRM en vaut la peine',
          paragraphs: [
            'Plusieurs personnes travaillent sur les mêmes contacts, les relances font la différence, vous devez savoir ce qui s’est passé avant d’appeler quelqu’un, ou vous détenez des données personnelles à protéger et à communiquer sur demande. Dès que deux conditions sont réunies, le tableur partagé devient un risque.',
          ],
        },
      ],
    },
    {
      id: 'preparer-vos-donnees',
      heading: 'Préparer vos données',
      paragraphs: [
        'La plupart des migrations ratées échouent ici, pas dans le logiciel : une heure de nettoyage épargne une semaine de corrections fiche par fiche.',
      ],
      steps: [
        { title: 'Rassembler toutes les listes', text: 'Fichier des ventes, liste des membres, export de la newsletter, fichier du trésorier, contacts de la messagerie : tout côte à côte avant de décider.' },
        { title: 'Choisir vos colonnes', text: 'Prénom, nom, e-mail, téléphone, entreprise, langue, statut, provenance, responsable, prochaine étape et sa date. Une information par colonne, rien de fusionné.' },
        { title: 'Nettoyer les formats', text: 'Prénom et nom séparés, espaces en trop supprimés, un e-mail par cellule, numéros au format +41 79 123 45 67, un seul format de date, rue, numéro, NPA et localité séparés.' },
        { title: 'Supprimer les doublons', text: 'Triez par e-mail, puis par nom et NPA. Décidez quelle ligne l’emporte et fusionnez les notes, plutôt que de supprimer à l’aveugle.' },
        { title: 'Noter le consentement', text: 'Ajoutez une colonne pour l’accord à la newsletter, avec sa date et sa source. Si vous ne savez pas si une personne a accepté, ne le supposez pas.' },
        { title: 'Laisser de côté l’inutile', text: 'Vieux prospects sans contact depuis des années et données injustifiables restent mieux derrière vous.' },
      ],
      tip: 'Avant tout nettoyage, enregistrez une copie intacte de chaque fichier d’origine, en lecture seule : en cas d’erreur, vous repartez de zéro.',
    },
    {
      id: 'l-import',
      heading: 'L’import : correspondance des colonnes, simulation et annulation',
      paragraphs: [
        'Enregistrez chaque feuille nettoyée en CSV UTF-8, pour que les é, les ü et les ç survivent. Faites ensuite correspondre chaque colonne à un champ du CRM : « Natel » vers le téléphone, « Remarques » vers une note. Une colonne sans champ correspondant devient une note ou disparaît, mais par choix.',
        'N’importez jamais un fichier entier à l’aveugle. Une simulation montre combien de fiches seront créées, mises à jour ou ignorées comme doublons. Vérifiez dix fiches à la main par rapport à l’original, dont celle au nom le plus compliqué, puis lancez l’import réel, en sachant que vous pouvez l’annuler.',
        'Dans CadenceE, prospects, contacts, entreprises et membres s’importent depuis un tableur. Chaque import commence par une simulation qui montre ce qui va se passer, la protection contre les doublons s’applique aux prospects importés, et un import peut être annulé.',
      ],
    },
    {
      id: 'quoi-migrer-en-premier',
      heading: 'Quoi migrer en premier',
      paragraphs: [
        'Commencez par ce dont l’équipe a besoin lundi matin, pas par dix ans d’historique :',
      ],
      bullets: [
        'Les prospects et affaires en cours, chacun avec son responsable et sa prochaine étape',
        'Les membres ou clients actuels, avec leur foyer ou leur entreprise',
        'Les entreprises et partenaires avec qui vous travaillez',
        'Un résumé des contacts passés : la dernière date et une courte note, plutôt que chaque ancien appel',
        'Les archives, seulement si vous en avez vraiment besoin, et une fois que tout le reste fonctionne',
      ],
    },
    {
      id: 'faire-adopter-le-crm',
      heading: 'Faire adopter le CRM par l’équipe',
      paragraphs: [
        'Un logiciel ne change pas les habitudes à lui seul : les équipes qui réussissent s’accordent sur quelques règles pour le premier mois.',
      ],
      bullets: [
        'Nommer un responsable qui décide des champs, répond aux questions et corrige les erreurs',
        'Convenir que ce qui n’est pas dans le CRM n’a pas eu lieu',
        'Passer l’ancien tableur en lecture seule le jour du changement, pour n’avoir qu’un seul endroit où travailler',
        'Former chacun sur ses propres prospects et membres, pas sur des données d’exemple',
        'Donner à chacun une vue enregistrée de sa liste et un coup d’œil quotidien sur ce qui est dû',
        'Laisser chacun travailler dans sa langue : CadenceE fonctionne en français, en allemand et en anglais',
      ],
      tip: 'Une courte routine du matin ancre l’habitude. Dans CadenceE, l’[assistant IA](page:ai) prépare chaque matin un brief de ce qui demande votre attention, et les bonnes pratiques de relance sont détaillées dans le guide des [e-mails de relance commerciale](page:guide-sales-follow-up).',
    },
    {
      id: 'plan-sur-deux-semaines',
      heading: 'Un plan sur deux semaines',
      paragraphs: [
        'Deux semaines suffisent à un club ou à une petite équipe, si une personne porte le projet.',
      ],
      steps: [
        { title: 'Jours 1 et 2 : décider', text: 'Nommez le responsable, listez tous les tableurs en usage et mettez-vous d’accord sur les champs et les phases.' },
        { title: 'Jours 3 à 5 : nettoyer', text: 'Formats, doublons, consentement, et tri de ce que vous ne migrerez pas.' },
        { title: 'Jour 6 : tester', text: 'Importez cinquante lignes avec une simulation, vérifiez-les à la main, ajustez la correspondance, puis annulez le test.' },
        { title: 'Jour 7 : configurer', text: 'Invitez les collègues avec leurs rôles, préparez les modèles d’e-mail et les vues enregistrées.' },
        { title: 'Jour 8 : importer', text: 'Lancez l’import complet, contrôlez quelques fiches et passez les tableurs en lecture seule.' },
        { title: 'Jours 9 et 10 : former', text: 'Montrez à chacun ses propres fiches et la routine quotidienne.' },
        { title: 'Jours 11 à 14 : travailler et corriger', text: 'Travaillez uniquement dans le CRM, notez les questions, corrigez les erreurs et faites le point ensemble en fin de semaine.' },
      ],
    },
    {
      id: 'reexporter-vos-donnees',
      heading: 'Réexporter vos données : pas de dépendance',
      paragraphs: [
        'Avant de vous engager, testez la sortie. Vous devez pouvoir exporter vos listes en CSV à tout moment, remettre un export comptable à votre fiduciaire et transmettre à une personne ce que vous détenez sur elle, comme l’exige la nLPD. Un système qui rend le départ difficile en dit long.',
        'Dans CadenceE, les listes comme les prospects et les activités s’exportent en CSV, l’export comptable part vers Bexio ou Abacus, et une demande d’accès exporte tout ce qui concerne une personne. Les critères à vérifier avant de choisir sont dans [comment choisir un CRM](page:guide-choose-a-crm), et la protection des données dans la [check-list nLPD pour les clubs](page:guide-fadp-checklist). Les clubs qui facturent leurs membres liront aussi le guide de la [facture QR](page:guide-qr-bill), qui exige des adresses structurées. Pour voir vos listes dans un vrai CRM, [essayez la démo](/login?demo=1) ou parcourez toutes les [fonctionnalités](page:features).',
      ],
    },
  ],
  faq: {
    heading: 'Excel ou CRM : questions fréquentes',
    items: [
      { q: 'Excel est-il un CRM ?', a: 'Non. Excel peut contenir une liste de contacts, mais sans historique par personne, sans rappels, sans protection contre les doublons et sans contrôle de qui voit ou modifie quoi : précisément ce pour quoi un CRM est conçu.' },
      { q: 'Quand une PME doit-elle remplacer Excel par un CRM ?', a: 'Quand plusieurs personnes travaillent sur les mêmes contacts, quand les relances décident des ventes ou des renouvellements, ou quand vous détenez des données personnelles à protéger. Une personne seule avec une courte liste peut rester sur un tableur.' },
      { q: 'Combien de temps faut-il pour passer d’Excel à un CRM ?', a: 'Pour un club ou une petite équipe de vente, environ deux semaines : nettoyage, import de test, import complet, puis une semaine de travail dans le CRM avec une personne qui corrige au fil de l’eau.' },
      { q: 'Risque-t-on de perdre des données à l’import ?', a: 'Pas si vous gardez une copie de l’original en lecture seule, faites correspondre chaque colonne volontairement et lancez d’abord une simulation, dans un CRM où un import peut être annulé.' },
      { q: 'Peut-on garder Excel à côté du CRM ?', a: 'Oui, pour l’analyse, sur des exports du CRM. Mais ne modifiez pas les contacts aux deux endroits, sinon vous retrouverez deux versions de la vérité.' },
      { q: 'Une liste de membres Excel partagée pose-t-elle problème avec la nLPD ?', a: 'La loi n’interdit pas les tableurs, mais elle exige une sécurité adaptée et la capacité de répondre aux demandes des personnes. Un fichier qui circule par e-mail sans contrôle d’accès rend les deux difficiles. Voir la [check-list nLPD pour les clubs](page:guide-fadp-checklist).' },
    ],
  },
  cta: {
    heading: 'Quittez le tableur, gardez les données',
    text: 'CadenceE importe vos prospects, contacts et membres depuis un tableur, avec une simulation d’abord et une annulation possible ensuite, et réexporte vos listes en CSV quand vous en avez besoin.',
  },
};

export default guide;
