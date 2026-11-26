import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Protection des données',
  h1: 'Checklist nLPD pour les clubs, les associations et les PME',
  lead: 'Depuis le 1er septembre 2023, la loi sur la protection des données révisée (nLPD) s’applique à chaque club, association et PME de Suisse qui traite des données personnelles. Cette checklist vous guide point par point, avec l’article correspondant et l’aide qu’apporte un CRM. Informations générales, pas un conseil juridique.',
  readingMinutes: 9,
  keyTakeaways: [
    'La nLPD s’applique à toute association, tout club et toute entreprise qui traite des données personnelles, quelle que soit sa taille.',
    'Moins de 250 collaborateurs : en général pas de registre des traitements, mais une déclaration de protection des données et une sécurité adaptée.',
    'Une demande d’accès doit en principe recevoir une réponse dans les 30 jours, sans frais.',
    'Une violation de la sécurité qui présente un risque élevé s’annonce au PFPDT dans les meilleurs délais.',
    'Les e-mails publicitaires exigent un consentement préalable, l’expéditeur exact et une désinscription simple.',
  ],
  sections: [
    {
      id: 'a-qui-s-applique-la-nlpd',
      heading: 'À qui s’applique la nLPD',
      paragraphs: [
        'La loi fédérale sur la protection des données (LPD) du 25 septembre 2020 est entrée en vigueur le 1er septembre 2023 avec son ordonnance (OPDo). Elle s’applique aux personnes privées, y compris les associations, fondations et entreprises de toute taille, ainsi qu’aux organes fédéraux. Elle protège les personnes physiques : un membre, un hôte ou l’interlocuteur d’un fournisseur, mais pas une entreprise en tant que telle. Elle vise aussi les traitements à l’étranger qui déploient des effets en Suisse (art. 3).',
        'Le RGPD européen ne s’applique pas directement en Suisse, mais il peut s’appliquer à une organisation suisse qui propose des biens ou des services à des personnes dans l’UE ou observe leur comportement, par exemple un club qui recrute activement en France.',
        'Certaines violations intentionnelles (ne pas informer, répondre faussement à une demande d’accès, sous-traiter sans précaution, communiquer à l’étranger sans garanties, ignorer la sécurité minimale) sont punissables, sur plainte, d’une amende de 250 000 francs au plus pour les personnes responsables (art. 60 et 61).',
      ],
    },
    {
      id: 'inventaire-des-donnees',
      heading: '1. Dressez l’inventaire des données personnelles',
      paragraphs: [
        'Commencez par un tableau simple : quelles données, sur qui, dans quel but, où, qui y accède, à qui elles sont transmises et combien de temps vous les gardez. Groupes typiques : membres et foyers, candidats, hôtes, abonnés, clients, collaborateurs, bénévoles, interlocuteurs des fournisseurs.',
      ],
      subsections: [
        {
          heading: 'Attention aux données sensibles',
          paragraphs: [
            'Les données sensibles bénéficient d’une protection renforcée (art. 5) : santé, opinions religieuses, philosophiques ou politiques, origine ethnique, données génétiques et biométriques, poursuites pénales, aide sociale. Une allergie notée pour un repas ou une blessure enregistrée par un club sportif sont des données de santé. Si vous vous fondez sur le consentement pour les traiter, il doit être exprès (art. 6 al. 7), et les communiquer à des tiers exige un motif justificatif (art. 30 et 31).',
          ],
        },
      ],
      tip: 'N’oubliez pas ce qui est hors de votre système principal : tableurs, formulaires papier, boîtes e-mail, groupes de messagerie, exports envoyés à la fiduciaire.',
    },
    {
      id: 'finalites-et-declaration',
      heading: '2. Définissez les finalités et publiez une déclaration de protection des données',
      paragraphs: [
        'Ne collectez des données que dans un but reconnaissable pour la personne, et utilisez-les de manière compatible avec ce but (art. 6). Contrairement au RGPD, la LPD n’exige pas de base légale pour chaque traitement. Un motif justificatif (consentement, intérêt prépondérant ou loi) n’est nécessaire qu’en cas d’atteinte à la personnalité, par exemple si la personne s’est opposée au traitement (art. 30 et 31) ; gérer le contrat d’un membre est un intérêt prépondérant typique.',
        'Lors de la collecte, vous devez informer les personnes (art. 19), sous une forme précise, compréhensible et facilement accessible (art. 13 OPDo). Votre déclaration indique au moins :',
      ],
      bullets: [
        'qui vous êtes et comment vous contacter ;',
        'la finalité du traitement et les destinataires ;',
        'en cas de communication à l’étranger, les pays et les garanties ;',
        'si les données ne viennent pas de la personne, les catégories de données traitées.',
      ],
    },
    {
      id: 'registre-des-activites',
      heading: '3. Vérifiez si vous devez tenir un registre des activités de traitement',
      paragraphs: [
        'Le registre (art. 12) décrit pour chaque activité la finalité, les catégories de personnes et de données, les destinataires, la durée de conservation, la sécurité et les communications à l’étranger.',
        'L’exception est précise (art. 24 OPDo) : les entreprises et autres organismes de droit privé employant moins de 250 collaborateurs au 1er janvier en sont dispensés, sauf s’ils traitent des données sensibles à grande échelle ou pratiquent un profilage à risque élevé. Ce sont les collaborateurs qui comptent, pas les membres ni les bénévoles : une association de 3000 membres et 20 employés est donc en général dispensée.',
      ],
      tip: 'Même dispensé, tenez à jour le tableau de l’étape 1 : il nourrit votre déclaration et accélère la réponse à une demande ou une violation.',
    },
    {
      id: 'securite-des-donnees',
      heading: '4. Sécurisez les données',
      paragraphs: [
        'La sécurité doit être adaptée au risque (art. 8). L’ordonnance demande notamment que chacun n’accède qu’aux données dont il a besoin, qu’on puisse retracer qui a saisi ou modifié quoi, que les données puissent être restaurées après un incident et que les logiciels soient à jour (art. 3 OPDo). Concrètement :',
      ],
      bullets: [
        'Des comptes personnels plutôt que des identifiants partagés, avec double authentification.',
        'Des rôles : le bar n’a pas besoin de l’historique de paiement des membres.',
        'Les accès retirés le jour du départ d’un bénévole ou d’un collaborateur.',
        'Appareils chiffrés, sauvegardes régulières, formulaires papier sous clé.',
      ],
    },
    {
      id: 'sous-traitants-et-contrats',
      heading: '5. Encadrez vos sous-traitants par contrat',
      paragraphs: [
        'CRM, service d’e-mail, prestataire de paiement et stockage en ligne sont des sous-traitants. Vous pouvez leur confier des données par contrat s’ils les traitent seulement comme vous pourriez le faire, après vous être assuré qu’ils garantissent leur sécurité (art. 9). Ils ne peuvent sous-traiter à leur tour qu’avec votre autorisation préalable (art. 9 al. 3). Signez un contrat de sous-traitance avec chacun et notez où ils hébergent les données.',
      ],
    },
    {
      id: 'communication-a-l-etranger',
      heading: '6. Contrôlez les communications à l’étranger',
      paragraphs: [
        'Les données peuvent aller vers les pays reconnus adéquats par le Conseil fédéral (annexe 1 OPDo) : les États de l’UE et de l’EEE, le Royaume-Uni et, depuis le 15 septembre 2024, les entreprises américaines qui participent au Swiss-US Data Privacy Framework. Ailleurs, il faut des garanties, comme des clauses types reconnues par le PFPDT (art. 16), ou une exception, comme le consentement exprès ou l’exécution d’un contrat (art. 17). Citez les pays dans votre déclaration.',
      ],
    },
    {
      id: 'droits-des-personnes',
      heading: '7. Préparez-vous aux demandes d’accès, de remise et d’effacement',
      paragraphs: [
        'Toute personne peut demander si vous traitez des données la concernant (art. 25). La réponse comprend les données elles-mêmes, la finalité, la durée de conservation, les destinataires et l’origine. Elle est en principe due dans les 30 jours et sans frais ; en cas d’effort disproportionné, une participation de 300 francs au plus peut être demandée, annoncée à l’avance (art. 18 et 19 OPDo). Vérifiez l’identité, puis cherchez partout : CRM, tableurs, e-mails, papier.',
        'Les personnes peuvent aussi demander la remise des données qu’elles vous ont fournies dans un format électronique courant (art. 28), la rectification de données inexactes, et s’opposer au traitement ou demander l’effacement ; vous ne pouvez alors continuer qu’avec un motif justificatif (art. 30 à 32).',
      ],
    },
    {
      id: 'conservation-et-effacement',
      heading: '8. Fixez des durées de conservation et effacez à temps',
      paragraphs: [
        'Les données doivent être détruites ou anonymisées dès qu’elles ne sont plus nécessaires au but du traitement (art. 6 al. 4). Fixez une durée par catégorie, par exemple :',
      ],
      bullets: [
        'Pièces comptables, factures et paiements : dix ans (art. 958f CO, applicable aux associations par l’art. 69a CC).',
        'Candidats non admis : le temps nécessaire à la décision.',
        'Anciens membres : ce qu’exige la comptabilité et, s’ils l’acceptent, ce qu’il faut pour garder le contact.',
        'Personnes désinscrites : uniquement ce qui garantit qu’elles ne reçoivent plus rien.',
      ],
    },
    {
      id: 'violations-de-la-securite',
      heading: '9. Sachez réagir à une violation de la sécurité des données',
      paragraphs: [
        'Un ordinateur volé avec la liste des membres, une boîte e-mail piratée ou un tableur envoyé à la mauvaise adresse sont des violations de la sécurité des données. Si elles entraînent vraisemblablement un risque élevé pour les personnes concernées, annoncez-les au PFPDT dans les meilleurs délais via son formulaire en ligne, avec la nature de la violation, ses conséquences et les mesures prises (art. 24) ; n’attendez pas la fin d’une longue enquête, les détails peuvent suivre. Vos sous-traitants doivent vous signaler toute violation dans les meilleurs délais.',
        'Informez les personnes concernées si leur protection l’exige ou si le PFPDT le demande. Documentez la violation, ses effets et vos mesures, et conservez cette documentation au moins deux ans (art. 15 OPDo).',
      ],
    },
    {
      id: 'e-mails-publicitaires-et-consentement',
      heading: '10. Envoyez les e-mails publicitaires avec consentement',
      paragraphs: [
        'Les e-mails et SMS publicitaires relèvent de la loi contre la concurrence déloyale (art. 3 al. 1 let. o LCD) : il faut le consentement préalable du destinataire, l’indication de l’expéditeur exact et une possibilité simple de refus, sans frais. Exception : un client qui vous a donné ses coordonnées lors d’un achat et a été informé de son droit de refus peut recevoir de la publicité pour vos propres produits ou services similaires.',
        'La convocation à l’assemblée générale ou le relevé d’un membre ne sont pas de la publicité ; les promotions et les newsletters à des non-membres en sont. Notez quand et comment chacun a consenti, et respectez une désinscription dans toutes les listes.',
      ],
    },
    {
      id: 'comment-un-crm-aide',
      heading: 'Comment un CRM aide à respecter la nLPD',
      paragraphs: [
        'La plupart des problèmes viennent des copies dispersées : le même membre dans quatre tableurs, un vieil export dans une boîte e-mail. Notre guide pour [passer d’Excel à un CRM](page:guide-excel-to-crm) explique comment tout réunir.',
        'Dans CadenceE, rôles et autorisations limitent qui voit quoi, un journal d’audit enregistre chaque modification, la connexion accepte clés d’accès et double authentification, le consentement est enregistré avec désinscription en un clic, et les demandes d’accès et d’effacement exportent ou effacent les données d’une personne, selon la nLPD et le RGPD. Voir le [CRM suisse](page:swiss) et la [gestion des membres](page:membership), ou [essayez la démo](/login?demo=1). Voir aussi notre guide sur la [fidélisation des membres](page:guide-member-retention).',
      ],
    },
  ],
  faq: {
    heading: 'nLPD pour clubs et associations : questions fréquentes',
    items: [
      {
        q: 'La nLPD s’applique-t-elle aux petites associations et aux clubs ?',
        a: 'Oui, à toute organisation privée qui traite des données personnelles, quelles que soient sa taille et sa forme juridique. La taille ne compte que pour quelques obligations, comme le registre.',
      },
      {
        q: 'Notre association doit-elle tenir un registre des activités de traitement ?',
        a: 'En général non, si elle emploie moins de 250 collaborateurs au 1er janvier et ne traite ni données sensibles à grande échelle ni profilage à risque élevé (art. 24 OPDo). Les membres et les bénévoles ne sont pas des collaborateurs.',
      },
      {
        q: 'Faut-il le consentement des membres pour enregistrer leurs données ?',
        a: 'Pas pour gérer l’adhésion, à condition que votre déclaration de protection des données les informe. Le consentement compte pour les e-mails publicitaires, et doit être exprès si vous vous fondez sur lui pour traiter des données sensibles comme la santé.',
      },
      {
        q: 'Quand faut-il annoncer une violation de données au PFPDT ?',
        a: 'Dans les meilleurs délais, lorsqu’elle entraîne vraisemblablement un risque élevé pour les personnes concernées. En dessous de ce seuil, l’annonce n’est pas obligatoire, mais une note sur les faits et votre appréciation est une bonne pratique.',
      },
      {
        q: 'Faut-il nommer un conseiller à la protection des données ?',
        a: 'Non, c’est facultatif pour les organisations privées (art. 10). Désigner une personne qui traite les demandes et tient cette checklist à jour reste une bonne idée.',
      },
    ],
  },
  cta: {
    heading: 'Des données de membres bien tenues',
    text: 'CadenceE réunit les données des membres et des clients au même endroit, avec rôles et autorisations, journal d’audit, consentement enregistré et désinscription en un clic, et des demandes d’accès et d’effacement qui exportent ou effacent les données d’une personne.',
  },
};

export default guide;
