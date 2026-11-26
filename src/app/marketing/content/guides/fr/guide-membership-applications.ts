import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Admission des membres',
  h1: 'Le processus d’adhésion d’un club, étape par étape',
  lead: 'Comment admettre de nouveaux membres de façon juste et rapide : ce que doit contenir un formulaire d’adhésion, le rôle des parrains et du comité, comment dire oui, non ou pas encore, tenir une liste d’attente, puis transformer un contrat signé en un nouveau membre bien accueilli, avec des dossiers conformes à la nLPD.',
  readingMinutes: 9,
  keyTakeaways: [
    'Écrivez la procédure et les critères d’admission avant la prochaine candidature.',
    'Ne demandez que ce qui sert à décider ; laissez les données sensibles hors du formulaire.',
    'Répondez vite à chaque candidat, y compris ceux que vous refusez ou faites patienter.',
    'Faites signer le contrat et encaissez la finance d’entrée en une seule étape.',
    'L’accueil commence le jour de l’admission, pas à la première visite.',
  ],
  sections: [
    {
      id: 'pourquoi-c-est-important',
      heading: 'Pourquoi le processus d’adhésion compte',
      paragraphs: [
        'Pour un candidat, le processus d’adhésion est la première vraie expérience de votre club. Une procédure lente et opaque laisse penser que le club est mal organisé ; une procédure rapide et personnelle montre qu’on prendra soin de lui. Les meilleurs candidats ont souvent d’autres options, et ils n’attendront pas trois mois une réponse.',
        'Une procédure écrite protège aussi le club. Quand chaque candidature suit les mêmes étapes et les mêmes critères, les décisions sont cohérentes, le comité peut les expliquer, et personne n’entre, ou ne reste dehors, parce qu’il connaissait la bonne personne au bon moment.',
      ],
    },
    {
      id: 'les-etapes-en-bref',
      heading: 'Les étapes en un coup d’œil',
      paragraphs: ['Quelle que soit leur taille, la plupart des clubs suivent le même enchaînement :'],
      steps: [
        { title: 'Demande', text: 'Quelqu’un s’intéresse à l’adhésion, par votre site, par un membre ou lors d’une visite. Répondez dans un délai d’un jour ouvrable.' },
        { title: 'Rencontre', text: 'Une visite ou un entretien avec la personne responsable des membres, pour voir de part et d’autre si cela correspond.' },
        { title: 'Candidature', text: 'Le candidat remplit un formulaire court, idéalement en ligne.' },
        { title: 'Parrainage', text: 'Si vos règles le prévoient, un ou deux membres soutiennent la candidature.' },
        { title: 'Examen', text: 'Le comité d’admission examine le dossier selon des critères écrits.' },
        { title: 'Décision', text: 'Le candidat reçoit un oui, un non ou un pas encore, rapidement et avec courtoisie.' },
        { title: 'Contrat et finance d’entrée', text: 'Le nouveau membre signe le contrat et paie la finance d’entrée ou l’acompte.' },
        { title: 'Accueil', text: 'L’intégration commence : accès au portail, présentations et premier événement.' },
      ],
      tip: 'Fixez un objectif pour l’ensemble du parcours, par exemple une décision dans les quatre semaines suivant un dossier complet, et mesurez-le.',
    },
    {
      id: 'formulaire-d-adhesion',
      heading: 'Le formulaire d’adhésion : quoi demander, quoi éviter',
      paragraphs: [
        'Un bon formulaire d’adhésion de club tient sur un écran ou une page. Chaque champ doit avoir une raison d’être que vous pourriez expliquer au candidat.',
      ],
      subsections: [
        {
          heading: 'Ce qu’il faut demander',
          paragraphs: ['Ces champs suffisent à la plupart des clubs :'],
          bullets: [
            'Nom, e-mail, téléphone et adresse postale.',
            'Langue de correspondance souhaitée.',
            'Catégorie d’adhésion, et membres du foyer si elle couvre une famille.',
            'Profession ou entreprise, si elle a un lien avec l’objet du club.',
            'Qui l’a recommandé, et le nom de ses parrains.',
            'Quelques lignes sur sa motivation et ses centres d’intérêt.',
            'La confirmation qu’il a lu votre déclaration de confidentialité.',
          ],
        },
        {
          heading: 'Ce qu’il faut laisser de côté',
          paragraphs: [
            'La nLPD demande de ne collecter que le nécessaire et protège particulièrement certaines données sensibles : opinions religieuses, philosophiques, politiques ou syndicales, santé, origine ethnique, données génétiques et biométriques, poursuites et sanctions pénales ou administratives, mesures d’aide sociale. Une décision d’admission n’en a presque jamais besoin : gardez-les hors du formulaire. Il en va de même des coordonnées bancaires et des copies de pièces d’identité, qui peuvent attendre le moment où elles servent, et des revenus.',
          ],
        },
      ],
    },
    {
      id: 'parrains-et-references',
      heading: 'Parrains et références',
      paragraphs: [
        'Beaucoup de clubs privés demandent un parrain et un second parrain : des membres qui connaissent le candidat et se portent garants. Le parrainage fonctionne mieux quand il est léger et structuré. Plutôt qu’une lettre libre, posez trois questions courtes : depuis quand le parrain connaît le candidat, dans quel cadre, et pourquoi il pense que le club lui plaira et qu’il y apportera quelque chose.',
        'Notez qui a parrainé qui sur la fiche du candidat : cela aide le comité et montre plus tard quels membres amènent des personnes qui restent. Gardez les avis des parrains confidentiels au sein du comité, et rappelez-vous qu’un candidat peut demander l’accès aux données qui le concernent ; demandez à un conseiller comment cela s’applique aux références.',
      ],
    },
    {
      id: 'examen-par-le-comite',
      heading: 'L’examen par le comité et la décision',
      paragraphs: [
        'Qui admet les membres dépend de vos règles. Dans une association au sens du Code civil, les statuts désignent l’organe compétent ; dans un club exploité par une société, ce sont les conditions d’adhésion. Dans les deux cas, rendez la procédure explicite.',
      ],
      bullets: [
        'Écrivez les critères d’admission et transmettez-les à tous ceux qui examinent les dossiers.',
        'Réunissez-vous à un rythme connu, par exemple chaque mois, pour que les candidats sachent quand attendre une réponse.',
        'Demandez aux membres en conflit d’intérêts, comme un parrain, de se retirer de la décision.',
        'Consignez chaque décision, sa date et qui l’a prise.',
        'N’examinez que des dossiers complets : relancez avant la séance, pas pendant.',
      ],
    },
    {
      id: 'communiquer-la-decision',
      heading: 'Dire oui, non ou pas encore',
      paragraphs: [
        'Communiquez la décision dans les jours qui suivent la séance. Une admission contient tout ce qu’il faut pour la suite : le contrat à signer, la finance d’entrée et la date de début de l’adhésion. Si possible, appelez le candidat : c’est un moment dont on se souvient.',
        'Un refus mérite la même courtoisie. Restez bref et respectueux, remerciez le candidat, et prévenez ses parrains avant qu’ils ne l’apprennent ailleurs. Motiver ou non un refus relève de vos règles ; quel que soit votre choix, appliquez-le de façon constante. Si la question est le moment ou la capacité plutôt que le profil, proposez un report ou une place sur la liste d’attente.',
      ],
    },
    {
      id: 'liste-d-attente',
      heading: 'Tenir une liste d’attente équitable',
      paragraphs: [
        'Quand une catégorie est complète, une liste d’attente garde l’intérêt des bons candidats. Elle reste équitable si ses règles sont écrites et respectées :',
      ],
      bullets: [
        'Fixez l’ordre : en général la date de l’avis favorable du comité, parfois par catégorie.',
        'Dites aux candidats où ils en sont, à peu près, et à quel rythme des places se libèrent.',
        'Donnez des nouvelles à chacun au moins une ou deux fois par an.',
        'Si vous demandez un acompte pour réserver une place, précisez par écrit quand il est remboursé.',
      ],
    },
    {
      id: 'contrat-et-signature',
      heading: 'Contrat, finance d’entrée et signature électronique',
      paragraphs: [
        'Le contrat d’adhésion transforme les attentes en engagements. Il précise la catégorie, la cotisation annuelle et sa facturation, la durée, le renouvellement et le préavis, le règlement ou les statuts, ainsi que la finance d’entrée et la part éventuellement remboursable.',
        'En droit suisse, la plupart des contrats ne sont soumis à aucune forme particulière : un contrat signé en ligne est donc en général valable. Lorsque la loi exige la forme écrite, seule la signature électronique qualifiée remplace la signature manuscrite. Un contrat d’adhésion n’y est en principe pas soumis, mais faites vérifier votre cas par un conseiller.',
        'Le plus simple est d’encaisser la finance d’entrée à la signature, par carte ou par facture QR, plutôt que d’envoyer une facture une semaine plus tard. C’est ce que fait CadenceE avec ses [contrats signés en ligne](page:e-signature) : le candidat signe sans créer de compte et paie l’acompte dans la même étape. Pour le paiement, voir notre guide de la [facture QR](page:guide-qr-bill).',
      ],
    },
    {
      id: 'accueil-et-integration',
      heading: 'Accueil et intégration des nouveaux membres',
      paragraphs: [
        'L’intégration commence le jour de la signature. Envoyez un message de bienvenue avec les informations pratiques, ouvrez l’accès au portail membre, présentez le nouveau venu à l’équipe et à quelques membres aux intérêts communs, et invitez-le à un événement dans son premier mois. Prenez de ses nouvelles après deux mois environ.',
        'Un membre qui se sent chez lui dès la première année a toutes les raisons de renouveler. Nos guides [gérer un club privé](page:guide-run-a-club) et [renouvellement et fidélisation](page:guide-member-retention) couvrent la suite.',
      ],
    },
    {
      id: 'dossiers-et-confidentialite',
      heading: 'Tenue des dossiers et confidentialité',
      paragraphs: [
        'Une candidature contient des données personnelles, et la nLPD s’applique dès leur collecte. Indiquez dans votre déclaration de confidentialité ce que vous collectez, pourquoi et pour combien de temps. Réservez l’accès aux candidatures à ceux qui les examinent, et fixez un délai court après lequel les candidatures non retenues sont effacées.',
        'Le dossier d’un candidat admis rejoint le fichier des membres. Les factures et paiements liés à la finance d’entrée et aux cotisations sont des pièces comptables, conservées en général dix ans en Suisse. Notre [checklist nLPD pour les clubs](page:guide-fadp-checklist) couvre le reste ; pour votre cas, consultez un spécialiste de la protection des données.',
      ],
    },
    {
      id: 'outils',
      heading: 'Des outils pour que les candidatures avancent',
      paragraphs: [
        'Gérées par e-mail et tableur, les candidatures s’enlisent : la réponse d’un parrain dort dans une boîte de réception, et personne ne sait qui doit rappeler le candidat. Un outil pensé pour cela garde chaque candidature dans une seule liste, avec son statut et son historique.',
        'Dans CadenceE, un formulaire de candidature sur votre site alimente une liste unique, protégée contre les doublons. Chaque fiche indique qui a recommandé le candidat, le contrat part en signature électronique avec l’acompte, et une seule étape fait du candidat un membre actif, avec e-mail de bienvenue et accès au portail. Voir la [gestion des membres](page:membership) et le [CRM pour club privé](page:private-clubs), ou [essayez la démo](/login?demo=1).',
      ],
    },
  ],
  faq: {
    heading: 'Processus d’adhésion : questions fréquentes',
    items: [
      {
        q: 'Combien de temps doit durer un processus d’adhésion ?',
        a: 'Visez une décision dans les quelques semaines qui suivent un dossier complet. Répondez aux demandes en un jour ouvrable, réunissez le comité à un rythme connu et dites aux candidats quand attendre une réponse.',
      },
      {
        q: 'Que doit contenir un formulaire d’adhésion de club ?',
        a: 'Coordonnées, langue souhaitée, catégorie d’adhésion, membres du foyer le cas échéant, parrains, quelques lignes de motivation et la confirmation de votre déclaration de confidentialité. Laissez de côté les données sensibles comme la santé, la religion ou les opinions politiques.',
      },
      {
        q: 'Faut-il des parrains pour entrer dans un club ?',
        a: 'Seulement si vos règles le prévoient. Beaucoup de clubs privés demandent un ou deux membres pour soutenir chaque candidature ; quelques questions courtes et structurées valent mieux qu’une lettre libre.',
      },
      {
        q: 'Faut-il motiver le refus d’une candidature ?',
        a: 'Cela dépend de vos statuts ou de vos conditions d’adhésion. Beaucoup de clubs restent brefs. Quelle que soit votre règle, appliquez-la de façon constante, restez courtois et faites vérifier la situation juridique par un conseiller.',
      },
      {
        q: 'Une signature électronique est-elle valable pour un contrat d’adhésion en Suisse ?',
        a: 'En général, oui : la plupart des contrats suisses ne sont soumis à aucune forme particulière. Lorsque la loi exige la forme écrite, seule la signature électronique qualifiée remplace la signature manuscrite ; vérifiez votre cas avec un conseiller.',
      },
      {
        q: 'Combien de temps garder les données des candidats refusés ?',
        a: 'La nLPD ne fixe pas de durée : seulement le temps nécessaire. Fixez un délai court dans vos règles, mentionnez-le dans votre déclaration de confidentialité et effacez les données à son terme.',
      },
    ],
  },
  cta: {
    heading: 'De la candidature au membre, sur une seule fiche',
    text: 'CadenceE reçoit les candidatures en ligne, envoie le contrat en signature électronique avec l’acompte et fait du candidat un membre actif en une étape.',
  },
};

export default guide;
