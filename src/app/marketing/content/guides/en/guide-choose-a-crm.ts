import type { GuideContent } from '../types';

// How to choose a CRM for a club, venue or small sales team in Switzerland: processes first, a
// scored checklist (data model, email, Swiss payments, FADP, lock-in, AI, cost), trials, vendor
// questions, red flags and a timeline. Vendor-neutral; CadenceE appears as one option.
const guide: GuideContent = {
  eyebrow: 'Choosing software',
  h1: 'How to choose a CRM for a club or small business in Switzerland',
  lead: 'The right CRM fits how you work, your data, Swiss payments and languages, data protection and your budget over several years. Here is a method, a scored checklist, the questions to ask vendors, the red flags and a realistic timeline, for clubs, venues and small sales teams.',
  readingMinutes: 9,
  keyTakeaways: [
    'Start from how you work, from enquiry to payment and renewal, not from a feature list.',
    'Keep the must-haves short and score every product against the same weighted checklist.',
    'In Switzerland, check QR-bill and TWINT, Bexio or Abacus exports, French and German, and FADP-compliant data handling.',
    'Test with your own scenarios and a sample of your own data, including the export, before you sign.',
    'Compare the total cost over three years, and allow six to ten weeks to decide.',
  ],
  sections: [
    {
      id: 'start-from-your-processes',
      heading: 'Start from your processes, not from features',
      paragraphs: [
        'CRM projects disappoint when the software is chosen first and the needs discovered afterwards. Every demo looks convincing because it shows the vendor’s idea of how you work, so write down how you actually work before you look at any product.',
      ],
      steps: [
        { title: 'Map the customer journey', text: 'A club: enquiry, visit, application, contract, deposit, membership, renewal. A venue: enquiry, proposal, contract, event, invoice. A sales team: lead, meeting, proposal, signature, payment.' },
        { title: 'List the people and roles', text: 'Who enters data, who follows up, who approves, who bills, who only reads.' },
        { title: 'Collect the pain points', text: 'Lost enquiries, duplicates, late invoices, reports rebuilt by hand every month.' },
        { title: 'Write five to ten scenarios', text: 'Short stories such as “a member refers a friend, who visits, applies and pays a deposit”. They become your test script.' },
      ],
      tip: 'Involve the people who will use the CRM every day, not only management: if the front desk or the sales team do not adopt it, the best choice on paper fails.',
    },
    {
      id: 'must-haves-and-nice-to-haves',
      heading: 'Must-haves versus nice-to-haves',
      paragraphs: [
        'Sort every requirement into two lists. A must-have is something without which you cannot work, or would break a legal or contractual obligation; a nice-to-have saves time, but you could live without it. Keep the must-haves to about ten: thirty of them eliminate every product. Typical must-haves:',
      ],
      bullets: [
        'Private club: members and households, applications, renewals, recurring dues and statements.',
        'Hospitality venue: event enquiries, contracts and deposits, reservations, tabs and a link to the till.',
        'Small sales team: a clear pipeline, tasks and reminders, email from the CRM, simple reports.',
        'Everyone in Switzerland: your team’s languages, Swiss payments, an accounting export and sound data protection.',
      ],
    },
    {
      id: 'crm-selection-checklist',
      heading: 'The CRM selection checklist, with scores',
      paragraphs: [
        'Weight each criterion from 1 (nice to have) to 3 (must-have), score each shortlisted product from 0 (missing) to 3 (fully meets the need) after seeing it work, and multiply. A must-have scored 0 eliminates a product, whatever its total.',
      ],
      subsections: [
        {
          heading: 'Data model: leads, members, events',
          paragraphs: [
            'Sales CRMs think in leads, companies and deals. A club also needs members, households, corporate memberships, applications and renewals; a venue needs events, reservations and waitlists. Members squeezed into “deals” work in a demo and hurt every day after.',
          ],
        },
        {
          heading: 'Email and follow-up',
          paragraphs: [
            'One-to-one emails logged on the record, follow-up sequences, newsletters, bounce handling, and recorded consent with an unsubscribe link in marketing emails, as Swiss law requires. Our guide to [sales follow-up emails](page:guide-sales-follow-up) shows what a good process needs.',
          ],
        },
        {
          heading: 'Payments and Swiss specifics',
          paragraphs: [
            'The Swiss QR-bill (which replaced the orange and red payment slips in 2022), TWINT, card and SEPA, amounts in Swiss francs, bank-file matching, and an export your bookkeeper can use in Bexio or Abacus. Check the interface languages person by person, Italian included if you need it. More in [QR-bills for clubs](page:guide-qr-bill).',
          ],
        },
        {
          heading: 'Data protection, hosting and access',
          paragraphs: [
            'Under the revised FADP, in force since September 2023, you remain responsible for the data your CRM holds. Swiss hosting is not required, but as a rule data may only go to countries with adequate protection, such as EU and EEA states, or with safeguards like standard contractual clauses. Ask where data is stored, who the subprocessors are and whether you get a data processing agreement, and check roles, an audit log and two-factor sign-in. See our [FADP checklist](page:guide-fadp-checklist).',
          ],
        },
        {
          heading: 'Import, export and lock-in',
          paragraphs: [
            'Moving in should be safe: a preview or dry run, duplicate detection and undo. Moving out should be easy: all your records in an open format such as CSV, at any time, at no extra charge. A CRM you cannot leave can raise its prices.',
          ],
        },
        {
          heading: 'Integrations',
          paragraphs: [
            'List what must connect: website forms, your email domain and calendar, the till or booking system, accounting, e-signature. Prefer what works out of the box over the promise of an API; if an integration needs a consultant, count it in the cost.',
          ],
        },
        {
          heading: 'AI',
          paragraphs: [
            'Ask what the AI does (drafting, summarising, suggesting next steps), whether it can change records or send emails without a person confirming, which provider processes your data, and whether it can be switched off.',
          ],
        },
        {
          heading: 'Total cost and pricing models',
          paragraphs: [
            'CRMs are priced per user, per contact, by feature tier or per workspace, often with add-ons for email volume, AI or extra modules. Add setup, migration, training, integrations, your own team’s time and price rises after the first year, and compare three-year totals at the size you expect to reach.',
          ],
        },
        {
          heading: 'Support and the vendor',
          paragraphs: [
            'Who answers when something breaks, in which language, and how fast? Is it in the contract? Ask how often the product changes, and what happens to your data if the vendor is sold or closes.',
          ],
        },
      ],
    },
    {
      id: 'run-a-trial',
      heading: 'How to run a CRM trial with real scenarios',
      paragraphs: [
        'A vendor’s demo shows the product at its best; your trial should show it doing your work. Shortlist two or three products and run the same script on each.',
      ],
      steps: [
        { title: 'Prepare a test script', text: 'Turn your scenarios into steps with expected results: “import 50 members”, “send a contract and record the deposit”, “find every renewal due next month”.' },
        { title: 'Use a sample of your own data', text: 'A few dozen records in your real format, with personal details kept to a minimum.' },
        { title: 'Let the users drive', text: 'The people who will use the CRM daily run the scenarios themselves.' },
        { title: 'Test the way out', text: 'Export what you imported and check that nothing is missing.' },
        { title: 'Score the same day', text: 'Fill in the checklist while impressions are fresh, and note every workaround.' },
      ],
      tip: 'Keep the trial to two or three weeks. If CadenceE is on your list, you can [try the demo](/login?demo=1), a workspace with sample data that needs no account, before importing anything of your own.',
    },
    {
      id: 'questions-to-ask-vendors',
      heading: 'Questions to ask CRM vendors',
      paragraphs: ['Ask in writing, so that you can compare the answers side by side:'],
      bullets: [
        'Where is our data stored, and which subprocessors can access it?',
        'Do you provide a data processing agreement under the FADP and, if relevant, the GDPR?',
        'How do we export all our data, in which format, and at what cost?',
        'What does the price include, what costs extra, and what is the notice period?',
        'Which of our must-haves work today, and which are only planned?',
        'In which languages are the interface, the emails and the support?',
        'Can the AI change data or send messages without a person confirming?',
        'Who helps us migrate, and can a failed import be undone?',
      ],
    },
    {
      id: 'red-flags',
      heading: 'Red flags when choosing a CRM',
      paragraphs: ['None of these is always a deal-breaker, but each deserves a written answer before you sign:'],
      bullets: [
        'Must-haves answered with “it’s on the roadmap” or “you can build that with the API”.',
        'No way to export all your data yourself.',
        'A discount that is only valid if you sign this week.',
        'Vague answers about where data is stored or who processes it.',
        'A long minimum term with automatic renewal.',
        'AI that changes records or sends emails without anyone confirming.',
      ],
    },
    {
      id: 'decision-timeline',
      heading: 'A realistic decision timeline',
      paragraphs: [
        'For a club, a venue or a small sales team, six to ten weeks from first step to signature is realistic. Name one person who owns the decision.',
      ],
      steps: [
        { title: 'Weeks 1 to 2: processes and requirements', text: 'Map the journey, write the scenarios, agree on must-haves and a budget range.' },
        { title: 'Week 3: from longlist to shortlist', text: 'Check five or six products against the must-haves on paper; keep two or three.' },
        { title: 'Weeks 4 to 6: demos and trials', text: 'Run the test script, collect written answers, score.' },
        { title: 'Week 7: references and contract', text: 'Talk to customers, read the contract and the data processing agreement, check the exit terms.' },
        { title: 'Week 8: decide and plan the move', text: 'Choose, set a go-live date, plan migration and training.' },
      ],
      tip: 'Then allow two to six weeks for the migration, depending on how clean your data is. Our guide [from Excel to a CRM](page:guide-excel-to-crm) shows how to prepare it.',
    },
    {
      id: 'where-cadencee-fits',
      heading: 'Where CadenceE fits',
      paragraphs: [
        'CadenceE is one option for private clubs, hospitality venues and sales teams in Switzerland. It was built in Switzerland with a private members’ club and covers the path from enquiry to payment in one record: a [five-phase lead tracker](page:sales) from outreach to deposit, contracts signed online, members and households, events and tabs, and [Swiss payments and exports](page:swiss) with QR-bill, TWINT, Bexio and Abacus.',
        'The app is in French, German and English, chosen by each person; export and erasure requests are handled under the FADP and the GDPR; lists export to CSV; and the AI assistant proposes changes that a person confirms. It is not a hotel property management system. Put it through the same checklist as every other option, starting with the full list of [features](page:features).',
      ],
    },
  ],
  faq: {
    heading: 'Choosing a CRM: frequent questions',
    items: [
      { q: 'How do I choose the right CRM for a small business?', a: 'Map how you work from first contact to payment, agree on a short list of must-haves, score two or three products with the same checklist, and test each with your own scenarios and data.' },
      { q: 'What are the most important CRM selection criteria?', a: 'Fit with your data, ease of use, email and follow-up, payments and accounting export, data protection, export and lock-in, integrations, support and the total cost over several years.' },
      { q: 'Does a Swiss company need a CRM hosted in Switzerland?', a: 'Not by law. The FADP allows data to be stored in countries with adequate protection, such as EU and EEA states, or with safeguards such as standard contractual clauses. Some contracts or sectors require Swiss hosting, so check yours.' },
      { q: 'How long does it take to choose a CRM?', a: 'Six to ten weeks is realistic for a small organisation, plus two to six weeks to migrate the data and train the team.' },
      { q: 'How should we compare CRM prices?', a: 'Compare three-year totals for the users and contacts you expect, including setup, migration, training, add-ons and price rises, rather than the entry price per user.' },
      { q: 'Should a club use a CRM or membership software?', a: 'A club that sells memberships actively needs both a sales pipeline and member management; one system covering both lets a prospect become a member without retyping. See [CRM for private clubs](page:private-clubs).' },
    ],
  },
  cta: {
    heading: 'Put CadenceE through your checklist',
    text: 'Run your own scenarios in CadenceE, from a first enquiry to a signed contract, a paid deposit and a member’s monthly statement, and score it like any other CRM on your shortlist.',
  },
};

export default guide;
