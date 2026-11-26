import type { GuideContent } from '../types';

const guide: GuideContent = {
  eyebrow: 'Membership admissions',
  h1: 'The membership application process, step by step',
  lead: 'How to admit new members fairly and quickly: what a club membership application form should ask, how sponsors and committee review work, how to say yes, no or not yet, how to run a waitlist, and how to turn a signed agreement into a well-onboarded new member, with records that respect the Swiss FADP.',
  readingMinutes: 9,
  keyTakeaways: [
    'Write the process and the admission criteria down before the next application arrives.',
    'Ask only what you need to decide; leave sensitive data out of the form.',
    'Answer every applicant quickly, including those you decline or defer.',
    'Collect the signature and the joining fee in the same step.',
    'Onboarding starts the day of acceptance, not the first visit.',
  ],
  sections: [
    {
      id: 'why-it-matters',
      heading: 'Why the admission process matters',
      paragraphs: [
        'For an applicant, the membership application process is the first real experience of your club. A slow, opaque process tells them the club is disorganised; a quick, personal one tells them they will be looked after. The best candidates often have other options, and they will not wait three months for an answer.',
        'A written process also protects the club. When every application follows the same steps and the same criteria, decisions are consistent, the committee can explain them, and nobody gets in, or stays out, because they knew the right person at the right time.',
      ],
    },
    {
      id: 'the-process-at-a-glance',
      heading: 'The process at a glance',
      paragraphs: ['Most clubs, whatever their size, follow the same sequence:'],
      steps: [
        { title: 'Enquiry', text: 'Someone asks about membership, through your website, a member or a visit. Reply within one working day.' },
        { title: 'Meeting', text: 'A tour or a conversation with the membership manager, so both sides can see whether it fits.' },
        { title: 'Application', text: 'The applicant completes a short form, ideally online.' },
        { title: 'Sponsors', text: 'If your rules require it, one or two members support the application.' },
        { title: 'Review', text: 'The committee or the membership manager assesses the application against written criteria.' },
        { title: 'Decision', text: 'The applicant hears yes, no or not yet, quickly and courteously.' },
        { title: 'Agreement and joining fee', text: 'The new member signs the agreement and pays the joining fee or deposit.' },
        { title: 'Welcome', text: 'Onboarding begins: portal access, introductions and a first event.' },
      ],
      tip: 'Set a target for the whole journey, for example a decision within four weeks of a complete application, and measure it.',
    },
    {
      id: 'application-form',
      heading: 'The application form: what to ask, and what not to',
      paragraphs: [
        'A good club membership application form fits on one screen or one page. Every field should have a purpose you could explain to the applicant.',
      ],
      subsections: [
        {
          heading: 'What to ask',
          paragraphs: ['These fields cover most clubs:'],
          bullets: [
            'Name, email, phone and postal address.',
            'Preferred language for correspondence.',
            'Membership category, and household members if it covers a family.',
            'Profession or company, if it relates to your club’s purpose.',
            'Who introduced them, and the names of their sponsors.',
            'A few lines on why they want to join and what interests them.',
            'Confirmation that they have read your privacy notice.',
          ],
        },
        {
          heading: 'What to leave out',
          paragraphs: [
            'The Swiss Federal Act on Data Protection (FADP) asks you to collect only what is necessary and treats some data as sensitive: religious, ideological, political or trade union views, health, ethnic origin, genetic and biometric data, criminal or administrative proceedings and sanctions, and social assistance measures. An admission decision rarely needs any of it, so keep it out of the form. The same goes for bank details and copies of identity documents, which can wait until they are actually needed, and for income figures.',
          ],
        },
      ],
    },
    {
      id: 'sponsors-and-references',
      heading: 'Sponsors and references',
      paragraphs: [
        'Many private clubs ask for a proposer and a seconder: existing members who know the applicant and vouch for them. Sponsorship works best when it is light and structured. Instead of an open letter, send each sponsor three short questions: how long they have known the applicant, in what context, and why they think the applicant will enjoy and contribute to the club.',
        'Record who sponsored whom on the applicant’s record. It helps the committee, and later shows which members bring in others who stay. Keep sponsor statements confidential within the committee, and remember that applicants can ask for access to the data held about them; ask an advisor how that applies to references.',
      ],
    },
    {
      id: 'committee-review',
      heading: 'Committee review and decision',
      paragraphs: [
        'Who admits members is set by your rules. In an association under the Swiss Civil Code, the statutes say which body decides; in a club run by a company, the membership terms do. Whichever applies, make the procedure explicit.',
      ],
      bullets: [
        'Write the admission criteria down and share them with everyone who reviews.',
        'Meet at a known rhythm, for example monthly, so applicants know when to expect an answer.',
        'Ask members with a conflict of interest, such as a sponsor, to step out of the decision.',
        'Record each decision, the date and who took it.',
        'Review only complete files: chase missing information before the meeting, not during it.',
      ],
    },
    {
      id: 'communicating-the-decision',
      heading: 'Saying yes, no or not yet',
      paragraphs: [
        'Send the decision within a few days of the meeting. An acceptance should include everything needed for the next step: the agreement to sign, the joining fee and the date the membership starts. Call the applicant if you can; it is a moment people remember.',
        'A refusal deserves the same courtesy. Keep it short and respectful, thank the applicant, and let their sponsors know before they hear it elsewhere. Whether you give reasons is a matter for your rules; whatever you decide, do it consistently. Where the issue is timing or capacity rather than fit, offer a deferral or a place on the waitlist instead.',
      ],
    },
    {
      id: 'waitlists',
      heading: 'Running a waitlist fairly',
      paragraphs: [
        'When a category is full, a waitlist keeps good applicants interested. It stays fair if its rules are written and applied:',
      ],
      bullets: [
        'Decide the order: usually the date of approval, sometimes by category.',
        'Tell applicants roughly where they stand and how often places open up.',
        'Contact everyone on the list at least once or twice a year.',
        'If you take a deposit to hold a place, state in writing when it is refunded.',
      ],
    },
    {
      id: 'agreement-and-joining-fee',
      heading: 'Agreement, joining fee and e-signature',
      paragraphs: [
        'The membership agreement is where expectations become commitments. It should state the category, the annual dues and how they are billed, the term, how renewal and notice work, the house rules or statutes, and the joining fee, including whether any part of it is refundable.',
        'Under Swiss law, most contracts need no particular form, so an agreement signed online is generally valid. Where the law does require written form, only a qualified electronic signature replaces a handwritten one. A membership agreement usually has no such requirement, but check your own case with an advisor.',
        'The joining fee is most easily collected at signing, by card or with a Swiss QR-bill, so the new member does not receive a separate invoice a week later. CadenceE does exactly this with [contracts signed online](page:e-signature): the applicant signs without an account and pays the deposit in the same step. More on payment slips in our [Swiss QR-bill guide](page:guide-qr-bill).',
      ],
    },
    {
      id: 'welcome-and-onboarding',
      heading: 'Welcome and new member onboarding',
      paragraphs: [
        'New member onboarding starts the day the agreement is signed. Send a welcome message with the practical details, open access to the member portal, introduce the new member to the staff and to a few members with shared interests, and invite them to an event in their first month. Check in after about two months.',
        'A member who feels at home in the first year has every reason to renew. Our guides on [how to run a private members’ club](page:guide-run-a-club) and on [renewals and retention](page:guide-member-retention) cover what comes next.',
      ],
    },
    {
      id: 'records-and-privacy',
      heading: 'Record keeping and privacy',
      paragraphs: [
        'Applications contain personal data, and the FADP applies from the moment you collect them. Tell applicants in your privacy notice what you collect, why and for how long. Restrict access to applications to the people who review them, and set a short retention period for unsuccessful applications, after which they are deleted.',
        'Accepted members’ records become part of the membership file. Invoices and payment records for joining fees and dues are accounting records, generally kept for ten years in Switzerland. Our [FADP checklist for clubs](page:guide-fadp-checklist) covers the rest; for your case, ask a data protection advisor.',
      ],
    },
    {
      id: 'tools',
      heading: 'Tools that keep applications moving',
      paragraphs: [
        'Applications handled by email and spreadsheet tend to stall: a sponsor’s reply sits in someone’s inbox, and nobody knows who should call the applicant. A system built for the job keeps every application in one list with its status and history.',
        'In CadenceE, an application form on your website feeds one review list, with duplicate protection. Each record shows who referred the applicant, the contract goes out for e-signature with the deposit at signing, and one step turns the applicant into an Active member with a welcome email and portal access. See [membership management](page:membership) and the [CRM for private clubs](page:private-clubs), or [try the demo](/login?demo=1).',
      ],
    },
  ],
  faq: {
    heading: 'Membership applications: frequent questions',
    items: [
      {
        q: 'How long should a membership application process take?',
        a: 'Aim for a decision within a few weeks of a complete application. Reply to enquiries within one working day, meet at a known rhythm and tell applicants when to expect an answer.',
      },
      {
        q: 'What should a club membership application form include?',
        a: 'Contact details, preferred language, membership category, household members if relevant, sponsors, a few lines on motivation and confirmation of your privacy notice. Leave out sensitive data such as health, religion or political views.',
      },
      {
        q: 'Do applicants need sponsors?',
        a: 'Only if your rules say so. Many private clubs ask for one or two members to support each application; a few short, structured questions for each sponsor work better than an open letter.',
      },
      {
        q: 'Do we have to give reasons when we decline an applicant?',
        a: 'That depends on your statutes or membership terms. Many clubs keep refusals short. Whatever your rule, apply it consistently, stay courteous and check the legal position with an advisor.',
      },
      {
        q: 'Is an electronic signature valid for a membership agreement in Switzerland?',
        a: 'Generally yes. Most Swiss contracts need no particular form, so an online signature is enough. Where the law requires written form, only a qualified electronic signature replaces a handwritten one; check your case with an advisor.',
      },
      {
        q: 'How long can we keep data on unsuccessful applicants?',
        a: 'The FADP sets no fixed period: keep it only as long as you need it. Set a short retention period in your rules, mention it in your privacy notice and delete the data when it ends.',
      },
    ],
  },
  cta: {
    heading: 'From application to member in one record',
    text: 'CadenceE collects applications online, sends the agreement for e-signature with the joining deposit and turns the applicant into an Active member in one step.',
  },
};

export default guide;
