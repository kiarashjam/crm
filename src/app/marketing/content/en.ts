// The English marketing pages. The English home page is the animated Homepage
// (src/app/pages/Homepage.tsx); every other page is laid out by MarketingPage from these words.
//
// Only what CadenceE does today: no price, no customer but Pavillon 46, no rating, no award.

import { enPricing } from './pricing';
import type { LanguageContent } from './types';

const en: LanguageContent = {
  template: {
    featuresEyebrow: 'Features',
    stepsEyebrow: 'How it works',
    faqEyebrow: 'FAQ',
    solutionsEyebrow: 'Solutions',
    stepLabel: 'Step',
  },
  pages: {
    features: {
      eyebrow: 'All features',
      h1: 'CRM features for clubs, hospitality',
      h1Accent: 'and sales teams',
      lead: 'CadenceE brings the sales side and the club side of a business into one CRM: leads and contracts, members and events, tabs and billing, email and reports, with an AI assistant that only acts when you confirm. Here is everything it does, module by module.',
      heroCardTitle: 'In one workspace',
      heroPoints: [
        'Leads tracked in five phases, from outreach to deposit',
        'Contracts signed online, deposit collected at signing',
        'Members, households, renewals and events',
        'Tabs, dues, statements and Swiss payments',
        'Email, reports and an AI assistant built on Claude',
      ],
      intro: {
        heading: 'One CRM instead of five tools',
        paragraphs: [
          'Most clubs and hospitality businesses run on a patchwork: a spreadsheet of enquiries, a separate tool for contracts, another for membership, a till for the bar and an email platform that knows none of the others. Every hand-over between them is a place where a lead is forgotten or a payment is missed.',
          'CadenceE replaces the patchwork with one record per person and per company. The enquiry that arrived through your website becomes a lead; the lead signs a contract and pays a deposit; and the same record becomes an Active member with a portal, a tab and a monthly statement. Nothing is typed twice.',
          'A workspace can use the sales side, the club side or both, and the whole app is available in English, French and German. Each module below links to a page that explains it in depth.',
        ],
      },
      sections: [
        {
          heading: 'Leads and sales',
          paragraphs: [
            'The [five-phase lead tracker](page:sales) follows every opportunity through outreach, meeting, contract, signature and deposit, so the team always knows the next step. Leads are scored and assigned, protected against duplicates, and record who referred them and which partner they came through.',
          ],
          bullets: [
            'Lead scoring and assignment rules',
            'Duplicate protection when leads arrive or are imported',
            'Imports with a dry run, and undo',
            'Public enquiry forms and a website lead webhook',
          ],
          link: 'sales',
        },
        {
          heading: 'Contracts, e-signature and deposits',
          paragraphs: [
            'Contracts are generated from your templates and sent for [electronic signature](page:e-signature). The person signing needs no account. The deposit is collected at signing, by card or Swiss QR-bill, and a contract signed on paper can be uploaded instead.',
          ],
          bullets: [
            'Contract templates filled from the lead',
            'E-signature without an account for the signer',
            'Deposit at signing by card or QR-bill',
            'Upload of contracts signed on paper',
          ],
          link: 'e-signature',
        },
        {
          heading: 'Members and memberships',
          paragraphs: [
            'Once the contract is signed, one step turns the lead into an Active member, with a welcome email and access to the member portal. [Membership management](page:membership) covers members, households, applications, renewals with at-risk scores and corporate memberships.',
          ],
          link: 'membership',
        },
        {
          heading: 'Events, reservations and hospitality',
          paragraphs: [
            'Events and reservations with waitlists, private-event hire, visits and check-ins, bar and restaurant tabs and a Lightspeed POS connection: the [hospitality side](page:hospitality) of a club or venue lives in the same CRM as its members and guests.',
          ],
          link: 'hospitality',
        },
        {
          heading: 'Billing and payments',
          paragraphs: [
            'House accounts, recurring dues and monthly PDF statements, paid by card, TWINT, SEPA or Swiss QR-bill. Bank files are matched against what is owed, and the accounting export goes to Bexio or Abacus from a ledger that is only ever reversed, never edited. See [club billing and payments](page:billing).',
          ],
          bullets: [
            'Recurring dues and house accounts',
            'Monthly PDF statements',
            'Card, TWINT, SEPA and QR-bill',
            'Bexio and Abacus export',
          ],
          link: 'billing',
        },
        {
          heading: 'Email and outreach',
          paragraphs: [
            'One outbox sends every email, retries when a server refuses and handles bounces. Sequences and automations follow up for you, newsletters can invite leads to book a meeting, and campaigns respect consent with a one-click unsubscribe. More on [email automation](page:email).',
          ],
          link: 'email',
        },
        {
          heading: 'AI assistant',
          paragraphs: [
            'The [AI assistant](page:ai), built on Claude, reads your workspace and proposes changes for a person to confirm. The AI sales writer drafts emails in your tone, and the morning brief lists what needs attention today.',
          ],
          link: 'ai',
        },
        {
          heading: 'Reports, privacy and the platform',
          paragraphs: [
            'Reports and a forecast, an audit log of every change, duplicate merging, and privacy requests for export and erasure under the GDPR and the [Swiss FADP](page:swiss). People sign in with Google, Microsoft or a passkey, can add two-factor authentication, and get the role and permissions their job needs. Several workspaces can sit side by side, and System health monitoring shows that everything is running.',
          ],
          bullets: [
            'Reports, forecast and audit log',
            'GDPR and FADP privacy requests',
            'Google, Microsoft and passkey sign-in',
            'Roles, permissions and multiple workspaces',
          ],
          link: 'swiss',
        },
      ],
      features: {
        heading: 'Built for the whole team',
        intro: 'From the front desk to the finance office, everyone works on the same records.',
        items: [
          { icon: 'leads', title: 'Five-phase lead tracker', text: 'Outreach, meeting, contract, signature, deposit: every lead has a phase and a next step.' },
          { icon: 'signature', title: 'E-signature contracts', text: 'Contracts from templates, signed online without creating an account.' },
          { icon: 'members', title: 'Members and households', text: 'Applications, renewals with at-risk scores, households and corporate memberships.' },
          { icon: 'reservations', title: 'Events and reservations', text: 'Waitlists, private-event hire, visits and check-ins.' },
          { icon: 'tabs', title: 'Tabs and POS', text: 'Bar and restaurant tabs, house accounts and a Lightspeed POS connection.' },
          { icon: 'payments', title: 'Swiss payments', text: 'Card, TWINT, SEPA and QR-bill, with bank-file matching.' },
          { icon: 'accounting', title: 'Accounting export', text: 'Export to Bexio or Abacus from a ledger that is reversed, never edited.' },
          { icon: 'automations', title: 'Email automation', text: 'Sequences, automations, newsletters and campaigns from one outbox.' },
          { icon: 'ai', title: 'AI assistant', text: 'Built on Claude: proposes changes, drafts emails, prepares a morning brief.' },
          { icon: 'loyalty', title: 'Loyalty, gift cards and donations', text: 'Reward regulars, sell gift cards and record donations in the same CRM.' },
          { icon: 'roles', title: 'Roles and permissions', text: 'Each colleague sees and changes what their role allows.' },
          { icon: 'languages', title: 'French, German and English', text: 'The whole app in three languages, chosen by each person.' },
        ],
      },
      steps: {
        heading: 'How a team goes live on CadenceE',
        items: [
          { title: 'Create your workspace', text: 'Sign up, name the workspace and invite colleagues with the roles they need.' },
          { title: 'Bring your data', text: 'Import leads, contacts and members from spreadsheets. A dry run shows the result first, and an import can be undone.' },
          { title: 'Connect your channels', text: 'Add the enquiry form or webhook to your website, your contract templates and your payment methods.' },
          { title: 'Work from one place', text: 'Follow leads to signature, turn them into members and bill them, with reports and a morning brief to keep track.' },
        ],
      },
      faq: {
        heading: 'Questions about CadenceE’s features',
        items: [
          { q: 'Is CadenceE a sales CRM or a club management system?', a: 'Both. A workspace can use the sales side (leads, deals, contracts), the club side (members, events, tabs, billing) or both together. Most clubs use both, so a lead becomes a member without changing system.' },
          { q: 'Which languages does CadenceE support?', a: 'The app is available in English, French and German, and each person chooses their own. Prepared emails are available in French and English.' },
          { q: 'Can we import our existing data?', a: 'Yes. Leads, contacts, companies and members can be imported from spreadsheets. Every import starts with a dry run that shows what will happen, and an import can be undone.' },
          { q: 'Does CadenceE handle payments?', a: 'Yes. Members and clients pay by card, TWINT, SEPA or Swiss QR-bill, deposits are collected at contract signing, and bank files are matched against what is owed. See [billing and payments](page:billing).' },
          { q: 'Does the AI change data on its own?', a: 'No. The [AI assistant](page:ai) proposes changes, and a person confirms each one before anything is saved.' },
          { q: 'How is access controlled?', a: 'People sign in with email and password, Google, Microsoft or a passkey, can add two-factor authentication, and get roles and permissions. An audit log records every change.' },
        ],
      },
      related: ['private-clubs', 'sales', 'swiss'],
      cta: {
        heading: 'See CadenceE with your own data',
        text: 'Create a workspace and import a first list of leads or members, or ask us for a walkthrough.',
      },
    },

    'private-clubs': {
      eyebrow: 'CRM for private clubs',
      h1: 'The CRM built for',
      h1Accent: 'private members’ clubs',
      lead: 'From the first enquiry to the monthly statement, CadenceE runs a private club in one place: applications, contracts and deposits, members and households, events, tabs and renewals. It was built in Switzerland, with a club.',
      heroCardTitle: 'What a club runs in CadenceE',
      heroPoints: [
        'Enquiries and applications from your website',
        'Membership contracts signed online, with a deposit',
        'Members, households and corporate memberships',
        'Events, reservations and private hire',
        'Bar tabs, dues and monthly statements',
      ],
      intro: {
        heading: 'Why a private club needs its own CRM',
        paragraphs: [
          'A private members’ club sells something unusual: not a product, but a place in a community. The path to membership is long and personal. Someone hears about the club, asks for a visit, meets the membership team, applies, signs, pays a joining deposit and, from then on, comes back for dinners, events and drinks.',
          'Generic CRMs stop at the signed deal, and club software tends to start at the member. CadenceE covers the whole journey in one record. It was built alongside Pavillon 46, a private members’ club in La Croix-sur-Lutry, Vaud, which uses it every day to sell memberships and to run the club.',
        ],
      },
      sections: [
        {
          heading: 'From enquiry to member in one record',
          paragraphs: [
            'An enquiry arrives through your website’s form or webhook and becomes a lead. The [five-phase tracker](page:sales) takes it through outreach, meeting, contract, signature and deposit, and records who referred the prospect, so introductions by existing members are never lost.',
            'When the contract is signed and the deposit paid, one step turns the lead into an Active member. The welcome email goes out and the member portal opens, with nothing typed twice.',
          ],
          bullets: [
            'Public enquiry and application forms',
            'Referred-by and partner links on every lead',
            'Membership contracts from templates, [signed online](page:e-signature)',
            'One-step conversion into an Active member',
          ],
        },
        {
          heading: 'Membership, households and renewals',
          paragraphs: [
            'Members belong to households, and companies can hold corporate memberships for several of their people. Applications are reviewed in one list. Renewals carry an at-risk score, so the team calls the members who are drifting away before they leave. Read more about [membership management](page:membership).',
          ],
          bullets: [
            'Households and corporate memberships',
            'Applications reviewed in one place',
            'Renewals with at-risk scores',
            'A member portal for every member',
          ],
        },
        {
          heading: 'Life in the club: events, visits and tabs',
          paragraphs: [
            'Events and reservations have waitlists, and rooms can be hired for members’ private occasions. Visits and check-ins show who comes and how often. Bar and restaurant spending goes onto tabs, with a Lightspeed POS connection, and ends up on the member’s house account.',
            'At the end of the month, each member receives a PDF statement and pays by card, TWINT, SEPA or Swiss QR-bill. See [club billing and payments](page:billing).',
          ],
        },
        {
          heading: 'Discretion built in',
          paragraphs: [
            'A club’s member list is its most sensitive asset. CadenceE gives each colleague a role with only the permissions they need, records every change in an audit log, and supports sign-in with a passkey and two-factor authentication. Requests to export or erase personal data are handled under the GDPR and the Swiss FADP.',
          ],
        },
      ],
      features: {
        heading: 'Everything a private club runs on',
        items: [
          { icon: 'applications', title: 'Applications', text: 'Collect applications through a public form and decide on them in one list.' },
          { icon: 'contract', title: 'Membership contracts', text: 'Generate contracts from templates and send them for e-signature.' },
          { icon: 'deposit', title: 'Joining deposits', text: 'Collect the deposit at signing, by card or Swiss QR-bill.' },
          { icon: 'households', title: 'Households', text: 'Group partners and family members under one household.' },
          { icon: 'corporate', title: 'Corporate memberships', text: 'Let a company hold memberships for several of its people.' },
          { icon: 'renewals', title: 'Renewals with at-risk scores', text: 'See which members may not renew while there is time to act.' },
          { icon: 'events', title: 'Events and waitlists', text: 'Publish events, take reservations and fill places from the waitlist.' },
          { icon: 'private-event', title: 'Private-event hire', text: 'Handle members’ private events from enquiry to invoice.' },
          { icon: 'tabs', title: 'Tabs and house accounts', text: 'Bar and restaurant tabs flow into each member’s house account.' },
          { icon: 'statements', title: 'Monthly statements', text: 'Send each member a PDF statement every month.' },
          { icon: 'loyalty', title: 'Loyalty, gift cards, donations', text: 'Reward loyal members, sell gift cards and record donations.' },
          { icon: 'portal', title: 'Member portal', text: 'Members sign in with an emailed link to see their club account.' },
        ],
      },
      steps: {
        heading: 'How a club moves to CadenceE',
        items: [
          { title: 'Set up the club', text: 'Create the workspace, add your contract templates and payment methods, and invite your team.' },
          { title: 'Import members and prospects', text: 'Bring in current members and open enquiries from spreadsheets, with a dry run first.' },
          { title: 'Open the doors online', text: 'Put the enquiry or application form on your website, so new interest lands straight in the CRM.' },
          { title: 'Run the season', text: 'Follow prospects to signature, welcome new members, run events and send monthly statements.' },
        ],
      },
      faq: {
        heading: 'Private club CRM: frequent questions',
        items: [
          { q: 'Who uses CadenceE today?', a: 'Pavillon 46, a private members’ club in La Croix-sur-Lutry, Vaud, uses CadenceE every day to sell memberships and run the club. CadenceE was built alongside it.' },
          { q: 'Can prospective members apply online?', a: 'Yes. The club publishes an application form and an enquiry form; what people send arrives as applications or leads, with duplicate protection, and the team decides from one list.' },
          { q: 'How does a prospect become a member?', a: 'The lead moves through outreach, meeting, contract, signature and deposit. Once the contract is signed and the deposit paid, one step converts the lead into an Active member, sends the welcome email and opens the member portal.' },
          { q: 'Can we bill bar and restaurant spending to members?', a: 'Yes. Spending goes onto tabs, with a connection to Lightspeed POS, and onto the member’s house account. Members receive a monthly PDF statement and pay by card, TWINT, SEPA or Swiss QR-bill.' },
          { q: 'Is member data kept confidential?', a: 'Each colleague gets a role with specific permissions, every change is recorded in an audit log, and sign-in supports passkeys and two-factor authentication. Export and erasure requests are handled under the GDPR and the Swiss FADP.' },
          { q: 'Does CadenceE work in French and German?', a: 'Yes. The whole app is available in French, German and English, and each person picks their own language. See [Swiss CRM](page:swiss).' },
        ],
      },
      related: ['membership', 'billing', 'hospitality'],
      cta: {
        heading: 'Run your club from one CRM',
        text: 'Create a workspace for your club, or ask for a demo from the team that built it.',
      },
    },

    membership: {
      eyebrow: 'Membership management software',
      h1: 'Membership management software',
      h1Accent: 'for clubs',
      lead: 'Keep every member, household and corporate account in one place, see renewals coming with an at-risk score, and give members a portal of their own. CadenceE handles the membership and the money that comes with it.',
      heroCardTitle: 'Membership in CadenceE',
      heroPoints: [
        'Members, households and corporate memberships',
        'Applications reviewed in one list',
        'Renewals with at-risk scores',
        'A member portal with emailed sign-in links',
        'Recurring dues and monthly statements',
      ],
      intro: {
        heading: 'Membership is a relationship, not a list',
        paragraphs: [
          'A spreadsheet can list members. It cannot tell you that a family’s membership renews next month, that one of them has not visited since spring, or that their last statement is still open. Membership management software should.',
          'CadenceE keeps the member record connected to everything around it: the household, the company paying for a corporate membership, the events booked, the visits, the tab and the dues. The team sees the full picture, and members see their own through the portal.',
        ],
      },
      sections: [
        {
          heading: 'Members, households and corporate memberships',
          paragraphs: [
            'Each member has one record with their contact details, membership, language and history. Partners and children can be grouped into a household, so communication and billing follow the family rather than the individual. Companies can hold corporate memberships that cover several of their employees.',
          ],
          bullets: [
            'One record per member, with full history',
            'Households for families and partners',
            'Corporate memberships for companies',
            'Duplicate protection and merging',
          ],
        },
        {
          heading: 'Applications and new members',
          paragraphs: [
            'New members can apply through a public application form on your website. Applications arrive in one list, where the committee or membership team reviews them and decides. When membership is sold through the [sales pipeline](page:sales), a signed contract and a paid deposit turn the lead into an Active member in one step, with a welcome email.',
          ],
        },
        {
          heading: 'Renewals before they become cancellations',
          paragraphs: [
            'Every renewal has an at-risk score, so you know which members are likely to leave while there is still time to call them. Renewals, visits and spending sit side by side, which makes the conversation easier: you know when they last came and what they enjoyed.',
          ],
        },
        {
          heading: 'Dues, statements and the member portal',
          paragraphs: [
            'Recurring dues are billed on schedule, together with tabs and other charges, and each member receives a monthly PDF statement. Members pay by card, TWINT, SEPA or Swiss QR-bill (see [billing and payments](page:billing)). In the member portal they sign in with an emailed link, with no password to remember.',
          ],
        },
      ],
      features: {
        heading: 'Membership tools in CadenceE',
        items: [
          { icon: 'members', title: 'Member records', text: 'Contact details, membership, language and full history in one place.' },
          { icon: 'households', title: 'Households', text: 'Group family members and partners for communication and billing.' },
          { icon: 'corporate', title: 'Corporate memberships', text: 'Companies hold memberships for several employees.' },
          { icon: 'applications', title: 'Online applications', text: 'A public form feeds applications into one review list.' },
          { icon: 'renewals', title: 'At-risk renewals', text: 'A score shows which members may not renew.' },
          { icon: 'portal', title: 'Member portal', text: 'Members sign in with an emailed link, no password needed.' },
          { icon: 'dues', title: 'Recurring dues', text: 'Dues billed on schedule, alongside tabs and other charges.' },
          { icon: 'statements', title: 'PDF statements', text: 'A monthly statement for each member.' },
          { icon: 'visits', title: 'Visits and check-ins', text: 'See who comes, and how often.' },
          { icon: 'events', title: 'Events and reservations', text: 'Members book events, with a waitlist when they are full.' },
          { icon: 'newsletter', title: 'Member communication', text: 'Newsletters and campaigns with consent and one-click unsubscribe.' },
          { icon: 'privacy', title: 'Privacy requests', text: 'Export or erase a member’s data under the GDPR and the Swiss FADP.' },
        ],
      },
      steps: {
        heading: 'From spreadsheet to membership system',
        items: [
          { title: 'Import your members', text: 'Upload your member list. A dry run shows what will be created before anything is saved, and an import can be undone.' },
          { title: 'Group and classify', text: 'Put members into households, link corporate memberships and set up recurring dues.' },
          { title: 'Open the portal', text: 'Invite members to the member portal, where they sign in with an emailed link.' },
          { title: 'Watch renewals', text: 'Follow renewals by at-risk score and talk to members before they decide to leave.' },
        ],
      },
      faq: {
        heading: 'Membership management: frequent questions',
        items: [
          { q: 'What is membership management software?', a: 'Software that keeps a club’s members, memberships, renewals and payments in one system. CadenceE adds a sales pipeline in front of it, so prospective members are followed from the first enquiry. See [CRM for private clubs](page:private-clubs).' },
          { q: 'Can families share a membership?', a: 'Members can be grouped into a household, so partners and children are handled together for communication and billing.' },
          { q: 'Do you support corporate memberships?', a: 'Yes. A company can hold a corporate membership that covers several of its people, each with their own member record.' },
          { q: 'How does the at-risk score help with renewals?', a: 'Each renewal shows how likely the member is to leave, so the team can focus its calls and attention on those members before the renewal date.' },
          { q: 'Do members need a password for the portal?', a: 'No. Members sign in to the member portal with a link sent by email.' },
          { q: 'Can we import our current member list?', a: 'Yes. Member imports start with a dry run that shows what will happen, and an import can be undone if something is wrong.' },
        ],
      },
      related: ['private-clubs', 'billing', 'email'],
      cta: {
        heading: 'Give your members the attention they expect',
        text: 'Create a workspace and import your member list, or ask for a demo.',
      },
    },

    hospitality: {
      eyebrow: 'Hospitality CRM',
      h1: 'A hospitality CRM for restaurants,',
      h1Accent: 'hotels and event venues',
      lead: 'CadenceE connects the guest, the booking and the bill: reservations with waitlists, private-event hire, bar and restaurant tabs with a Lightspeed POS connection, and guest email, in one CRM built in Switzerland.',
      heroCardTitle: 'For hospitality teams',
      heroPoints: [
        'Reservations and events with waitlists',
        'Private-event hire from enquiry to deposit',
        'Bar and restaurant tabs, Lightspeed POS',
        'House accounts and monthly statements',
        'Guest newsletters with consent',
      ],
      intro: {
        heading: 'Know your guests, not just your covers',
        paragraphs: [
          'In hospitality, the best customers come back. But what you know about them is scattered: the booking in one system, the bill in the till, the event enquiry in someone’s inbox and the newsletter list somewhere else.',
          'CadenceE brings these together around the guest. A regular’s reservations, private events, spending and emails sit on one record, so the team can recognise them, invite them back and bill them correctly. It is the CRM behind Pavillon 46, a members’ club with a restaurant and a bar in La Croix-sur-Lutry, Vaud.',
        ],
      },
      sections: [
        {
          heading: 'Reservations, events and waitlists',
          paragraphs: [
            'Publish events, take reservations and keep a waitlist when places run out, so a cancellation becomes a booking rather than an empty seat. Visits and check-ins record who actually came, and hospitality reports sit next to the sales and membership figures.',
          ],
        },
        {
          heading: 'Private-event hire, from enquiry to deposit',
          paragraphs: [
            'Private dinners, receptions and company events start as enquiries. CadenceE tracks them like any sales opportunity: the [five-phase pipeline](page:sales) takes the request through meeting, contract, signature and deposit, and the contract is signed online with [e-signature](page:e-signature).',
          ],
          bullets: [
            'Event enquiries from your website',
            'Contracts from your templates',
            'Deposit at signing, by card or QR-bill',
            'The event on the same record as the guest',
          ],
        },
        {
          heading: 'Tabs, POS and house accounts',
          paragraphs: [
            'Bar and restaurant spending goes onto tabs. A Lightspeed POS connection brings sales from the till into the CRM, and regulars or members can run a house account that is settled with a monthly PDF statement. Payments arrive by card, TWINT, SEPA or Swiss QR-bill; see [billing and payments](page:billing).',
          ],
        },
        {
          heading: 'What CadenceE is not',
          paragraphs: [
            'CadenceE is a CRM, not a hotel property management system: it does not manage room inventory or channel bookings. It works alongside your till through the Lightspeed connection rather than replacing it. Its job is the relationship around the stay or the meal: the enquiry, the event, the account and the follow-up.',
          ],
        },
      ],
      features: {
        heading: 'Hospitality features',
        items: [
          { icon: 'reservations', title: 'Reservations', text: 'Bookings for events and dinners, with waitlists.' },
          { icon: 'private-event', title: 'Private-event hire', text: 'Enquiry, contract, deposit and invoice for private events.' },
          { icon: 'tabs', title: 'Bar and restaurant tabs', text: 'Spending recorded per guest or member.' },
          { icon: 'pos', title: 'Lightspeed POS connection', text: 'Sales from the till reach the CRM.' },
          { icon: 'house-account', title: 'House accounts', text: 'Regulars settle once a month.' },
          { icon: 'statements', title: 'Monthly PDF statements', text: 'A clear statement for every account.' },
          { icon: 'visits', title: 'Visits and check-ins', text: 'Who came, when, and how often.' },
          { icon: 'loyalty', title: 'Loyalty', text: 'Reward the guests who come back.' },
          { icon: 'gift-card', title: 'Gift cards', text: 'Sell gift cards and track what is left on them.' },
          { icon: 'newsletter', title: 'Guest newsletters', text: 'News and invitations with consent and one-click unsubscribe.' },
          { icon: 'payments', title: 'Swiss payments', text: 'Card, TWINT, SEPA and QR-bill.' },
          { icon: 'reports', title: 'Reports', text: 'Hospitality reports alongside sales and membership.' },
        ],
      },
      steps: {
        heading: 'How a venue starts with CadenceE',
        items: [
          { title: 'Set up the venue', text: 'Create the workspace, add your event types and payment methods, and connect Lightspeed POS.' },
          { title: 'Bring your guest list', text: 'Import guests and regulars from spreadsheets, with a dry run first and undo afterwards.' },
          { title: 'Take enquiries online', text: 'Put the enquiry form on your website so private-event requests arrive as leads.' },
          { title: 'Serve, bill and follow up', text: 'Run reservations and tabs, send statements, and invite guests back with newsletters.' },
        ],
      },
      faq: {
        heading: 'Hospitality CRM: frequent questions',
        items: [
          { q: 'Is CadenceE a restaurant reservation system?', a: 'CadenceE takes reservations for events and dinners, with waitlists, next to the guest’s history and bill. It is a CRM first, built around the relationship with the guest.' },
          { q: 'Does CadenceE replace our POS?', a: 'No. CadenceE connects to Lightspeed POS so sales reach the CRM, and keeps tabs and house accounts; the till stays your POS.' },
          { q: 'Can it manage hotel rooms?', a: 'No. CadenceE is not a property management system and does not manage room inventory. Hotels use it for private events, corporate clients, guest relationships and the billing of accounts.' },
          { q: 'How are private events handled?', a: 'An event enquiry becomes a lead and follows the five phases: outreach, meeting, contract, signature and deposit. The contract is signed online and the deposit is collected at signing.' },
          { q: 'Which payment methods can guests use?', a: 'Card, TWINT, SEPA and the Swiss QR-bill.' },
          { q: 'Can we email our guests?', a: 'Yes. Newsletters and campaigns go out from one outbox, with consent and one-click unsubscribe. See [email automation](page:email).' },
        ],
      },
      related: ['private-clubs', 'e-signature', 'billing'],
      cta: {
        heading: 'Bring your guests, events and bills together',
        text: 'Create a workspace for your venue, or ask for a demo.',
      },
    },

    sales: {
      eyebrow: 'Sales CRM',
      h1: 'A sales CRM that follows every lead',
      h1Accent: 'from outreach to deposit',
      lead: 'CadenceE tracks each lead through five phases (outreach, meeting, contract, signature and deposit) with scoring, assignment and duplicate protection, a deals board, and an AI sales writer for the emails in between.',
      heroCardTitle: 'The five phases',
      heroPoints: [
        'Outreach: first contact and follow-ups',
        'Meeting: a visit, a call or a demo',
        'Contract: generated from your template',
        'Signature: signed online, no account needed',
        'Deposit: paid at signing, by card or QR-bill',
      ],
      intro: {
        heading: 'A pipeline that ends with money in the bank',
        paragraphs: [
          'Many sales pipelines end at “won”. In practice, a deal is only finished when the contract is signed and the deposit has arrived. CadenceE’s lead tracker is built around that: five phases, each with a clear next step, ending with the deposit.',
          'Because contracts, e-signature and payments live in the same CRM, the pipeline moves on as the client signs and pays. The team spends its time on conversations rather than on updating spreadsheets.',
        ],
      },
      sections: [
        {
          heading: 'Five phases, one next step at a time',
          paragraphs: [
            'Outreach, meeting, contract, signature, deposit. Every lead sits in one phase, with its tasks and activities attached, so anyone on the team can pick it up. The deals board shows opportunities as columns you drag across, and saved views keep each colleague’s own list one click away.',
          ],
          bullets: [
            'Leads and deals on a board',
            'Tasks and activities on every lead',
            'Saved views for each colleague',
            'A calendar of meetings and follow-ups',
          ],
        },
        {
          heading: 'Lead scoring, assignment and clean data',
          paragraphs: [
            'Leads are scored so the most promising get attention first, and assignment rules route each new lead to the right person. Duplicate protection catches the same person or company arriving twice, and duplicates can be merged. Each lead records who referred it and which partner it came through.',
          ],
        },
        {
          heading: 'Leads that arrive by themselves',
          paragraphs: [
            'Public enquiry forms and a website lead webhook create leads straight from your site. Existing lists are imported from spreadsheets: a dry run shows what will happen first, and an import can be undone.',
          ],
        },
        {
          heading: 'The next email, drafted in seconds',
          paragraphs: [
            'The AI sales writer drafts emails, follow-ups and notes in your tone. Sequences send follow-ups on a schedule, and newsletters can invite leads to book a meeting through a booking page. See [email automation](page:email) and the [AI assistant](page:ai).',
          ],
        },
      ],
      features: {
        heading: 'Sales features',
        items: [
          { icon: 'leads', title: 'Five-phase lead tracker', text: 'Outreach, meeting, contract, signature, deposit.' },
          { icon: 'deals', title: 'Deals board', text: 'Drag opportunities from one stage to the next.' },
          { icon: 'score', title: 'Lead scoring', text: 'Focus on the leads most likely to sign.' },
          { icon: 'partner', title: 'Referrals and partners', text: 'Record who referred each lead and through which partner.' },
          { icon: 'duplicates', title: 'Duplicate protection', text: 'Stop the same lead from being entered twice, and merge duplicates.' },
          { icon: 'import', title: 'Imports with dry run and undo', text: 'Bring in lists without the risk.' },
          { icon: 'forms', title: 'Enquiry forms', text: 'Public forms that create leads.' },
          { icon: 'webhook', title: 'Website lead webhook', text: 'Send leads from your website or another tool.' },
          { icon: 'signature', title: 'E-signature', text: 'Contracts signed online without an account.' },
          { icon: 'deposit', title: 'Deposits at signing', text: 'By card or Swiss QR-bill.' },
          { icon: 'writer', title: 'AI sales writer', text: 'Emails and follow-ups drafted in your tone.' },
          { icon: 'forecast', title: 'Reports and forecast', text: 'See what is likely to close, and when.' },
        ],
      },
      steps: {
        heading: 'How a sales team works in CadenceE',
        items: [
          { title: 'Capture', text: 'Leads arrive from website forms, the webhook, imports or the team itself.' },
          { title: 'Qualify and assign', text: 'Scores and assignment rules put each lead in front of the right person.' },
          { title: 'Meet and propose', text: 'Book the meeting, then send a contract from your template.' },
          { title: 'Sign and get paid', text: 'The client signs online and pays the deposit, and the lead reaches its last phase.' },
        ],
      },
      faq: {
        heading: 'Sales CRM: frequent questions',
        items: [
          { q: 'What are the five phases?', a: 'Outreach, meeting, contract, signature and deposit. A lead moves forward as each step is done, and the last phase ends when the deposit is paid.' },
          { q: 'Can leads come in from our website?', a: 'Yes, through a public enquiry form or a website lead webhook. Both create leads directly, with duplicate protection.' },
          { q: 'How does lead assignment work?', a: 'Assignment rules route each new lead to the right colleague, and a lead can be reassigned at any time.' },
          { q: 'What does the AI sales writer do?', a: 'It drafts sales emails, follow-ups and notes in your brand’s tone for a person to review and send. See the [AI assistant](page:ai).' },
          { q: 'Can I see a forecast?', a: 'Yes. Reports include a forecast of the pipeline next to the team’s activity.' },
          { q: 'What happens after the deal is signed?', a: 'For clubs, a signed and paid lead becomes an Active member in one step, with a welcome email and the member portal. See [CRM for private clubs](page:private-clubs).' },
        ],
      },
      related: ['e-signature', 'email', 'ai'],
      cta: {
        heading: 'Follow every lead all the way to the deposit',
        text: 'Create a workspace and import your leads, or ask for a demo.',
      },
    },

    'e-signature': {
      eyebrow: 'Contracts and e-signature',
      h1: 'Contracts with e-signature',
      h1Accent: 'and the deposit at signing',
      lead: 'Generate contracts from your templates, send them to be signed online and collect the deposit in the same step, by card or Swiss QR-bill. The person signing needs no account, and contracts signed on paper can be uploaded too.',
      heroCardTitle: 'From template to deposit',
      heroPoints: [
        'Contracts generated from your templates',
        'Signed online, with no account for the signer',
        'Deposit by card or Swiss QR-bill',
        'Contracts signed on paper uploaded',
        'The lead moves on by itself',
      ],
      intro: {
        heading: 'The slowest step in most sales',
        paragraphs: [
          'The client has said yes. Then the contract is written in a word processor, exported to PDF, emailed, printed, signed, scanned and sent back, and someone remembers to ask for the deposit a week later. Every step is a chance for the deal to cool down.',
          'CadenceE turns that into one link. The contract is generated from your template with the lead’s details, the client reads and signs it online, and the deposit is requested as part of signing. The lead moves to the next phase as soon as it is done.',
        ],
      },
      sections: [
        {
          heading: 'Contracts from your templates',
          paragraphs: [
            'Write each contract once as a template, with fields for the client’s name, the membership or the event, the dates and the amounts. CadenceE fills them in from the lead, so every contract is right the first time.',
          ],
        },
        {
          heading: 'Signing without an account',
          paragraphs: [
            'The person signing opens a secure link, reads the contract and signs online. They do not create an account or install anything, which is what clients expect when they sign a membership or an event booking.',
          ],
        },
        {
          heading: 'The deposit, collected at signing',
          paragraphs: [
            'A contract can ask for a deposit at signing. The client pays by card or with a Swiss QR-bill, and the payment is recorded against the contract. The [sales pipeline](page:sales) reaches its last phase, deposit, without anyone chasing.',
          ],
          bullets: [
            'Card payment at signing',
            'Swiss QR-bill for bank transfer',
            'Payment recorded on the contract',
            'Matched with your bank file later',
          ],
        },
        {
          heading: 'Paper still works',
          paragraphs: [
            'Some clients prefer paper, and some contracts are signed in person. Upload the signed copy and CadenceE records it like any other signed contract, so the pipeline and the client’s record stay complete. For clubs, the signed lead then becomes a member: see [CRM for private clubs](page:private-clubs).',
          ],
        },
      ],
      features: {
        heading: 'Contract features',
        items: [
          { icon: 'contract', title: 'Contract templates', text: 'Reusable templates filled from the lead’s details.' },
          { icon: 'signature', title: 'Online e-signature', text: 'A secure link to read and sign.' },
          { icon: 'portal', title: 'No account needed', text: 'The person signing does not create an account.' },
          { icon: 'deposit', title: 'Deposit at signing', text: 'By card or Swiss QR-bill, recorded on the contract.' },
          { icon: 'paper', title: 'Paper contracts', text: 'Upload contracts signed by hand.' },
          { icon: 'leads', title: 'Pipeline updates', text: 'The lead moves to signature and deposit by itself.' },
          { icon: 'members', title: 'Into membership', text: 'For clubs, a signed and paid lead becomes an Active member in one step.' },
          { icon: 'audit', title: 'Audit log', text: 'Every change is recorded with who made it.' },
          { icon: 'outbox', title: 'Sent from your outbox', text: 'Contract emails leave through the CRM’s outbox, with retries.' },
        ],
      },
      steps: {
        heading: 'How a contract gets signed',
        items: [
          { title: 'Prepare', text: 'Pick the template; CadenceE fills in the lead’s details.' },
          { title: 'Send', text: 'The client receives a link by email.' },
          { title: 'Sign and pay', text: 'They read, sign online and pay the deposit by card or QR-bill.' },
          { title: 'Done', text: 'The contract is kept on the record and the lead moves on.' },
        ],
      },
      faq: {
        heading: 'E-signature contracts: frequent questions',
        items: [
          { q: 'Does the person signing need a CadenceE account?', a: 'No. They open the link they received, read the contract and sign online, without creating an account.' },
          { q: 'Can we collect a deposit when the contract is signed?', a: 'Yes. The deposit can be paid at signing by card or with a Swiss QR-bill, and is recorded against the contract.' },
          { q: 'What if a client signs on paper?', a: 'Upload the signed copy. CadenceE records it as signed, so the lead and the client’s record stay complete.' },
          { q: 'Are contracts generated from templates?', a: 'Yes. You write each type of contract once as a template, and CadenceE fills in the client’s details from the lead.' },
          { q: 'What happens after signing?', a: 'The lead moves to the signature and deposit phases. For a club, a signed and paid lead is converted into an Active member in one step, with a welcome email.' },
          { q: 'Is there a record of changes?', a: 'Yes. The audit log records changes, so you can see who did what, and when.' },
        ],
      },
      related: ['sales', 'billing', 'private-clubs'],
      cta: {
        heading: 'Contract signed and deposit paid, in one link',
        text: 'Create a workspace and send your first contract, or ask for a demo.',
      },
    },

    swiss: {
      eyebrow: 'Swiss CRM',
      h1: 'A Swiss CRM that speaks',
      h1Accent: 'French, German and English',
      lead: 'CadenceE was built in Switzerland for Swiss businesses: payments by Swiss QR-bill and TWINT, accounting exports to Bexio and Abacus, the app in French, German and English, and privacy requests handled under the FADP.',
      heroCardTitle: 'Made for Switzerland',
      heroPoints: [
        'Swiss QR-bill and TWINT',
        'Card and SEPA payments too',
        'Accounting export to Bexio and Abacus',
        'French, German and English',
        'Privacy requests under the FADP and the GDPR',
      ],
      intro: {
        heading: 'Why a Swiss business needs a Swiss CRM',
        paragraphs: [
          'International CRMs are built for other markets. They invoice in ways Swiss customers do not pay, they assume one language per team, and they leave the accounting export to a consultant. For a Swiss club or business, those gaps become manual work every month.',
          'CadenceE starts from Swiss habits. Members and clients pay with the QR-bill or TWINT, the bookkeeper receives an export for Bexio or Abacus, the front desk works in French while the finance office works in German, and requests under the Swiss Federal Act on Data Protection are part of the product.',
        ],
      },
      sections: [
        {
          heading: 'Swiss payments: QR-bill and TWINT',
          paragraphs: [
            'Statements can carry a Swiss QR-bill, the payment slip every Swiss banking app reads. Members and clients can also pay with TWINT, by card or by SEPA, and deposits at contract signing work with card and QR-bill. Bank files are matched against what is owed, so you see who has paid without ticking lines by hand. More on [billing and payments](page:billing).',
          ],
        },
        {
          heading: 'Accounting export to Bexio and Abacus',
          paragraphs: [
            'The accounting export goes to Bexio or Abacus, two accounting systems widely used in Switzerland. The CRM’s ledger is only ever reversed, never edited, so what was exported stays true and every correction is visible.',
          ],
        },
        {
          heading: 'French, German and English, for each person',
          paragraphs: [
            'The whole app is available in French, German and English, and each person chooses their own language. A colleague in Lausanne and one in Zurich work on the same lead, each in their own language. Amounts follow the workspace’s regional settings, such as CHF 1’234.50, and prepared emails are available in French and English.',
          ],
        },
        {
          heading: 'Data protection under the FADP and the GDPR',
          paragraphs: [
            'Requests to export or erase a person’s data are handled in the CRM, under the GDPR and the Swiss FADP. Every change is written to an audit log, roles and permissions limit who sees what, and sign-in supports passkeys and two-factor authentication.',
          ],
          bullets: [
            'Export and erasure requests',
            'Audit log of every change',
            'Roles and permissions',
            'Passkeys and two-factor authentication',
          ],
        },
      ],
      features: {
        heading: 'Swiss features',
        items: [
          { icon: 'payments', title: 'Swiss QR-bill', text: 'Statements with a QR-bill every Swiss bank reads.' },
          { icon: 'swiss', title: 'TWINT', text: 'Members and clients pay with TWINT.' },
          { icon: 'bank', title: 'Bank-file matching', text: 'Bank files matched against open amounts.' },
          { icon: 'accounting', title: 'Bexio export', text: 'Accounting export for Bexio.' },
          { icon: 'ledger', title: 'Abacus export', text: 'Accounting export for Abacus, from a ledger that is reversed, never edited.' },
          { icon: 'languages', title: 'Three languages', text: 'French, German and English, chosen by each person.' },
          { icon: 'privacy', title: 'FADP and GDPR requests', text: 'Export and erasure handled in the CRM.' },
          { icon: 'audit', title: 'Audit log', text: 'Every change recorded.' },
          { icon: 'two-factor', title: 'Passkeys and two-factor', text: 'Secure sign-in for the whole team.' },
        ],
      },
      steps: {
        heading: 'Setting up CadenceE for Switzerland',
        items: [
          { title: 'Create the workspace', text: 'Choose the workspace’s regional settings; each colleague picks their own language.' },
          { title: 'Add payment methods', text: 'Set up QR-bill, TWINT, card and SEPA for members and clients.' },
          { title: 'Connect accounting', text: 'Choose the export for Bexio or Abacus.' },
          { title: 'Work in your languages', text: 'Invite the team; everyone works in French, German or English.' },
        ],
      },
      faq: {
        heading: 'Swiss CRM: frequent questions',
        items: [
          { q: 'Does CadenceE support the Swiss QR-bill?', a: 'Yes. Statements can carry a Swiss QR-bill, and deposits at contract signing can be paid with one.' },
          { q: 'Can members pay with TWINT?', a: 'Yes. TWINT is one of the payment methods, alongside card, SEPA and the QR-bill.' },
          { q: 'Which accounting systems does it export to?', a: 'Bexio and Abacus.' },
          { q: 'Is the app available in French and German?', a: 'Yes. The app is available in French, German and English, chosen by each person. Prepared emails exist in French and English.' },
          { q: 'How does CadenceE handle data protection?', a: 'Requests to export or erase data are handled under the GDPR and the Swiss FADP, every change is recorded in an audit log, and access is controlled by roles and permissions.' },
          { q: 'Does it show amounts in Swiss francs?', a: 'Yes. Amounts follow the workspace’s regional settings, for example CHF 1’234.50 in a Swiss workspace.' },
          { q: 'Who built CadenceE?', a: 'CadenceE was built in Switzerland, together with Pavillon 46, a private members’ club in La Croix-sur-Lutry, Vaud, which uses it every day.' },
        ],
      },
      related: ['billing', 'private-clubs', 'features'],
      cta: {
        heading: 'A CRM that fits how Switzerland works',
        text: 'Create a workspace in French, German or English, or ask for a demo.',
      },
    },

    billing: {
      eyebrow: 'Club billing and payments',
      h1: 'Club billing and payments,',
      h1Accent: 'without the month-end scramble',
      lead: 'Recurring dues, house accounts, bar tabs and monthly PDF statements in one CRM, paid by card, TWINT, SEPA or Swiss QR-bill, matched against your bank file and exported to Bexio or Abacus.',
      heroCardTitle: 'Billing in CadenceE',
      heroPoints: [
        'Recurring dues on schedule',
        'Tabs and house accounts',
        'Monthly PDF statements',
        'Card, TWINT, SEPA and QR-bill',
        'Bank-file matching, Bexio and Abacus export',
      ],
      intro: {
        heading: 'Billing a club is more than sending invoices',
        paragraphs: [
          'A member owes the annual dues, a dinner from last Thursday, two drinks at the bar and a ticket for next month’s event. Good billing adds it all up correctly, sends one clear statement and knows when it has been paid.',
          'CadenceE does this from the records the club already uses. Dues, tabs, events and private hire all post to the member’s account. Payments come back by card, TWINT, SEPA or QR-bill, and the bank file is matched so each account balances itself.',
        ],
      },
      sections: [
        {
          heading: 'Recurring dues and charges',
          paragraphs: [
            'Membership dues are billed on their schedule, automatically. Other charges, such as events, private hire and extras, are added to the same account, so members receive one statement instead of a pile of invoices. Dues belong to the [membership](page:membership), so they follow renewals and households.',
          ],
        },
        {
          heading: 'Tabs, POS and house accounts',
          paragraphs: [
            'Bar and restaurant spending goes onto tabs, with a Lightspeed POS connection that brings sales from the till. Members and regular guests can run a house account that is settled every month. See the [hospitality CRM](page:hospitality) for the guest side.',
          ],
        },
        {
          heading: 'Statements and payments',
          paragraphs: [
            'At the end of the month, each account receives a PDF statement. Members pay by card, TWINT, SEPA or with the Swiss QR-bill on the statement. Deposits at contract signing are paid the same way, by card or QR-bill.',
          ],
        },
        {
          heading: 'Bank matching, a ledger you can trust, and accounting',
          paragraphs: [
            'Upload the bank file and CadenceE matches incoming payments against what is owed. The ledger is only ever reversed, never edited: a mistake is corrected by a visible reversal, not by rewriting history. The accounting export goes to Bexio or Abacus, so the bookkeeper starts from clean figures.',
          ],
          bullets: [
            'Bank-file matching',
            'A ledger corrected by reversals only',
            'Export to Bexio',
            'Export to Abacus',
          ],
        },
      ],
      features: {
        heading: 'Billing and payment features',
        items: [
          { icon: 'dues', title: 'Recurring dues', text: 'Dues billed on schedule.' },
          { icon: 'tabs', title: 'Bar and restaurant tabs', text: 'Spending recorded per member or guest.' },
          { icon: 'pos', title: 'Lightspeed POS', text: 'Sales from the till reach the CRM.' },
          { icon: 'house-account', title: 'House accounts', text: 'Settled once a month.' },
          { icon: 'statements', title: 'Monthly PDF statements', text: 'One clear statement per account.' },
          { icon: 'payments', title: 'Card and SEPA', text: 'The payment methods members already use.' },
          { icon: 'swiss', title: 'TWINT and QR-bill', text: 'Swiss payment methods built in.' },
          { icon: 'bank', title: 'Bank-file matching', text: 'Payments matched to what is owed.' },
          { icon: 'ledger', title: 'Reversal-only ledger', text: 'Corrections are reversals, never edits.' },
          { icon: 'accounting', title: 'Bexio and Abacus', text: 'Accounting export for your bookkeeper.' },
          { icon: 'gift-card', title: 'Gift cards', text: 'Sell and redeem gift cards.' },
          { icon: 'donation', title: 'Donations', text: 'Record donations next to the other income.' },
        ],
      },
      steps: {
        heading: 'How billing runs each month',
        items: [
          { title: 'Charges build up', text: 'Dues, tabs, events and extras post to each account during the month.' },
          { title: 'Statements go out', text: 'Each account receives a PDF statement with its QR-bill.' },
          { title: 'Payments come in', text: 'Members pay by card, TWINT, SEPA or QR-bill.' },
          { title: 'The books close', text: 'Bank files are matched and the export goes to Bexio or Abacus.' },
        ],
      },
      faq: {
        heading: 'Club billing: frequent questions',
        items: [
          { q: 'Which payment methods are supported?', a: 'Card, TWINT, SEPA and the Swiss QR-bill.' },
          { q: 'Can bar tabs be billed to members?', a: 'Yes. Tabs post to the member’s house account and appear on the monthly statement. A Lightspeed POS connection brings sales from the till.' },
          { q: 'Can mistakes be corrected?', a: 'Yes, with a reversal. The ledger is never edited, so the history stays complete and every account explains itself.' },
          { q: 'Do you integrate with accounting software?', a: 'CadenceE exports to Bexio and Abacus. See [Swiss CRM](page:swiss).' },
          { q: 'How are bank payments reconciled?', a: 'Upload the bank file and CadenceE matches incoming payments against what is owed.' },
          { q: 'Can deposits be collected when a contract is signed?', a: 'Yes, by card or Swiss QR-bill. See [e-signature contracts](page:e-signature).' },
        ],
      },
      related: ['swiss', 'membership', 'hospitality'],
      cta: {
        heading: 'Make month-end simpler',
        text: 'Create a workspace and set up your dues and payment methods, or ask for a demo.',
      },
    },

    email: {
      eyebrow: 'Email automation CRM',
      h1: 'Email automation inside your CRM,',
      h1Accent: 'with consent built in',
      lead: 'Every email from one outbox, with retries and bounce handling. Sequences and automations that follow up for you, newsletters that invite leads to book a meeting, and campaigns with consent and a one-click unsubscribe.',
      heroCardTitle: 'Email in CadenceE',
      heroPoints: [
        'One outbox, with retries and bounce handling',
        'Sequences and automations',
        'Newsletters with a booking page',
        'Campaigns with consent',
        'One-click unsubscribe in every email',
      ],
      intro: {
        heading: 'Email that knows your customers',
        paragraphs: [
          'When email lives in a separate marketing tool, it never quite knows what the CRM knows: that the lead signed yesterday, that the member asked not to receive newsletters, or that the last message bounced. Messages reach the wrong people at the wrong moment.',
          'In CadenceE, email is part of the CRM. Every message, whether a one-to-one reply, a sequence step or a newsletter, goes through the same outbox and is recorded on the person it was sent to. Consent and unsubscribes are respected everywhere, automatically.',
        ],
      },
      sections: [
        {
          heading: 'One outbox, delivered properly',
          paragraphs: [
            'All email leaves through one outbox. When a receiving server refuses for a while, the message is tried again; when an address bounces, that is recorded so you stop writing to it. You can see what was sent, to whom, and what happened to it.',
          ],
        },
        {
          heading: 'Sequences and automations',
          paragraphs: [
            'Sequences send a series of follow-ups on a schedule. Automations react to what happens in the CRM, such as a new lead from the website or a signed contract, so the right message goes out without anyone having to remember it.',
          ],
        },
        {
          heading: 'Newsletters that book meetings',
          paragraphs: [
            'Newsletters go to members, guests or leads. A newsletter can invite leads to book a meeting through a booking page, so interest turns into a slot in the calendar and the lead moves forward in the [sales pipeline](page:sales).',
          ],
        },
        {
          heading: 'Campaigns with consent',
          paragraphs: [
            'Campaigns only reach people who agreed to receive them, and every message carries a one-click unsubscribe link that takes effect everywhere at once. Prepared emails are available in French and English, and the [AI sales writer](page:ai) helps draft new ones.',
          ],
          bullets: [
            'Consent recorded on each person',
            'One-click unsubscribe',
            'Prepared emails in French and English',
            'AI drafts for new messages',
          ],
        },
      ],
      features: {
        heading: 'Email features',
        items: [
          { icon: 'outbox', title: 'One outbox', text: 'Every email sent from one place and recorded on the person.' },
          { icon: 'audit', title: 'Retries and bounces', text: 'Temporary refusals retried, bounces recorded.' },
          { icon: 'sequences', title: 'Sequences', text: 'Follow-ups on a schedule.' },
          { icon: 'automations', title: 'Automations', text: 'Messages triggered by what happens in the CRM.' },
          { icon: 'newsletter', title: 'Newsletters', text: 'To members, guests and leads.' },
          { icon: 'booking', title: 'Booking page', text: 'Invite leads to book a meeting from a newsletter.' },
          { icon: 'campaigns', title: 'Campaigns', text: 'Sends to the groups you choose.' },
          { icon: 'consent', title: 'Consent and unsubscribe', text: 'Consent respected, one-click unsubscribe in every email.' },
          { icon: 'languages', title: 'French and English emails', text: 'Prepared emails in both languages.' },
          { icon: 'writer', title: 'AI writer', text: 'Drafts in your tone, for you to review.' },
        ],
      },
      steps: {
        heading: 'How email works in CadenceE',
        items: [
          { title: 'Set up sending', text: 'Add your workspace’s sending address and choose your templates.' },
          { title: 'Write the sequences', text: 'Write the follow-ups once and decide when each one goes out.' },
          { title: 'Automate', text: 'Let new leads, signatures or renewals trigger the right message.' },
          { title: 'Send with consent', text: 'Newsletters and campaigns go to people who agreed, and every send is recorded on their record.' },
        ],
      },
      faq: {
        heading: 'Email automation: frequent questions',
        items: [
          { q: 'What happens when an email bounces?', a: 'The bounce is recorded on the person’s record so you stop writing to an address that does not work. Temporary refusals are retried automatically.' },
          { q: 'What is the difference between a sequence and an automation?', a: 'A sequence is a series of follow-ups sent on a schedule. An automation reacts to something that happens in the CRM, such as a new lead or a signed contract, and sends the right message at that moment.' },
          { q: 'How is consent handled?', a: 'Campaigns and newsletters only go to people who agreed to receive them, and every email carries a one-click unsubscribe link that takes effect at once.' },
          { q: 'Can leads book a meeting from a newsletter?', a: 'Yes. A newsletter can invite leads to book a meeting through a booking page.' },
          { q: 'In which languages are the prepared emails?', a: 'Prepared emails are available in French and English. The app itself is available in French, German and English.' },
          { q: 'Can AI help write emails?', a: 'Yes. The AI sales writer drafts emails in your tone for a person to review before sending. See the [AI assistant](page:ai).' },
        ],
      },
      related: ['sales', 'ai', 'membership'],
      cta: {
        heading: 'The right email, to the right person',
        text: 'Create a workspace and add your sending address, or ask for a demo.',
      },
    },

    ai: {
      eyebrow: 'AI CRM assistant',
      h1: 'An AI CRM assistant',
      h1Accent: 'that asks before it acts',
      lead: 'CadenceE’s assistant is built on Claude. It reads your workspace, proposes changes for a person to confirm, drafts sales emails in your tone and prepares a morning brief of what needs attention today.',
      heroCardTitle: 'AI in CadenceE',
      heroPoints: [
        'Built on Claude, by Anthropic',
        'Proposes changes; a person confirms them',
        'AI sales writer for emails and notes',
        'A morning brief every day',
        'Part of the CRM, not a separate tool',
      ],
      intro: {
        heading: 'AI that helps, without taking over',
        paragraphs: [
          'AI in a CRM can save hours, or make a mess. An assistant that edits records on its own may update the wrong lead, overwrite a note or send something nobody approved. For a club that holds members’ personal data, that is not acceptable.',
          'CadenceE’s assistant takes another approach: it does the reading and the drafting, and leaves the decisions to people. Every change it suggests is shown as a proposal, and a person confirms it before anything is saved.',
        ],
      },
      sections: [
        {
          heading: 'An assistant that proposes, never imposes',
          paragraphs: [
            'Ask the assistant to tidy up a lead, prepare the next steps after a meeting or update several records at once. It works out what should change and presents the changes as proposals. Nothing is written until a person confirms, and confirmed changes appear in the audit log like any other.',
          ],
        },
        {
          heading: 'The AI sales writer',
          paragraphs: [
            'The sales writer drafts sales emails, follow-ups, deal messages and notes in your brand’s tone: professional, friendly or persuasive. You choose the goal and the length, adjust the draft with a click and send it from the CRM’s outbox. See [email automation](page:email).',
          ],
        },
        {
          heading: 'A morning brief, every day',
          paragraphs: [
            'Every morning, each person gets a brief of their day in the workspace: the leads, tasks and meetings that need attention. It is built from the records that person is allowed to see, and links straight to each one.',
          ],
        },
        {
          heading: 'Built on Claude',
          paragraphs: [
            'The assistant runs on Claude, the AI model made by Anthropic, and works inside CadenceE on your workspace’s records. It sits next to the rest of the [CRM’s features](page:features) rather than in a separate tool with a copy of your data to keep in sync.',
          ],
        },
      ],
      features: {
        heading: 'AI features',
        items: [
          { icon: 'ai', title: 'AI assistant', text: 'Ask in plain language; get proposed changes to confirm.' },
          { icon: 'audit', title: 'Changes you confirm', text: 'Nothing is saved until a person approves it, and it is logged.' },
          { icon: 'writer', title: 'AI sales writer', text: 'Emails, follow-ups and notes in your tone.' },
          { icon: 'brief', title: 'Morning brief', text: 'The day’s priorities, prepared every morning.' },
          { icon: 'leads', title: 'Suggested next actions', text: 'What to do next on a lead or a deal.' },
          { icon: 'roles', title: 'Your rules', text: 'The brief follows what each person is allowed to see.' },
        ],
      },
      steps: {
        heading: 'How to work with the assistant',
        items: [
          { title: 'Ask', text: 'Describe what you need in your own words.' },
          { title: 'Review', text: 'The assistant shows the changes it proposes.' },
          { title: 'Confirm', text: 'Accept what is right; nothing else is saved.' },
          { title: 'Follow up', text: 'Use the sales writer for the email, and read tomorrow’s morning brief.' },
        ],
      },
      faq: {
        heading: 'AI CRM assistant: frequent questions',
        items: [
          { q: 'Which AI model does CadenceE use?', a: 'The assistant is built on Claude, made by Anthropic.' },
          { q: 'Can the AI change my data without asking?', a: 'No. The assistant only proposes changes. A person confirms each one before it is saved, and confirmed changes are recorded in the audit log.' },
          { q: 'What does the AI sales writer do?', a: 'It drafts sales emails, follow-ups, deal messages and notes in the tone and length you choose, for a person to review and send.' },
          { q: 'What is the morning brief?', a: 'A daily summary of each person’s day in the workspace, built from the records they are allowed to see.' },
          { q: 'Is the AI a separate tool?', a: 'No. The assistant, the sales writer and the brief are part of CadenceE and work on your workspace’s records.' },
          { q: 'Do we have to use the AI?', a: 'No. The CRM works without it; the assistant is there when it saves time. See all [features](page:features).' },
        ],
      },
      related: ['sales', 'email', 'features'],
      cta: {
        heading: 'Let AI do the reading and the drafting',
        text: 'Create a workspace and try the assistant on your own leads, or ask for a demo.',
      },
    },

    pricing: enPricing,
  },
};

export default en;
