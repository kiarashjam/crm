import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Membership',
  h1: 'Membership renewal and member retention: how to keep your members',
  lead: 'Members rarely leave because of one bad evening. They drift away over months, and the renewal date only makes it official. This guide shows how to measure retention, spot the drift early, run renewals without chasing, remove payment friction and win members back, with a 90-day plan to start.',
  readingMinutes: 9,
  keyTakeaways: [
    'Retention is decided months before the renewal date, by whether members still use the club.',
    'Measure the renewal rate by membership type and by the year members joined, not just the head count.',
    'Visits, spending and engagement are the early warnings: a drop in any of them deserves a call.',
    'Start each renewal 90 days ahead, with a personal conversation for the members at risk.',
    'Make paying effortless: one clear statement, QR-bill, TWINT or card, and dues spread over the year where it helps.',
  ],
  sections: [
    {
      id: 'why-members-leave',
      heading: 'Why members leave',
      paragraphs: [
        'Ask a club why members leave and the answer is usually the price. Ask the members who left and the story is different: they stopped coming, and then the fee no longer made sense. Price is the reason given; lack of use is the cause. The usual causes:',
      ],
      bullets: [
        'They never really arrived: no friends or habits made in the first year.',
        'Life changed: a move, a new job, children, a separation or retirement.',
        'The club changed: a new crowd, a new chef, fewer of the events they joined for.',
        'Something went wrong and nobody followed it up: a complaint, a billing error.',
        'Friction at renewal: the invoice went to an old address, the card expired, the reminder came too late.',
      ],
      tip: 'Hold a short exit conversation with every member who leaves and record the reason in one of a few fixed categories. After a year, you will know which causes are yours to fix.',
    },
    {
      id: 'measure-retention-and-churn',
      heading: 'How to measure member retention and churn',
      paragraphs: [
        'The head count hides what matters: a club can grow while losing a fifth of its members every year. Measure renewals instead.',
      ],
      subsections: [
        {
          heading: 'The renewal rate',
          paragraphs: [
            'Divide the members who renewed by the members whose renewal fell due in the period. If 200 came up and 176 renewed, the renewal rate is 88% and churn is 12%. Leave out members who joined during the period.',
          ],
        },
        {
          heading: 'Break it down',
          paragraphs: ['One overall figure tells you little. Split it so you can see where members are lost:'],
          bullets: [
            'By cohort: members facing their first renewal leave more often than long-standing ones.',
            'By membership type: individual, household, corporate, junior, country.',
            'By revenue: dues kept after upgrades and downgrades, not only people kept.',
            'By reason for leaving, from your exit conversations.',
          ],
        },
      ],
    },
    {
      id: 'spot-at-risk-members',
      heading: 'Spot at-risk members early',
      paragraphs: [
        'Most departures announce themselves months in advance, in data the club already has. Four groups of signals matter most:',
      ],
      bullets: [
        'Visits: fewer check-ins than the member’s own usual rhythm, or no visit for 60 to 90 days.',
        'Spending: bar and restaurant tabs that shrink, or a house account that goes quiet.',
        'Engagement: no event bookings, newsletters ignored, the partner in the household no longer coming.',
        'Service: an unresolved complaint, a statement left unpaid, a change of address or employer.',
      ],
      subsections: [
        {
          heading: 'Turn signals into a short list',
          paragraphs: [
            'Compare each member with their own history, not the club average: someone who came weekly and now comes monthly is more at risk than someone who always came monthly. Give a point per signal and review the highest scores each month, nearest renewals first.',
            'In [membership management software](page:membership) such as CadenceE, each renewal carries an at-risk score and sits next to the member’s visits, spending and household, so this list is ready when you need it.',
          ],
        },
      ],
    },
    {
      id: 'renewal-calendar-and-reminders',
      heading: 'The renewal calendar and reminder sequence',
      paragraphs: [
        'A renewal should never surprise anyone. Whether memberships renew on 1 January or on each anniversary, the rhythm is the same. Write the messages once and let [email automation](page:email) send them on time, in each member’s language.',
      ],
      steps: [
        { title: '90 days before', text: 'Review the members due for renewal, sorted by risk. Call the ones at risk before any message goes out.' },
        { title: '60 days before', text: 'Send a personal renewal notice: what the membership includes next year, the events coming up and what the member enjoyed this year.' },
        { title: '30 days before', text: 'Send the invoice or statement with its QR-bill and the other ways to pay.' },
        { title: '14 days before', text: 'A short, friendly reminder to those who have not paid, with the payment details again.' },
        { title: 'Renewal date', text: 'Thank the members who renewed. Ask the manager or a board member to phone those who have not.' },
        { title: '30 days after', text: 'Record the outcome and the reason. Members who left move to the win-back list.' },
      ],
      tip: 'In a Swiss association, members may by law resign with six months’ notice to the end of the year (see the questions below), so hold retention conversations before that deadline.',
    },
    {
      id: 'the-renewal-conversation',
      heading: 'The renewal conversation',
      paragraphs: [
        'For members at risk, a call or a coffee works far better than a third email. It should come from someone who knows the member and open with listening, not with the invoice. Look at their history first: last visit, events booked, household. Then ask:',
      ],
      bullets: [
        'How has the club been for you this year?',
        'What would make you come more often?',
        'Has anything changed for you or your family?',
        'Is there anything we got wrong that we should know about?',
      ],
      subsections: [
        {
          heading: 'Answering the common objections',
          paragraphs: [
            '“We don’t come enough” is an invitation: suggest events that match what they enjoy, or a membership type that fits how they use the club now. “It’s too expensive” often means the same thing. Record what was agreed, so the next colleague knows.',
          ],
        },
      ],
    },
    {
      id: 'payment-friction',
      heading: 'Remove payment friction',
      paragraphs: [
        'Many lapsed memberships are not decisions at all: the invoice went unnoticed, the card expired, the payment was put off and forgotten. Every step you remove keeps members.',
      ],
      bullets: [
        'One clear statement instead of separate invoices for dues, tabs and events.',
        'The payment methods Swiss members use: a QR-bill their banking app can scan, TWINT and card.',
        'Recurring dues billed monthly or quarterly, so the renewal is not one large annual shock.',
        'The right billing contact: the household’s payer, or a company’s finance department.',
        'A due date that is easy to see, and reminders that sound like the club, not a debt collector.',
      ],
      tip: 'See our guide to the [Swiss QR-bill for clubs](page:guide-qr-bill). In CadenceE, dues, tabs and events end up on one monthly statement, paid by card, TWINT, SEPA or QR-bill: see [club billing and payments](page:billing).',
    },
    {
      id: 'households-and-corporate-memberships',
      heading: 'Households and corporate memberships',
      paragraphs: [
        'In a household, the person who pays is not always the one who uses the club. If only one partner comes, the other will question the fee, so invite both and speak to both at renewal. Watch the moment children move from a junior to an adult category: a common point of departure.',
        'A corporate membership is renewed by someone who rarely visits, in HR or finance. Send them a short summary of how their people used the club before their budget is set. When a named employee leaves the company, offer to transfer the place or turn it into a personal membership. Both are easier when [households and corporate memberships](page:private-clubs) are recorded as such.',
      ],
    },
    {
      id: 'events-and-communication',
      heading: 'Events and communication that keep members',
      paragraphs: [
        'Retention starts on the first day. A new member who has met several people and booked a few events in the first three months is likely to stay; one who has not is already at risk. Onboarding begins with the [membership application](page:guide-membership-applications) and continues with a welcome evening, introductions and a call after the first month.',
        'After that, a steady rhythm of small events (a tasting, a talk, a members’ table) matters more than a few big ones. In communication, relevance beats frequency: write in the member’s language, invite people to what matches their interests, and send promotional mailings only to those who agreed. Our guide on [how to run a private members’ club](page:guide-run-a-club) covers the calendar.',
      ],
    },
    {
      id: 'win-back-former-members',
      heading: 'Win back former members',
      paragraphs: [
        'Not every departure is final: people move back, children grow up, a new job settles down. Close the door gently: thank them, record the reason, and agree whether you may stay in touch.',
      ],
      bullets: [
        'After three to six months, invite those who left for practical reasons to an open event.',
        'After a year, write about what has changed at the club, especially what touches their reason for leaving.',
        'Make returning simple: no new application from scratch for members in good standing.',
      ],
      tip: 'Keep only what you need about former members, and send them promotional emails only with their consent. Our [Swiss FADP checklist for clubs](page:guide-fadp-checklist) explains what applies.',
    },
    {
      id: 'a-90-day-plan',
      heading: 'A 90-day plan to improve member retention',
      paragraphs: ['Three months are enough to put the basics in place before the next renewal season.'],
      steps: [
        { title: 'Days 1 to 30: get the facts', text: 'Bring members, renewal dates, visits and payments into one place (see [from Excel to a CRM](page:guide-excel-to-crm)). Calculate last year’s renewal rate and list everyone due in the next six months.' },
        { title: 'Days 31 to 60: set up the routine', text: 'Agree on your at-risk signals, call the members with the highest scores, write the reminder sequence and check that every member can pay by QR-bill, TWINT or card.' },
        { title: 'Days 61 to 90: run and review', text: 'Run the first renewals with the new calendar, hold exit conversations, send the first win-back invitations and compare the numbers with last year.' },
      ],
      tip: 'To see at-risk renewals, visits and statements side by side before you change anything, [try the demo](/login?demo=1).',
    },
  ],
  faq: {
    heading: 'Membership renewal and retention: frequent questions',
    items: [
      {
        q: 'What is a good member retention rate for a club?',
        a: 'There is no universal benchmark: it depends on the type of club, its fees and its waiting list. Compare with your own previous years, by membership type and cohort, and track first-year members separately.',
      },
      {
        q: 'How do you calculate member churn?',
        a: 'Divide the members who did not renew by those whose membership came up for renewal in the period. If 200 came up and 24 did not renew, churn is 12%.',
      },
      {
        q: 'When should we send membership renewal reminders?',
        a: 'A personal notice about 60 days before the renewal date, the invoice 30 days before, a friendly reminder 14 days before, and a call shortly after the date to those who have not paid. Members at risk deserve a conversation 90 days ahead.',
      },
      {
        q: 'How can a club reduce member churn?',
        a: 'Help new members settle in during their first year, watch visits, spending and engagement, talk to members at risk well before renewal, make paying simple and ask every leaver why.',
      },
      {
        q: 'How much notice must members of a Swiss association give to leave?',
        a: 'Under Art. 70 para. 2 of the Civil Code, members of an association may resign with six months’ notice to the end of the calendar year or of the administrative period. Check your articles of association, and talk to members at risk before that period begins.',
      },
    ],
  },
  cta: {
    heading: 'See renewals at risk before they become cancellations',
    text: 'CadenceE shows each renewal with an at-risk score next to the member’s visits, spending and household, and sends reminders and statements from one place, so your team calls the right members in time.',
  },
};

export default guide;
