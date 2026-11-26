import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Club management',
  h1: 'How to run a private members’ club',
  lead: 'A practical guide to private members’ club management, from the first enquiry to the tenth renewal: membership structure, admissions, events, tabs and dues, the team, member data under the Swiss FADP and the right software.',
  readingMinutes: 9,
  keyTakeaways: [
    'Keep membership categories few, each with clear rights, dues and a cap.',
    'Run admissions as one defined process, from enquiry to welcome.',
    'The first ninety days decide whether a new member stays.',
    'Post every charge to one house account and send one statement a month.',
    'Treat the member list as confidential: roles, need-to-know access and the FADP.',
    'Use one system from first enquiry to renewal, not five spreadsheets.',
  ],
  sections: [
    {
      id: 'what-the-job-involves',
      heading: 'What running a members’ club involves',
      paragraphs: [
        'A private members’ club sells a place in a community, not a meal or a room. Running one well means keeping three things in balance: the right members, and enough of them; reasons for them to come back, from the programme to the service; and money that adds up, with dues paid on time and tabs settled. Most of the daily work sits where those three meet, and the sections below take each part in turn.',
      ],
    },
    {
      id: 'membership-structure',
      heading: 'Design a membership structure people understand',
      paragraphs: [
        'Your membership categories are your product range. Each one should say who it is for, what it includes, the joining fee and the annual dues. Typical categories in Swiss clubs include:',
      ],
      bullets: [
        'Individual: the standard membership, with full use of the club.',
        'Couple or household: partners and children grouped and billed together.',
        'Young members: reduced dues below an age limit, to renew the membership.',
        'Non-resident: for members who live far away and visit less often.',
        'Corporate: a company holds memberships for several of its people.',
        'Honorary: granted by the board, usually without dues.',
      ],
      tip: 'Four to six categories are usually enough. Every extra category adds a rule to explain and a billing case to get right.',
      subsections: [
        {
          heading: 'Set a cap before you need one',
          paragraphs: [
            'A club that is packed every Thursday loses its charm. Work out a ceiling from your capacity and from how often an average member visits, decide it per category before demand forces the question, and keep a waitlist once you reach it.',
          ],
        },
      ],
    },
    {
      id: 'enquiries-and-applications',
      heading: 'From enquiry to admission',
      paragraphs: [
        'Good candidates usually have other options, so admission has to be fair, personal and quick. Write the process down once, so every enquiry is handled the same way whoever answers the phone.',
      ],
      steps: [
        { title: 'Capture every enquiry', text: 'Website form, email, phone or a member’s introduction: each enquiry goes into one list the same day, with who referred the person.' },
        { title: 'Meet and show the club', text: 'A visit or a lunch with the membership manager is where most people decide.' },
        { title: 'Application and sponsors', text: 'A short form, plus one or two members who support the application if your rules require it.' },
        { title: 'Decision', text: 'The committee or the membership manager decides on written criteria, at a known rhythm.' },
        { title: 'Agreement and joining fee', text: 'The new member signs and pays the joining fee or deposit, ideally in the same step.' },
      ],
      tip: 'Our guide to the [membership application process](page:guide-membership-applications) covers each step in detail, including what an application form should and should not ask.',
    },
    {
      id: 'onboarding',
      heading: 'Onboard new members in their first ninety days',
      paragraphs: [
        'A member who has not found their place within a few months is at risk of leaving at the first renewal. Treat onboarding as a short programme, not a single email.',
      ],
      bullets: [
        'A welcome message with house rules, opening hours and contacts.',
        'Access to the member portal, to book and see their statement.',
        'A personal introduction to the staff and to a few like-minded members.',
        'An invitation to a first event within a month of joining.',
        'A check-in call after about two months: is the club what they hoped for?',
      ],
    },
    {
      id: 'events-and-reservations',
      heading: 'Events, reservations and private hire',
      paragraphs: [
        'The programme turns a membership into a habit. Plan it by season, mix regular formats such as a monthly dinner with a few special evenings, and publish dates early. Keep a waitlist for full events, so a cancellation becomes a booking, and set a clear rule for late cancellations and no-shows.',
        'Private hire for birthdays or company dinners can be a welcome source of revenue. Treat each request like a sale: a quote, a contract, a deposit and a confirmed menu. Recording visits and check-ins also shows who actually uses the club, which matters at renewal time. See our [hospitality CRM](page:hospitality) page.',
      ],
    },
    {
      id: 'tabs-and-billing',
      heading: 'F&B tabs, house accounts and dues',
      paragraphs: [
        'Members expect to sign for a drink rather than pay at the bar, so every food and beverage tab must end up on the member’s house account. The simplest rhythm is one monthly statement per member or household, combining dues, tabs, events and private hire.',
      ],
      bullets: [
        'Connect the till to the member record, so no tab is retyped by hand.',
        'Set a credit limit per account and a rule for overdue balances.',
        'Put a Swiss QR-bill on each statement, and offer TWINT or card as well.',
        'Match the bank file against what is owed instead of ticking payments by hand.',
        'Export clean figures to accounting, and correct mistakes with visible reversals.',
      ],
      tip: 'Ask your fiduciary how dues, joining fees and food and beverage are treated for VAT before you set your rates. Our guide to the [Swiss QR-bill for clubs](page:guide-qr-bill) covers the payment side.',
    },
    {
      id: 'communication',
      heading: 'Communicate like a club, not a brand',
      paragraphs: [
        'Members want to feel informed, not marketed to. A monthly newsletter with the programme, an occasional personal note from the manager and a reminder before each event are usually enough. In Switzerland, let each member choose French, German or English.',
        'Send newsletters only to people who agreed to receive them, with a one-click unsubscribe. Statements and booking confirmations are part of the membership itself.',
      ],
    },
    {
      id: 'renewals',
      heading: 'Renewals and retention',
      paragraphs: [
        'Renewal is decided long before the renewal date. Watch the signals: no visit in three months, no more event bookings, an unpaid statement. Call those members before the renewal notice goes out, not after the resignation letter arrives.',
        'Write the term, renewal and notice rules clearly in your statutes or membership agreement. If your club is an association under the Swiss Civil Code, the law sets a default: members may leave with six months’ notice to the end of the calendar year or of the administrative period, and the statutes can shorten that period but not lengthen it. Check your own rules with an advisor. Our guide to [membership renewals and retention](page:guide-member-retention) goes further.',
      ],
    },
    {
      id: 'staff-roles',
      heading: 'Staff roles: who does what',
      paragraphs: [
        'Even a small club runs better when each task has an owner. Titles vary, but the roles are much the same everywhere:',
      ],
      bullets: [
        'General manager: programme, budget, team and the relationship with the board.',
        'Membership manager: enquiries, visits, applications, onboarding and renewals.',
        'Events and food and beverage: calendar, reservations, private hire and service.',
        'Front desk: welcoming members and guests, check-ins and daily requests.',
        'Finance: dues, statements, payments, reminders and the accounting export.',
        'Board or committee: admissions, rules and the club’s long-term direction.',
      ],
      tip: 'Give each person access to what their role needs and no more. The bar team needs to open a tab, not to read the committee’s notes.',
    },
    {
      id: 'data-and-privacy',
      heading: 'Member data and privacy under the Swiss FADP',
      paragraphs: [
        'A club’s member list is its most sensitive asset. The revised Swiss Federal Act on Data Protection (FADP), in force since 1 September 2023, applies to associations and companies alike. In practice: collect only what you need, explain in a privacy notice what you do with the data, answer requests for access, keep data secure and delete it when it is no longer needed.',
        'Be especially careful with sensitive data such as health, religious or political views and criminal proceedings: a club rarely needs any of it. Ask before publishing photos of members taken at events. Accounting records follow their own rules and are generally kept for ten years in Switzerland. Our [FADP checklist for clubs](page:guide-fadp-checklist) lists the steps; for your specific case, check with a data protection advisor.',
      ],
    },
    {
      id: 'tools',
      heading: 'Choosing private club management software',
      paragraphs: [
        'Many clubs run on a member spreadsheet, an email tool, a till and accounting software that know nothing of each other, and every hand-over is where an enquiry or a tab gets lost. When you compare members club management software, check that it offers:',
      ],
      bullets: [
        'One record per person, from first enquiry to member, renewal and departure.',
        'Online applications, decided in one list.',
        'Agreements signed online, with the joining fee collected at signing.',
        'Events with waitlists, and tabs linked to your till.',
        'Dues, statements and Swiss payment methods, with an accounting export.',
        'Email with consent, several languages, roles and an audit log.',
      ],
      subsections: [
        {
          heading: 'Where CadenceE fits',
          paragraphs: [
            'CadenceE is a Swiss [CRM for private members’ clubs](page:private-clubs) that covers this list in one place: applications, contracts signed online with a deposit, [membership management](page:membership) with households and renewals, events, tabs with a Lightspeed POS connection, and [billing with QR-bill and TWINT](page:billing). It was built alongside Pavillon 46, a private club in Vaud. Whatever you choose, our guide to [choosing a CRM](page:guide-choose-a-crm) helps you compare, and [from Excel to a CRM](page:guide-excel-to-crm) covers the move. You can also [try the demo](/login?demo=1).',
          ],
        },
      ],
    },
  ],
  faq: {
    heading: 'Running a private members’ club: frequent questions',
    items: [
      {
        q: 'What is a private members’ club?',
        a: 'A club whose spaces and services are reserved for members and their guests. Members are admitted through an application, pay a joining fee and annual dues, and usually settle food, drink and events on a house account.',
      },
      {
        q: 'How many members should a private club have?',
        a: 'Work back from capacity: how many people the club hosts comfortably at its busiest, and how often an average member visits. Set a cap per category that keeps those evenings pleasant, and keep a waitlist once you reach it.',
      },
      {
        q: 'Is a private club an association under Swiss law?',
        a: 'It can be. Many Swiss clubs are associations under the Civil Code, whose statutes set out admission, dues, exclusion and resignation. Others are run by a company that sells memberships under contract. The legal form changes who decides and how members leave, so check with an advisor.',
      },
      {
        q: 'How should a club bill members for food and drink?',
        a: 'Post each tab to the member’s house account and send one monthly statement that also covers dues and events, with a Swiss QR-bill and TWINT or card as alternatives. See [club billing and payments](page:billing).',
      },
      {
        q: 'Does the Swiss FADP apply to members’ clubs?',
        a: 'Yes. It applies to private persons and organisations that process personal data, including associations and companies running clubs. Collect only what you need, inform members, answer access requests and keep data secure.',
      },
      {
        q: 'What software do private members’ clubs use?',
        a: 'Often a member database, a booking tool, a till and accounting software side by side. A club CRM such as CadenceE brings enquiries, members, events, tabs and billing into one system.',
      },
    ],
  },
  cta: {
    heading: 'Run the whole club from one record per member',
    text: 'CadenceE follows each person from first enquiry to signed agreement, member, tab, statement and renewal, in French, German or English.',
  },
};

export default guide;
