import type { GuideContent } from '../types';

// L’e-mail de relance commerciale : quand relancer, quoi écrire, modèles, quand s’arrêter, les
// règles suisses sur la publicité par e-mail (art. 3, al. 1, let. o LCD, nLPD) et le suivi dans un CRM.
const guide: GuideContent = {
  eyebrow: 'Vente',
  h1: 'E-mail de relance commerciale : comment obtenir une réponse',
  lead: 'Un premier e-mail reste souvent sans réponse pour des raisons qui n’ont rien à voir avec vous. Voici quand relancer, quoi écrire, six modèles à adapter, quand s’arrêter, les règles suisses sur le consentement et comment tout suivre dans un CRM.',
  readingMinutes: 9,
  keyTakeaways: [
    'Le silence est rarement un non : prévoyez trois à cinq relances avant d’abandonner un nouveau prospect.',
    'Relancez le jour même après un rendez-vous, et deux à trois jours ouvrables après une offre.',
    'Chaque relance apporte un élément nouveau et propose une seule prochaine étape, simple.',
    'Sans réponse après quatre ou cinq semaines, envoyez un e-mail de rupture courtois, puis arrêtez.',
    'Un e-mail marketing envoyé à une liste exige un consentement préalable ; une relance personnelle cesse dès qu’on vous le demande.',
  ],
  sections: [
    {
      id: 'pourquoi-relancer',
      heading: 'Pourquoi la plupart des ventes demandent plusieurs e-mails',
      paragraphs: [
        'Votre prospect a lu votre e-mail entre deux séances et a oublié de répondre, ou il attend le budget du prochain trimestre. Le silence signifie le plus souvent un mauvais moment, pas un refus.',
        'Une adhésion, un événement privé ou un contrat de service se décident à plusieurs, sur plusieurs semaines. Celui qui reste présent, utilement et sans insister, est encore là au moment de décider. Les raisons du silence sont banales :',
      ],
      bullets: [
        'L’e-mail est arrivé au mauvais moment.',
        'La décision dépend de quelqu’un d’autre, ou d’un budget.',
        'La prochaine étape n’était pas claire, ou semblait demander un effort.',
      ],
    },
    {
      id: 'quand-relancer',
      heading: 'Quand envoyer un e-mail de relance : délais et rythme',
      paragraphs: [
        'Relancez vite tant que l’échange est frais, puis espacez les messages. Comptez en jours ouvrables et pensez aux vacances : elles varient d’un canton à l’autre, et la période entre Noël et début janvier est calme presque partout.',
      ],
      subsections: [
        {
          heading: 'Après un premier contact',
          paragraphs: [
            'Si votre premier e-mail reste sans réponse, un rythme sur cinq semaines convient à la plupart des clubs, établissements et équipes B2B : un bref rappel au 3e jour, un apport utile vers le 7e, un autre angle vers le 17e et un e-mail de rupture vers le 30e. Si c’est le prospect qui vous a contacté, répondez le jour même.',
          ],
        },
        {
          heading: 'Après un rendez-vous',
          paragraphs: [
            'Envoyez l’e-mail de suivi le jour même, ou le lendemain matin au plus tard : ce que vous avez compris, qui fait quoi, et la prochaine étape avec une date. C’est la relance à ne jamais oublier.',
          ],
        },
        {
          heading: 'Après une offre ou un contrat',
          paragraphs: [
            'Deux à trois jours ouvrables après une offre, vérifiez qu’elle est bien arrivée et proposez de la parcourir ensemble. Quand un contrat attend une [signature en ligne](page:e-signature), un rappel avant une échéance réelle est un service, pas une pression.',
          ],
        },
        {
          heading: 'Après un silence',
          paragraphs: [
            'Quand un prospect chaud ne répond plus, ralentissez : un message après une semaine, un autre après deux ou trois, puis un e-mail de rupture. Ne réécrivez des mois plus tard que si quelque chose a vraiment changé.',
          ],
        },
      ],
    },
    {
      id: 'quoi-ecrire',
      heading: 'Que mettre dans une relance : objet, valeur, prochaine étape',
      paragraphs: [
        'Une bonne relance tient en trois parties : le contexte en une phrase, un élément nouveau et utile, et une seule prochaine étape.',
      ],
      subsections: [
        {
          heading: 'Un objet qui donne envie d’ouvrir',
          paragraphs: [
            'Court et précis : « Votre visite de jeudi : prochaines étapes », « Offre pour votre réception d’été ». Relancez votre propre message dans le même fil, et n’inventez jamais un « Re: » sur un premier e-mail.',
          ],
        },
        {
          heading: 'Du nouveau dans chaque message',
          paragraphs: [
            'Une réponse à une question posée en rendez-vous, l’exemple d’un client comparable, une date que vous réservez, une option plus simple. « Je me permets de revenir vers vous » n’apporte rien : sans élément nouveau, attendez.',
          ],
        },
        {
          heading: 'Une seule prochaine étape',
          paragraphs: [
            '« Mardi à 10 h ou mercredi à 14 h vous conviendrait-il pour un appel de 20 minutes ? » vaut mieux que « N’hésitez pas si vous avez des questions. » Une question, un lien au maximum.',
          ],
        },
        {
          heading: 'Le ton suisse',
          paragraphs: [
            'Vouvoyez jusqu’à ce que votre interlocuteur propose le tutoiement, donnez des dates et des heures précises, et renoncez à la pression comme à l’urgence inventée. En Suisse romande, « Meilleures salutations » reste la formule de fin la plus courante.',
          ],
        },
      ],
    },
    {
      id: 'modeles-de-relance',
      heading: 'Six modèles de relance client et prospect à adapter',
      paragraphs: [
        'Un mail de relance prospect efficace tient en quelques lignes. Remplacez les noms, dates et détails par les vôtres, et gardez la structure : contexte, élément utile, prochaine étape.',
      ],
      subsections: [
        {
          heading: '1. Après un premier contact resté sans réponse',
          paragraphs: [
            'Objet : Re: L’événement d’été de votre équipe',
            'Bonjour Madame Keller, Je vous ai écrit la semaine dernière au sujet de l’événement d’été de votre équipe. Les dates de juin partent vite : souhaitez-vous que je vous envoie trois dates possibles et un exemple de menu ? Meilleures salutations, Laura Meier',
          ],
        },
        {
          heading: '2. L’e-mail de suivi après un rendez-vous',
          paragraphs: [
            'Objet : Merci pour notre échange : résumé et prochaines étapes',
            'Bonjour Monsieur Rochat, Merci pour votre temps aujourd’hui. Si j’ai bien compris, vous prévoyez un dîner pour 80 personnes fin septembre. Comme convenu, je vous envoie d’ici vendredi une offre avec deux plans de salle, et vous confirmez le nombre de participants avec votre direction. Pouvons-nous en reparler mardi à 10 h ? Meilleures salutations, Laura Meier',
          ],
        },
        {
          heading: '3. Relancer après l’envoi d’une offre',
          paragraphs: [
            'Objet : Votre offre pour le dîner de septembre',
            'Bonjour Monsieur Rochat, Notre offre vous est-elle bien parvenue, et correspond-elle à ce que vous aviez en tête ? Je peux la parcourir avec vous en 15 minutes par téléphone, jeudi matin ou vendredi après-midi. Meilleures salutations, Laura Meier',
          ],
        },
        {
          heading: '4. Quand un contrat attend la signature',
          paragraphs: [
            'Objet : Votre contrat d’adhésion : il reste une étape',
            'Bonjour Madame Favre, Votre contrat est prêt à être signé via le lien envoyé lundi, et l’acompte d’entrée se règle dans la même étape. Si un point n’est pas clair, répondez simplement à cet e-mail : je vous réponds aujourd’hui. Meilleures salutations, Marc Bühler',
          ],
        },
        {
          heading: '5. Apporter de la valeur après un silence',
          paragraphs: [
            'Objet : Trois idées pour votre soirée clients',
            'Bonjour Monsieur Steiner, Vous trouverez en annexe trois formats de soirées clients que nous avons organisées pour des entreprises de votre taille. Si l’un d’eux se rapproche de votre projet, je l’adapterai volontiers. Meilleures salutations, Laura Meier',
          ],
        },
        {
          heading: '6. L’e-mail de rupture',
          paragraphs: [
            'Objet : Dois-je clore votre demande ?',
            'Bonjour Monsieur Steiner, Sans nouvelles de votre part, je suppose que le moment n’est pas le bon : ce sera mon dernier message au sujet de votre soirée clients. Si les choses changent, il suffit de répondre à cet e-mail, et je reprendrai volontiers le dossier. Meilleures salutations, Laura Meier',
          ],
        },
      ],
    },
    {
      id: 'quand-arreter',
      heading: 'Quand s’arrêter : l’e-mail de rupture et après',
      paragraphs: [
        'Pour un nouveau prospect, trois à cinq relances sur quatre à six semaines sont une limite raisonnable ; au-delà, vous nuisez à votre image et à la réputation de votre adresse d’envoi. L’e-mail de rupture laisse une dernière chance simple de répondre, et ramène souvent un utile « pas maintenant, recontactez-moi en automne ». Arrêtez immédiatement quand :',
      ],
      bullets: [
        'la personne le demande ou se désinscrit ;',
        'elle dit non, ou désigne quelqu’un d’autre ;',
        'l’e-mail revient en erreur, ou elle a quitté l’entreprise ;',
        'elle répond, prend rendez-vous ou signe.',
      ],
      tip: 'Notez pourquoi un prospect a été clôturé (« pas de budget cette année ») : dans six mois, cette note vous dira s’il faut réécrire.',
    },
    {
      id: 'consentement-et-desinscription',
      heading: 'Consentement et désinscription : les règles en Suisse',
      paragraphs: [
        'Un message personnel à quelqu’un que vous avez rencontré, au sujet d’une offre qu’il a demandée, relève de la correspondance commerciale. Une newsletter envoyée à une liste relève du marketing, régi en Suisse par la loi fédérale contre la concurrence déloyale (LCD).',
        'Selon l’art. 3, al. 1, let. o LCD, envoyer de la publicité de masse par e-mail est déloyal sans le consentement préalable des destinataires, sans indication correcte de l’expéditeur ou sans possibilité simple et sans frais de refuser les envois suivants. Exception : les clients dont vous avez obtenu l’adresse lors d’une vente, en les informant de leur droit de refus, peuvent recevoir de la publicité pour vos propres produits similaires. Les adresses professionnelles ne sont pas exemptées.',
        'La loi ne trace pas de frontière exacte entre relance personnelle et publicité de masse : un message écrit pour une personne relève en général de la correspondance, un modèle envoyé à une liste achetée non. Et selon la nLPD, chacun peut s’opposer à l’utilisation de ses données à des fins de marketing. En pratique :',
      ],
      bullets: [
        'Newsletters et campagnes uniquement aux personnes qui ont accepté, consentement enregistré.',
        'Un expéditeur identifié et un lien de désinscription dans chaque e-mail marketing.',
        'Des relances individuelles qui cessent dès que la personne le demande.',
        'Des règles souvent plus strictes dans l’UE : l’Allemagne, par exemple, exige en général un consentement préalable, même entre entreprises.',
      ],
      tip: 'Information générale, pas un avis juridique. Pour les données personnelles en général, voir notre [checklist nLPD](page:guide-fadp-checklist).',
    },
    {
      id: 'suivre-les-relances-dans-un-crm',
      heading: 'Suivre ses relances dans un CRM : séquences et rappels',
      paragraphs: [
        'Les relances échouent parce que personne ne pense à les envoyer, et un tableur ne suffit plus au-delà de quelques dizaines de prospects. Utilisez des séquences pour les étapes standard et des tâches pour les relances personnelles, et veillez à ce qu’une séquence s’arrête quand la personne répond.',
        'Dans le [CRM commercial](page:sales) de CadenceE, chaque prospect se trouve dans l’une des cinq phases, de la prise de contact à l’acompte, avec ses tâches. Les [séquences](page:email) envoient les relances selon un calendrier depuis une boîte d’envoi qui inscrit chaque e-mail sur la fiche et respecte le consentement, et un brief matinal liste ce qui demande votre attention. Encore sur un tableur ? Voir [passer d’Excel à un CRM](page:guide-excel-to-crm).',
      ],
      steps: [
        { title: 'Une prochaine étape par prospect', text: 'Une phase, un responsable, une date.' },
        { title: 'Des séquences écrites une fois', text: 'À partir des modèles ci-dessus, dans chaque langue de vente.' },
        { title: 'Chaque e-mail sur la fiche', text: 'Pour que personne n’écrive deux fois.' },
        { title: 'Les relances en premier', text: 'Avant que la boîte de réception n’envahisse la journée.' },
      ],
    },
    {
      id: 'redacteur-ia',
      heading: 'Utiliser un rédacteur IA avec discernement',
      paragraphs: [
        'L’IA aide face à la page blanche, mais elle ne sait ni ce que vous avez promis, ni ce qui est vrai. Le [rédacteur commercial IA](page:ai) de CadenceE rédige les relances dans le ton de votre marque, avec l’objectif et la longueur choisis, et une personne relit et envoie chaque message. Avec n’importe quel outil :',
      ],
      bullets: [
        'Donnez-lui vos notes de rendez-vous et l’étape visée.',
        'Vérifiez chaque fait, nom, date, prix et promesse, et gardez votre voix.',
        'Ne la laissez jamais inventer des clients ou des chiffres, ni écrire seule à une liste.',
      ],
    },
  ],
  faq: {
    heading: 'E-mails de relance : questions fréquentes',
    items: [
      { q: 'Combien de temps attendre avant d’envoyer un e-mail de relance ?', a: 'Le jour même après un rendez-vous. Après un premier e-mail ou une offre, deux à trois jours ouvrables, puis environ une semaine, puis deux.' },
      { q: 'Combien de relances envoyer à un prospect ?', a: 'Trois à cinq sur quatre à six semaines, la dernière étant un e-mail de rupture. Arrêtez dès qu’on vous le demande.' },
      { q: 'Que mettre dans un e-mail de suivi après un rendez-vous ?', a: 'Des remerciements, un résumé de ce que vous avez compris, qui fait quoi et quand, et la prochaine étape avec une date.' },
      { q: 'Comment relancer une offre sans insister ?', a: 'Demandez si elle est bien arrivée, proposez un court appel, puis apportez un élément utile dans le message suivant. N’évoquez une échéance que si elle est réelle.' },
      { q: 'Faut-il un consentement pour envoyer des relances en Suisse ?', a: 'En général pas pour une relance personnelle, mais arrêtez si on vous le demande. Les e-mails marketing envoyés à une liste exigent un consentement préalable selon la LCD, sauf pour les clients existants, et une possibilité de refus.' },
      { q: 'L’IA peut-elle rédiger mes relances ?', a: 'Elle peut les préparer, mais une personne doit relire et envoyer chaque message.' },
    ],
  },
  cta: {
    heading: 'Chaque relance à temps, sur une seule fiche',
    text: 'CadenceE donne à chaque prospect une phase et une prochaine étape, envoie vos séquences de relance avec le consentement intégré et prépare chaque e-mail avec un rédacteur IA que vous relisez avant l’envoi.',
  },
};

export default guide;
