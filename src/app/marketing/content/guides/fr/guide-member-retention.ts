import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Gestion des membres',
  h1: 'Renouvellement des adhésions et fidélisation des membres',
  lead: 'Un membre part rarement à cause d’une mauvaise soirée. Il s’éloigne pendant des mois, et l’échéance de la cotisation ne fait qu’officialiser son départ. Ce guide montre comment mesurer la fidélisation, repérer les signaux faibles, organiser les relances de cotisation, simplifier le paiement et faire revenir les anciens membres, avec un plan sur 90 jours.',
  readingMinutes: 9,
  keyTakeaways: [
    'La fidélisation se joue des mois avant l’échéance, selon que le membre fréquente encore le club ou non.',
    'Mesurez le taux de renouvellement par type d’adhésion et par année d’entrée, pas seulement l’effectif.',
    'Visites, consommations et participation sont les signaux d’alerte : une baisse mérite un appel.',
    'Préparez chaque renouvellement 90 jours à l’avance, avec un entretien personnel pour les membres à risque.',
    'Facilitez le paiement : un relevé clair, facture QR, TWINT ou carte, et une cotisation échelonnée si cela aide.',
  ],
  sections: [
    {
      id: 'pourquoi-les-membres-partent',
      heading: 'Pourquoi les membres partent',
      paragraphs: [
        'Demandez à un comité pourquoi les membres partent, il répondra souvent : le prix. Demandez-le aux membres partis, l’histoire est autre : ils ne venaient plus, et dès lors la cotisation n’avait plus de sens. Le prix est la raison invoquée, le manque d’usage en est la cause. Les causes les plus fréquentes :',
      ],
      bullets: [
        'Ils ne sont jamais vraiment arrivés : ni amis ni habitudes au cours de la première année.',
        'Leur vie a changé : déménagement, nouvel emploi, enfants, séparation, retraite.',
        'Le club a changé : nouvelle ambiance, nouveau chef, moins des événements qui les avaient attirés.',
        'Un problème n’a pas été suivi : une réclamation, une erreur de facturation.',
        'Des frictions à l’échéance : facture à une ancienne adresse, carte expirée, rappel trop tardif.',
      ],
      tip: 'Proposez un bref entretien de départ à chaque membre qui s’en va et classez le motif dans quelques catégories fixes : en un an, vous saurez quelles causes dépendent de vous.',
    },
    {
      id: 'mesurer-la-fidelisation',
      heading: 'Mesurer la fidélisation et l’attrition',
      paragraphs: ['L’effectif total cache l’essentiel : un club peut grandir tout en perdant un cinquième de ses membres chaque année. Mesurez plutôt les renouvellements.'],
      subsections: [
        {
          heading: 'Le taux de renouvellement',
          paragraphs: [
            'Divisez le nombre de membres qui ont renouvelé par le nombre de membres arrivés à échéance sur la période. Si 200 adhésions arrivaient à échéance et que 176 ont été renouvelées, le taux de renouvellement est de 88 % et l’attrition de 12 %. Excluez les membres entrés pendant la période.',
          ],
        },
        {
          heading: 'Le ventiler',
          paragraphs: ['Un chiffre global dit peu de chose. Ventilez-le pour voir où vous perdez des membres :'],
          bullets: [
            'Par année d’entrée : le premier renouvellement est le plus fragile.',
            'Par type d’adhésion : individuelle, couple ou famille, entreprise, junior, hors canton.',
                'Par motif de départ, issu des entretiens de départ.',
          ],
        },
      ],
    },
    {
      id: 'reperer-les-membres-a-risque',
      heading: 'Repérer tôt les membres à risque',
      paragraphs: ['La plupart des départs s’annoncent des mois à l’avance, dans des données que le club possède déjà :'],
      bullets: [
        'Visites : moins de passages que d’habitude pour ce membre, ou aucune visite depuis 60 à 90 jours.',
        'Consommations : des notes de bar et de restaurant qui baissent, un compte maison qui s’endort.',
        'Participation : plus aucune inscription aux événements, le conjoint qui ne vient plus.',
        'Service : une réclamation en suspens, un relevé impayé, un changement d’adresse ou d’employeur.',
      ],
      subsections: [
        {
          heading: 'Des signaux à la liste d’appels',
          paragraphs: [
            'Comparez chaque membre à sa propre histoire plutôt qu’à la moyenne : celui qui venait chaque semaine et ne vient plus qu’une fois par mois est plus à risque que celui qui est toujours venu une fois par mois. Donnez un point par signal et revoyez chaque mois les scores les plus élevés, échéances proches d’abord.',
            'Dans un [logiciel de gestion des membres](page:membership) comme CadenceE, chaque renouvellement porte un score de risque et figure à côté des visites, des consommations et du foyer du membre : la liste est prête quand vous en avez besoin.',
          ],
        },
      ],
    },
    {
      id: 'calendrier-de-renouvellement',
      heading: 'Le calendrier de renouvellement et les relances de cotisation',
      paragraphs: [
        'Un renouvellement ne devrait surprendre personne. Que toutes les adhésions arrivent à échéance le 1er janvier ou à la date anniversaire de chacun, le rythme est le même. Rédigez les messages une fois et laissez l’[automatisation des e-mails](page:email) les envoyer à temps, dans la langue de chaque membre.',
      ],
      steps: [
        { title: '90 jours avant', text: 'Passez en revue les membres arrivant à échéance, triés par risque, et appelez ceux qui sont à risque avant tout envoi.' },
        { title: '60 jours avant', text: 'Envoyez un avis de renouvellement personnel : ce que l’adhésion comprend l’an prochain, les événements à venir, ce que le membre a apprécié cette année.' },
        { title: '30 jours avant', text: 'Envoyez la facture ou le relevé, avec sa facture QR et les autres moyens de paiement.' },
        { title: '14 jours avant', text: 'Une relance courte et aimable à ceux qui n’ont pas payé, avec les informations de paiement.' },
        { title: 'À l’échéance', text: 'Remerciez ceux qui ont renouvelé. Le gérant ou un membre du comité appelle les autres.' },
        { title: '30 jours après', text: 'Notez le résultat et le motif. Les membres partis passent sur la liste de reconquête.' },
      ],
      tip: 'Dans une association, la loi permet de sortir avec un préavis de six mois pour la fin de l’année (voir les questions ci-dessous) : menez les entretiens avant ce délai.',
    },
    {
      id: 'entretien-de-renouvellement',
      heading: 'L’entretien de renouvellement',
      paragraphs: [
        'Pour un membre à risque, un appel ou un café vaut mieux qu’un troisième e-mail. Il doit venir de quelqu’un qui le connaît et commencer par l’écoute, pas par la facture. Regardez d’abord son historique : dernière visite, événements réservés, foyer. Puis demandez :',
      ],
      bullets: [
        'Comment s’est passée votre année au club ?',
        'Qu’est-ce qui vous ferait venir plus souvent ?',
        'Quelque chose a-t-il changé pour vous ou votre famille ?',
        'Y a-t-il quelque chose que nous avons mal fait ?',
      ],
      subsections: [
        {
          heading: 'Répondre aux objections courantes',
          paragraphs: [
            '« Nous ne venons pas assez » est une invitation : proposez des événements à son goût ou une adhésion adaptée à son usage actuel. « C’est trop cher » veut souvent dire la même chose. Notez ce qui a été convenu.',
          ],
        },
      ],
    },
    {
      id: 'simplifier-le-paiement',
      heading: 'Supprimer les frictions de paiement',
      paragraphs: [
        'Beaucoup de non-renouvellements ne sont pas des décisions : facture passée inaperçue, carte expirée, paiement remis puis oublié. Chaque étape supprimée garde des membres.',
      ],
      bullets: [
        'Un seul relevé clair plutôt que des factures séparées.',
        'Les moyens de paiement des Suisses : une facture QR que l’application bancaire lit, TWINT et la carte.',
        'Une cotisation récurrente, mensuelle ou trimestrielle, pour éviter le choc d’une grosse facture annuelle.',
        'Le bon destinataire : la personne qui paie pour le foyer, ou la comptabilité de l’entreprise.',
        'Une échéance visible et des relances au ton du club, pas d’un service de recouvrement.',
      ],
      tip: 'Voir notre guide sur la [facture QR pour clubs](page:guide-qr-bill). Dans CadenceE, cotisations, consommations et événements figurent sur un relevé mensuel, payable par carte, TWINT, SEPA ou facture QR : voir [facturation et paiements](page:billing).',
    },
    {
      id: 'foyers-et-adhesions-entreprise',
      heading: 'Foyers et adhésions d’entreprise',
      paragraphs: [
        'Dans un foyer, celui qui paie n’est pas toujours celui qui vient. Si un seul conjoint fréquente le club, l’autre finira par discuter la cotisation : invitez les deux et parlez aux deux au moment du renouvellement. Surveillez le passage des enfants de la catégorie junior à la catégorie adulte, un moment de départ fréquent.',
        'Une adhésion d’entreprise est renouvelée par quelqu’un qui vient rarement, aux RH ou aux finances. Envoyez-lui un bref bilan de l’usage de ses collaborateurs avant qu’il fixe son budget. Quand un collaborateur quitte l’entreprise, proposez de transférer la place ou d’en faire une adhésion personnelle. Tout cela est plus simple quand [foyers et adhésions d’entreprise](page:private-clubs) sont enregistrés comme tels.',
      ],
    },
    {
      id: 'evenements-et-communication',
      heading: 'Des événements et une communication qui fidélisent',
      paragraphs: [
        'La fidélisation commence le premier jour. Un nouveau membre qui a fait des rencontres et participé à quelques événements dans ses trois premiers mois a toutes les chances de rester. L’accueil commence avec le [processus d’admission](page:guide-membership-applications) et se poursuit par une soirée de bienvenue, des présentations et un appel après le premier mois.',
        'Ensuite, un rythme régulier de petits formats (dégustation, conférence, table des membres) compte plus que quelques grands événements. En communication, la pertinence prime sur la fréquence : écrivez dans la langue du membre, invitez selon ses intérêts et réservez les envois promotionnels à ceux qui les ont acceptés. Voir notre guide pour [gérer un club privé](page:guide-run-a-club).',
      ],
    },
    {
      id: 'reconquerir-les-anciens-membres',
      heading: 'Reconquérir les anciens membres',
      paragraphs: [
        'Tous les départs ne sont pas définitifs : on revient de l’étranger, les enfants grandissent. Fermez la porte en douceur : remerciez, notez le motif et demandez si vous pouvez rester en contact.',
      ],
      bullets: [
        'Après trois à six mois, invitez ceux qui sont partis pour des raisons pratiques à un événement ouvert.',
        'Après un an, racontez ce qui a changé au club, surtout ce qui touche à leur motif de départ.',
        'Facilitez le retour : pas de nouvelle candidature complète pour un ancien membre en règle.',
      ],
      tip: 'Ne gardez sur les anciens membres que ce dont vous avez besoin, et ne leur envoyez d’e-mails promotionnels qu’avec leur accord. Notre [checklist nLPD pour les clubs](page:guide-fadp-checklist) explique les règles.',
    },
    {
      id: 'plan-sur-90-jours',
      heading: 'Un plan sur 90 jours pour améliorer la fidélisation',
      paragraphs: ['Trois mois suffisent pour mettre les bases en place avant la prochaine saison de renouvellement.'],
      steps: [
        { title: 'Jours 1 à 30 : les faits', text: 'Réunissez membres, échéances, visites et paiements au même endroit (voir [passer d’Excel à un CRM](page:guide-excel-to-crm)). Calculez le taux de renouvellement de l’an dernier et listez les échéances des six prochains mois.' },
        { title: 'Jours 31 à 60 : la routine', text: 'Fixez vos signaux de risque, appelez les membres aux scores les plus élevés, rédigez les relances et vérifiez que chacun peut payer par facture QR, TWINT ou carte.' },
        { title: 'Jours 61 à 90 : appliquer et mesurer', text: 'Menez les premiers renouvellements selon le nouveau calendrier, tenez les entretiens de départ et comparez avec l’an dernier.' },
      ],
      tip: 'Pour voir renouvellements à risque, visites et relevés côte à côte, [essayez la démo](/login?demo=1).',
    },
  ],
  faq: {
    heading: 'Renouvellement et fidélisation des membres : questions fréquentes',
    items: [
      {
        q: 'Quel est un bon taux de fidélisation pour un club ?',
        a: 'Il n’existe pas de référence universelle : tout dépend du type de club, de la cotisation et de la liste d’attente. Comparez-vous à vos années précédentes, par type d’adhésion et année d’entrée.',
      },
      {
        q: 'Comment calculer le taux d’attrition des membres ?',
        a: 'Divisez le nombre de membres qui n’ont pas renouvelé par le nombre de ceux dont l’adhésion arrivait à échéance sur la période. Si 200 arrivaient à échéance et que 24 n’ont pas renouvelé, l’attrition est de 12 %.',
      },
      {
        q: 'Quand envoyer les relances de cotisation ?',
        a: 'Un avis personnel environ 60 jours avant l’échéance, la facture 30 jours avant, une relance aimable 14 jours avant, puis un appel peu après l’échéance à ceux qui n’ont pas payé. Les membres à risque méritent un entretien 90 jours avant.',
      },
      {
        q: 'Comment réduire les départs de membres ?',
        a: 'Intégrez bien les nouveaux membres, suivez visites, consommations et participation, parlez aux membres à risque avant l’échéance, simplifiez le paiement et demandez à chaque partant pourquoi.',
      },
      {
        q: 'Quel préavis un membre d’association doit-il respecter pour sortir ?',
        a: 'Selon l’art. 70 al. 2 du Code civil, un membre peut sortir de l’association en annonçant sa sortie six mois avant la fin de l’année civile ou, si un exercice administratif est prévu, de celui-ci. Vérifiez vos statuts, et parlez aux membres à risque avant que ce délai ne commence.',
      },
    ],
  },
  cta: {
    heading: 'Voir les renouvellements à risque avant qu’ils deviennent des départs',
    text: 'CadenceE affiche chaque renouvellement avec un score de risque à côté des visites, des consommations et du foyer du membre, et envoie relances et relevés depuis un seul endroit, pour que votre équipe appelle les bons membres à temps.',
  },
};

export default guide;
