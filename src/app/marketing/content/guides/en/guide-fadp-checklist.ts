import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Data protection',
  h1: 'Swiss FADP checklist for clubs, associations and small businesses',
  lead: 'Since 1 September 2023, the revised Federal Act on Data Protection (FADP) has applied to every club, association and small business in Switzerland that handles personal data. This checklist takes you through what to do, point by point, with the article behind each one and where a CRM helps. It is general information, not legal advice.',
  readingMinutes: 9,
  keyTakeaways: [
    'The revised FADP applies to every association, club and business that processes personal data, whatever its size.',
    'Most organisations with fewer than 250 employees need no record of processing activities, but they still need a privacy notice and proper security.',
    'Requests for access must generally be answered within 30 days and at no cost.',
    'A breach likely to cause a high risk must be reported to the FDPIC as quickly as possible.',
    'Promotional emails need prior consent, a correct sender and an easy way to opt out, with a narrow exception for existing customers.',
  ],
  sections: [
    {
      id: 'who-the-fadp-applies-to',
      heading: 'Who the revised FADP applies to',
      paragraphs: [
        'The Federal Act on Data Protection of 25 September 2020 came into force on 1 September 2023, with its Data Protection Ordinance (DPO). It applies to private persons, including associations, foundations and companies of any size, and to federal bodies. It protects natural persons: a member, a guest or a supplier’s contact person, but not a company as such. It also covers processing started abroad that has an effect in Switzerland (Art. 3).',
        'The EU’s GDPR does not apply directly in Switzerland, but it can apply to a Swiss organisation that offers goods or services to people in the EU or monitors their behaviour, for example a club that actively recruits members in neighbouring countries.',
        'Certain wilful breaches (failing to inform people, false or incomplete answers to access requests, careless use of processors, unlawful disclosure abroad, ignoring minimum security) can lead, on complaint, to fines of up to CHF 250’000 for the responsible individuals (Art. 60 and 61).',
      ],
    },
    {
      id: 'map-your-personal-data',
      heading: '1. Map the personal data you hold',
      paragraphs: [
        'Start with a simple table: which data, about whom, for what purpose, where it is stored, who can see it, who receives it and how long you keep it. Typical groups are members and their households, applicants, guests, newsletter subscribers, customers, employees, volunteers, and contact people at suppliers and corporate members.',
      ],
      subsections: [
        {
          heading: 'Look out for sensitive data',
          paragraphs: [
            'Sensitive personal data gets extra protection (Art. 5): health, religious, philosophical or political views, ethnic origin, genetic and biometric data, criminal proceedings and social assistance. An allergy noted for an event dinner or an injury recorded by a sports club is health data. Where you rely on consent to process it, the consent must be explicit (Art. 6 para. 7), and passing it to third parties needs a justification (Art. 30 and 31).',
          ],
        },
      ],
      tip: 'Include the places outside your main system: spreadsheets on laptops, paper forms, inboxes, messaging groups and exports sent to the accountant.',
    },
    {
      id: 'purposes-and-privacy-notice',
      heading: '2. Define purposes and publish a privacy notice',
      paragraphs: [
        'Collect personal data only for a purpose the person can recognise, and use it only in compatible ways (Art. 6). Unlike the GDPR, the FADP does not require a legal basis such as consent for every processing. A justification (consent, an overriding interest or the law) is needed where processing would otherwise breach someone’s personality rights, for instance if they objected (Art. 30 and 31); managing a member’s contract is a typical overriding interest.',
        'When you collect data, you must inform people (Art. 19) in a precise, comprehensible and easily accessible form (DPO Art. 13). Your privacy notice says at least:',
      ],
      bullets: [
        'who you are and how to contact you;',
        'why you process the data, and who receives it;',
        'if data goes abroad, to which countries and with which safeguards;',
        'if you did not collect the data from the person, which categories you hold.',
      ],
    },
    {
      id: 'record-of-processing-activities',
      heading: '3. Check whether you need a record of processing activities',
      paragraphs: [
        'The record (Art. 12) lists, for each activity, the purpose, the categories of people and data, the recipients, the retention period, the security measures and any disclosure abroad.',
        'The exemption is precise (DPO Art. 24): companies and other private organisations with fewer than 250 employees on 1 January of the year are exempt, unless they process sensitive data on a large scale or carry out high-risk profiling. Employees count; members and volunteers do not. A club with 3,000 members and 20 staff is therefore usually exempt.',
      ],
      tip: 'Even when exempt, keep the table from step 1 up to date: it feeds your privacy notice and lets you answer a request or a breach quickly.',
    },
    {
      id: 'data-security',
      heading: '4. Secure the data',
      paragraphs: [
        'Security must be appropriate to the risk (Art. 8). The ordinance asks in particular that people only access the data their task requires, that you can trace who entered or changed what, that data can be restored after an incident and that software is kept up to date (DPO Art. 3). In practice:',
      ],
      bullets: [
        'Personal accounts instead of shared logins, with two-factor authentication.',
        'Roles: bar staff do not need the members’ payment history.',
        'Access removed on the day a volunteer or employee leaves.',
        'Encrypted devices, regular backups, paper forms under lock.',
      ],
    },
    {
      id: 'processors-and-contracts',
      heading: '5. Put contracts in place with your processors',
      paragraphs: [
        'Your CRM, email service, payment provider and cloud storage are processors. You may entrust data to them by contract if they only process it as you yourself could, and you must make sure they can guarantee its security (Art. 9). They may only use sub-processors with your prior approval (Art. 9 para. 3). List your providers, sign a data processing agreement with each and note where they host the data.',
      ],
    },
    {
      id: 'cross-border-disclosure',
      heading: '6. Check disclosures abroad',
      paragraphs: [
        'Data may go to countries the Federal Council lists as adequate in Annex 1 of the DPO: the EU and EEA states, the United Kingdom and, since 15 September 2024, US companies that take part in the Swiss-US Data Privacy Framework. Elsewhere you need safeguards such as standard contractual clauses recognised by the FDPIC (Art. 16), or an exception such as explicit consent or the performance of a contract (Art. 17). Name the countries in your privacy notice.',
      ],
    },
    {
      id: 'data-subject-rights',
      heading: '7. Be ready for access, portability and deletion requests',
      paragraphs: [
        'Anyone may ask whether you process data about them (Art. 25). The answer includes the data itself, the purpose, the retention period, the recipients and the source. It is generally due within 30 days and at no cost; for disproportionate effort you may ask a contribution of up to CHF 300, announced in advance (DPO Art. 18 and 19). Check the person’s identity first, and search every system: CRM, spreadsheets, inboxes, accounting and paper.',
        'People may also ask for the data they gave you in a common electronic format (Art. 28), have incorrect data corrected, and object to processing or ask for deletion, after which you may only continue with a justification (Art. 30 to 32).',
      ],
    },
    {
      id: 'retention-and-deletion',
      heading: '8. Set retention periods and delete on time',
      paragraphs: [
        'Data must be destroyed or anonymised once it is no longer needed for its purpose (Art. 6 para. 4). Set a period for each category, for example:',
      ],
      bullets: [
        'Accounting records such as invoices and payments: ten years (Art. 958f of the Code of Obligations, applied to associations by Art. 69a of the Civil Code).',
        'Applicants not admitted: only as long as the decision requires.',
        'Former members: what accounting requires and, if they agree, what you need to stay in touch.',
        'People who unsubscribed: only what ensures they receive nothing more.',
      ],
    },
    {
      id: 'data-breaches',
      heading: '9. Know what to do in a data breach',
      paragraphs: [
        'A stolen laptop with the member list, a hacked mailbox or a spreadsheet sent to the wrong address is a breach of data security. If it is likely to lead to a high risk for the people concerned, report it to the FDPIC as quickly as possible through its online form, with the nature of the breach, its consequences and the measures taken (Art. 24); do not wait for a lengthy investigation, as missing details can follow. Your processors must tell you about breaches as quickly as possible.',
        'Inform the people concerned when it is necessary for their protection or when the FDPIC asks. Document the breach, its effects and what you did, and keep that record for at least two years (DPO Art. 15).',
      ],
    },
    {
      id: 'marketing-emails-and-consent',
      heading: '10. Send marketing emails with consent',
      paragraphs: [
        'Promotional emails and text messages fall under the Unfair Competition Act (Art. 3 para. 1 let. o UCA): you need prior consent, the correct sender and an easy way to opt out at no cost. The exception: a customer who gave you their details when buying and was told they could refuse may receive advertising for your own similar products or services.',
        'Notices members need as members, such as the general assembly or their statement, are not advertising; promotions and newsletters to non-members are. Record when and how each person consented, and respect an unsubscribe in every list.',
      ],
    },
    {
      id: 'how-a-crm-helps',
      heading: 'How a CRM helps you comply',
      paragraphs: [
        'Most problems in small organisations come from scattered copies: the same member in four spreadsheets, an old export in an inbox, nobody sure who has access. Our guide [from Excel to a CRM](page:guide-excel-to-crm) shows how to bring them together.',
        'In CadenceE, roles and permissions limit who sees what, an audit log records every change, sign-in supports passkeys and two-factor authentication, consent is recorded on each person with a one-click unsubscribe, and privacy requests export or erase what is held about a person under the FADP and the GDPR. See the [Swiss CRM](page:swiss) and [membership management](page:membership) pages, or [try the demo](/login?demo=1). For keeping in touch with former members, read our guide to [member retention](page:guide-member-retention).',
      ],
    },
  ],
  faq: {
    heading: 'Swiss FADP for clubs and associations: frequent questions',
    items: [
      {
        q: 'Does the revised FADP apply to small associations and clubs?',
        a: 'Yes. It applies to every private person or organisation that processes personal data, whatever its size or legal form. Size only matters for a few duties, such as the record of processing activities.',
      },
      {
        q: 'Does our club need a record of processing activities?',
        a: 'Usually not, if it employs fewer than 250 people on 1 January and neither processes sensitive data on a large scale nor carries out high-risk profiling (DPO Art. 24). Members and volunteers are not employees.',
      },
      {
        q: 'Do we need members’ consent to store their data?',
        a: 'Not to run the membership, provided your privacy notice tells them what you do. Consent matters for promotional emails, and must be explicit where you rely on it to process sensitive data such as health information.',
      },
      {
        q: 'When must a data breach be reported to the FDPIC?',
        a: 'As quickly as possible, when it is likely to lead to a high risk for the people concerned. Breaches below that threshold need not be reported, but a short note of what happened and why you did not report it is good practice.',
      },
      {
        q: 'Do we need a data protection officer?',
        a: 'No. Private organisations may appoint one but are not required to (Art. 10). A named person who handles requests and keeps this checklist up to date is still a good idea.',
      },
    ],
  },
  cta: {
    heading: 'Keep member data in order',
    text: 'CadenceE keeps member and customer data in one place, with roles and permissions, an audit log, recorded consent with one-click unsubscribe, and privacy requests that export or erase a person’s data.',
  },
};

export default guide;
