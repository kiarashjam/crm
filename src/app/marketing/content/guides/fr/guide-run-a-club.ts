import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Gestion de club',
  h1: 'Gérer un club privé : le guide pratique',
  lead: 'La gestion d’un club de membres, de la première demande au dixième renouvellement : adhésions, admissions, événements, notes et cotisations, équipe, nLPD et choix d’un logiciel de gestion de club.',
  readingMinutes: 9,
  keyTakeaways: [
    'Peu de catégories d’adhésion, chacune avec ses droits, sa cotisation et un plafond.',
    'Une procédure d’admission écrite, de la demande à l’accueil.',
    'Les trois premiers mois décident si un nouveau membre reste.',
    'Toutes les consommations sur un seul compte et un relevé par mois.',
    'La liste des membres est confidentielle : rôles, accès limités et nLPD.',
    'Un seul outil de la demande au renouvellement, plutôt que cinq tableurs.',
  ],
  sections: [
    {
      id: 'en-quoi-consiste-le-metier',
      heading: 'Ce que gérer un club privé veut dire',
      paragraphs: [
        'Un club privé ne vend ni un repas ni une salle, mais une place dans une communauté. Le gérer, c’est tenir trois choses en équilibre : les bons membres, en nombre suffisant ; des raisons de revenir, du programme à la qualité du service ; et des comptes qui tombent juste, avec des cotisations payées à temps et des notes réglées. Ce guide reprend chaque volet un à un.',
      ],
    },
    {
      id: 'structure-des-adhesions',
      heading: 'Une structure d’adhésion lisible',
      paragraphs: [
        'Vos catégories d’adhésion sont votre gamme. Chacune doit dire à qui elle s’adresse, ce qu’elle comprend, la finance d’entrée et la cotisation annuelle. On retrouve souvent dans les clubs suisses :',
      ],
      bullets: [
        'Individuelle : l’adhésion de référence, avec l’accès complet au club.',
        'Couple ou famille : conjoints et enfants regroupés en un foyer, facturés ensemble.',
        'Jeunes membres : une cotisation réduite sous un certain âge, pour renouveler les effectifs.',
        'Non-résident : pour ceux qui habitent loin et viennent moins souvent.',
        'Entreprise : une société détient des adhésions pour plusieurs collaborateurs.',
        'Membre d’honneur : nommé par le comité, en général sans cotisation.',
      ],
      tip: 'Quatre à six catégories suffisent presque toujours : chaque catégorie de plus ajoute une règle à expliquer et un cas de facturation.',
      subsections: [
        {
          heading: 'Fixer un plafond avant d’en avoir besoin',
          paragraphs: [
            'Un club bondé tous les jeudis perd son charme. Calculez un plafond à partir de votre capacité et de la fréquence de visite d’un membre moyen, fixez-le par catégorie avant que la demande ne vous y oblige, puis ouvrez une liste d’attente.',
          ],
        },
      ],
    },
    {
      id: 'demandes-et-candidatures',
      heading: 'De la demande à l’admission',
      paragraphs: [
        'Les bons candidats ont souvent d’autres options : l’admission doit être juste, personnelle et rapide. Écrivez la procédure une fois pour toutes, afin que chaque demande soit traitée de la même façon, quelle que soit la personne qui décroche.',
      ],
      steps: [
        { title: 'Noter chaque demande', text: 'Formulaire du site, e-mail, téléphone ou recommandation d’un membre : chaque demande rejoint une liste unique le jour même, avec le nom du parrain.' },
        { title: 'Rencontrer et faire visiter', text: 'Une visite ou un déjeuner avec la personne responsable des membres : c’est là que la plupart des gens se décident.' },
        { title: 'Candidature et parrains', text: 'Un formulaire court et, si vos règles le prévoient, un ou deux membres qui soutiennent la candidature.' },
        { title: 'Décision', text: 'Le comité d’admission décide selon des critères écrits, à un rythme connu.' },
        { title: 'Contrat et finance d’entrée', text: 'Le nouveau membre signe et paie la finance d’entrée ou l’acompte, idéalement en une seule étape.' },
      ],
      tip: 'Notre guide du [processus d’adhésion](page:guide-membership-applications) détaille chaque étape, y compris ce qu’un formulaire d’adhésion doit demander, et ce qu’il ne doit pas demander.',
    },
    {
      id: 'accueil-des-nouveaux-membres',
      heading: 'Accueillir les nouveaux membres pendant trois mois',
      paragraphs: [
        'Un membre qui n’a pas trouvé sa place après quelques mois risque de partir au premier renouvellement. L’accueil est un petit programme, pas un simple e-mail.',
      ],
      bullets: [
        'Un message de bienvenue avec le règlement, les horaires et les contacts.',
        'L’accès au portail membre, pour réserver et consulter son relevé.',
        'Une présentation à l’équipe et à quelques membres aux intérêts communs.',
        'Une invitation à un premier événement dans le mois.',
        'Un appel après environ deux mois : le club est-il à la hauteur de ses attentes ?',
      ],
    },
    {
      id: 'evenements-et-reservations',
      heading: 'Événements, réservations et privatisations',
      paragraphs: [
        'Le programme transforme une adhésion en habitude. Construisez-le par saison, mêlez des rendez-vous réguliers, comme un dîner mensuel, à quelques soirées d’exception, et publiez les dates tôt. Tenez une liste d’attente pour les événements complets, afin qu’une annulation devienne une réservation, et fixez une règle claire pour les annulations tardives.',
        'La privatisation de salons pour un anniversaire ou un dîner d’entreprise peut être une belle source de revenus. Traitez chaque demande comme une vente : une offre, un contrat, un acompte et un menu confirmé. Enregistrer les visites montre aussi qui fréquente vraiment le club. Voir notre page [CRM pour l’hôtellerie-restauration](page:hospitality).',
      ],
    },
    {
      id: 'notes-et-cotisations',
      heading: 'Notes de bar, comptes membres et cotisations',
      paragraphs: [
        'Au club, on signe la note plutôt que de payer au bar. Chaque consommation doit donc finir sur le compte du membre. Le rythme le plus simple : un relevé mensuel par membre ou par foyer, qui réunit cotisation, notes, événements et privatisations.',
      ],
      bullets: [
        'Reliez la caisse à la fiche du membre, pour ne rien ressaisir.',
        'Fixez une limite de crédit par compte et une règle pour les retards.',
        'Joignez une facture QR à chaque relevé, et proposez aussi TWINT ou la carte.',
        'Rapprochez le fichier bancaire de ce qui est dû, au lieu de pointer à la main.',
        'Exportez des chiffres propres vers la comptabilité, et corrigez par contre-écriture.',
      ],
      tip: 'Demandez à votre fiduciaire comment la TVA s’applique aux cotisations, à la finance d’entrée et à la restauration avant de fixer vos tarifs. Notre guide de la [facture QR pour clubs et associations](page:guide-qr-bill) couvre le paiement.',
    },
    {
      id: 'communication',
      heading: 'Communiquer comme un club, pas comme une marque',
      paragraphs: [
        'Les membres veulent être informés, pas sollicités. Une lettre mensuelle avec le programme, un mot personnel de temps à autre et un rappel avant chaque événement suffisent. Laissez chaque membre choisir sa langue : français, allemand ou anglais.',
        'N’envoyez la lettre d’information qu’à ceux qui l’ont acceptée, avec un lien de désinscription en un clic. Les relevés et confirmations de réservation, eux, font partie de l’adhésion.',
      ],
    },
    {
      id: 'renouvellements',
      heading: 'Renouvellements et fidélisation',
      paragraphs: [
        'Un renouvellement se joue bien avant l’échéance. Surveillez les signaux : aucune visite depuis trois mois, plus de réservations, un relevé impayé. Appelez ces membres avant l’envoi de l’avis de renouvellement, pas après la lettre de démission.',
        'Écrivez clairement la durée, le renouvellement et le préavis dans vos statuts ou votre contrat d’adhésion. Si votre club est une association au sens du Code civil, la loi prévoit déjà une règle : un membre peut sortir moyennant un préavis de six mois pour la fin de l’année civile ou de l’exercice administratif, et les statuts peuvent raccourcir ce délai, mais pas l’allonger. Faites vérifier vos règles par un conseiller. Notre guide sur le [renouvellement et la fidélisation des membres](page:guide-member-retention) va plus loin.',
      ],
    },
    {
      id: 'roles-de-l-equipe',
      heading: 'Les rôles de l’équipe : qui fait quoi',
      paragraphs: [
        'Même un petit club tourne mieux quand chaque tâche a un responsable :',
      ],
      bullets: [
        'Direction : programme, budget, équipe et relation avec le comité.',
        'Responsable des membres : demandes, visites, candidatures, accueil et renouvellements.',
        'Événements et restauration : calendrier, réservations, privatisations et service.',
        'Réception : accueil des membres et des invités, pointages et demandes du quotidien.',
        'Finances : cotisations, relevés, paiements, rappels et export comptable.',
        'Comité : admissions, règlement et orientation du club à long terme.',
      ],
      tip: 'Donnez à chacun l’accès dont son rôle a besoin, et rien de plus. Le bar doit pouvoir ouvrir une note, pas lire les notes du comité d’admission.',
    },
    {
      id: 'donnees-et-nlpd',
      heading: 'Données des membres et nLPD',
      paragraphs: [
        'La liste des membres est le bien le plus sensible d’un club. La nouvelle loi fédérale sur la protection des données (nLPD), en vigueur depuis le 1er septembre 2023, s’applique aux associations comme aux sociétés. Concrètement : ne collecter que le nécessaire, expliquer dans une déclaration de confidentialité ce que vous faites des données, répondre aux demandes d’accès, protéger les données et les effacer quand elles ne servent plus.',
        'Soyez particulièrement prudents avec les données sensibles, comme la santé, les opinions religieuses ou politiques et les poursuites pénales : un club en a rarement besoin. Demandez l’accord des membres avant de publier des photos prises lors d’événements. Les pièces comptables suivent leurs propres règles et se conservent en général dix ans en Suisse. Notre [checklist nLPD pour les clubs](page:guide-fadp-checklist) détaille les étapes ; pour votre cas précis, consultez un spécialiste de la protection des données.',
      ],
    },
    {
      id: 'logiciel-de-gestion-de-club',
      heading: 'Choisir un logiciel de gestion de club',
      paragraphs: [
        'Tableur des membres, outil d’e-mailing, caisse et comptabilité s’ignorent souvent, et chaque passage de l’un à l’autre fait perdre une demande ou une note. Quand vous comparez des logiciels, vérifiez qu’ils offrent :',
      ],
      bullets: [
        'Une fiche par personne, de la première demande au renouvellement et au départ.',
        'Des candidatures en ligne, traitées dans une seule liste.',
        'Des contrats signés en ligne, avec la finance d’entrée encaissée à la signature.',
        'Des événements avec liste d’attente, et des notes reliées à la caisse.',
        'Cotisations, relevés et moyens de paiement suisses, avec export comptable.',
        'Des e-mails avec consentement, plusieurs langues, des rôles et un journal d’audit.',
      ],
      subsections: [
        {
          heading: 'Où CadenceE trouve sa place',
          paragraphs: [
            'CadenceE est un [CRM suisse pour clubs privés](page:private-clubs) qui réunit tout cela : candidatures, contrats signés en ligne avec acompte, [gestion des membres](page:membership) avec foyers et renouvellements, événements, notes reliées à la caisse Lightspeed et [facturation avec facture QR et TWINT](page:billing). Il a été conçu avec le Pavillon 46, club privé vaudois. Quel que soit votre choix, notre guide [comment choisir un CRM](page:guide-choose-a-crm) aide à comparer, et [passer d’Excel à un CRM](page:guide-excel-to-crm) explique la migration. Vous pouvez aussi [essayer la démo](/login?demo=1).',
          ],
        },
      ],
    },
  ],
  faq: {
    heading: 'Gérer un club privé : questions fréquentes',
    items: [
      {
        q: 'Qu’est-ce qu’un club privé ?',
        a: 'Un club dont les espaces et les services sont réservés aux membres et à leurs invités. On y entre sur candidature, on paie une finance d’entrée et une cotisation annuelle, et l’on règle repas et boissons sur son compte.',
      },
      {
        q: 'Combien de membres un club privé doit-il avoir ?',
        a: 'Partez de votre capacité : combien de personnes le club accueille confortablement aux heures de pointe, et à quelle fréquence un membre moyen vient. Fixez un plafond par catégorie qui garde ces soirées agréables, puis tenez une liste d’attente.',
      },
      {
        q: 'Un club privé est-il une association au sens du droit suisse ?',
        a: 'Il peut l’être. Beaucoup de clubs sont des associations régies par le Code civil, dont les statuts règlent admission, cotisations, exclusion et démission. D’autres sont exploités par une société qui vend des adhésions par contrat. La forme juridique change qui décide et comment on part : consultez un conseiller.',
      },
      {
        q: 'Comment facturer les consommations aux membres ?',
        a: 'Chaque note va sur le compte du membre, et un relevé mensuel réunit cotisation, notes et événements, avec une facture QR et TWINT ou la carte en option. Voir [facturation et paiements](page:billing).',
      },
      {
        q: 'La nLPD s’applique-t-elle aux clubs de membres ?',
        a: 'Oui. Elle s’applique aux personnes privées et aux organisations qui traitent des données personnelles, associations et sociétés exploitant un club comprises.',
      },
      {
        q: 'Quel logiciel de gestion de club utiliser ?',
        a: 'Souvent, un fichier de membres, un outil de réservation, une caisse et une comptabilité cohabitent. Un CRM de club comme CadenceE réunit demandes, membres, événements, notes et facturation dans un seul système.',
      },
    ],
  },
  cta: {
    heading: 'Tout le club, une fiche par membre',
    text: 'CadenceE suit chaque personne de la première demande au contrat signé, au relevé mensuel et au renouvellement, en français, en allemand ou en anglais.',
  },
};

export default guide;
