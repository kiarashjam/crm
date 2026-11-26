import type { GuideContent } from '../types';

// Technical facts checked against SIX (Swiss Implementation Guidelines for the QR-bill, v2.3,
// and six-group.com/qr-bill), PostFinance (factsheet 460.445, "Changes to QR-bills") and the
// SME portal of the Swiss Confederation (kmu.admin.ch) in November 2026.
const guide: GuideContent = {
  eyebrow: 'Club billing',
  h1: 'Swiss QR-bill for clubs and associations: the complete guide',
  lead: 'How the Swiss QR-bill works, which account and reference to choose, what has to be on it, and how a club or association sends its dues as a PDF and sees who has paid without ticking off bank lines by hand.',
  readingMinutes: 9,
  keyTakeaways: [
    'The QR-bill is the only Swiss payment slip: the orange and red slips have not been processed since 30 September 2022.',
    'A QR-IBAN goes with a 27-digit QR reference; an ordinary IBAN goes with a Creditor Reference (RF…) or with no reference.',
    'A structured reference is what lets each payment be matched to the right member automatically.',
    'Since 22 November 2025, every address on a QR-bill must be structured: street, number, postcode and town in separate fields.',
    'Sent as a PDF, the bill is paid by scanning it in a banking app; the bank’s camt file then tells you who has paid.',
  ],
  sections: [
    {
      id: 'what-is-the-qr-bill',
      heading: 'What the QR-bill is, and what it replaced',
      paragraphs: [
        'The Swiss QR-bill is the standard payment slip for invoices in Switzerland and Liechtenstein. Introduced on 30 June 2020, it replaced the red inpayment slip (IS) and the orange slip with reference number (ISR). After 30 September 2022, banks and PostFinance stopped processing the old slips, online and at the counter, and standing orders based on them stopped too.',
        'What changed for members is the Swiss QR Code, a square with a small Swiss cross that holds all the payment data. A banking app reads it in a second, so nobody types a long reference number any more. The rules are set by SIX in its implementation guidelines; the latest revision, in force since 22 November 2025, made structured addresses compulsory.',
      ],
    },
    {
      id: 'payment-part-and-receipt',
      heading: 'The payment part and the receipt',
      paragraphs: [
        'The bottom of a QR-bill has two parts. The payment part, on the right, is A6 landscape (148 × 105 mm) and carries the QR code. The receipt, on the left, is 62 × 105 mm and is kept by whoever pays at a post office counter. On paper, both must be perforated, the payment part may carry no advertising and its back stays blank. Headings can be in German, French, Italian or English.',
      ],
      bullets: [
        'Account / Payable to: the IBAN or QR-IBAN, with the club’s name and address',
        'Reference: the QR reference or Creditor Reference, if there is one',
        'Additional information: a short message, such as “Membership 2027”',
        'Payable by: the member’s name and address, or an empty box',
        'Currency and Amount: CHF or EUR, and the amount or an empty box',
      ],
    },
    {
      id: 'qr-iban-or-iban',
      heading: 'QR-IBAN with QR reference, or IBAN with Creditor Reference',
      paragraphs: [
        'This is the one real decision a club makes. The account and the reference type belong together, and mixing them up is the most common reason a QR-bill is rejected.',
      ],
      subsections: [
        {
          heading: 'QR-IBAN and QR reference (QRR)',
          paragraphs: [
            'A QR-IBAN is a separate number your bank issues for an existing account. Its bank code (the QR-IID) lies between 30000 and 31999, so its fifth character is always a 3. It only receives payments, and only with a QR reference: 27 digits, the last one a check digit. The reference usually encodes the member number and the invoice number.',
          ],
        },
        {
          heading: 'IBAN and Creditor Reference (SCOR)',
          paragraphs: [
            'With the club’s ordinary IBAN, you use the international Creditor Reference (ISO 11649): RF, two check digits, 5 to 25 characters in all, letters allowed. It does the same job as the QR reference and is understood abroad too.',
          ],
        },
        {
          heading: 'IBAN without a reference (NON)',
          paragraphs: [
            'A QR-bill with only the IBAN and a message such as “Membership 2027, Rossi” is valid, but the message is the only clue to who paid, so every payment is matched by hand. Fine for 30 members, painful for 300.',
          ],
        },
      ],
      tip: 'Ask your bank whether it can issue a QR-IBAN for the club’s account and whether it charges for it or for camt files. If that is not worth it, an ordinary IBAN with Creditor References works just as well.',
    },
    {
      id: 'what-goes-on-a-qr-bill',
      heading: 'What goes on a QR-bill: addresses, amounts and currencies',
      paragraphs: [
        'The account must be a Swiss or Liechtenstein IBAN, starting with CH or LI, and the club’s name and address are compulsory. The member’s address and even the amount are optional: left out, they become empty boxes to fill in by hand, which suits donations and supporter contributions.',
        'Addresses must now be structured, with street, house number, postcode, town and country in separate fields. Old combined address lines were tolerated during a transition period that ended in November 2026, so a member list that keeps the whole address in one cell needs splitting first. The wider character set now accepts letters such as ă, ł or ș, so names no longer need simplifying.',
        'Only Swiss francs and euros are allowed, for amounts from 0.01 up to just under one billion, printed with two decimals (CHF 1 250.00). The message and the coded billing information share 140 characters. When a household pays several memberships at once, one bill per household with one reference is easier to match than several.',
      ],
    },
    {
      id: 'sending-dues-as-pdf',
      heading: 'Sending dues and statements as a PDF',
      paragraphs: [
        'Most clubs now email their QR-bills. A PDF is meant to be paid in e-banking or mobile banking; its payment part and receipt are outlined with a scissors symbol, so a member who prefers the counter can print and cut them out.',
      ],
      steps: [
        { title: 'Clean the member list', text: 'Split addresses into their fields and check the emails. Moving a list out of a spreadsheet is covered in [from Excel to a CRM](page:guide-excel-to-crm).' },
        { title: 'Give each bill its reference', text: 'One reference per member and invoice, generated with its check digit, never typed by hand.' },
        { title: 'Generate the PDF', text: 'The invoice on top, the payment part at the bottom, printed at 100% and never scaled to fit.' },
        { title: 'Send and record', text: 'Email each member their own PDF and record what each owes, so you know what the bank file should contain.' },
        { title: 'Remind', text: 'After the due date, send a polite reminder with the same reference, so a late payment still matches.' },
      ],
    },
    {
      id: 'how-members-pay',
      heading: 'How members pay a QR-bill',
      bullets: [
        'Mobile banking: the member scans the QR code with the banking app and confirms',
        'E-banking: the member uploads the PDF or reads the code with a scanner or camera, depending on the bank',
        'Post office counter: the member brings the printed payment part, and the counter fees are charged to the club',
        'By post: the payment part goes to the member’s bank with a payment order',
        'TWINT: since November 2026, the TWINT app pays QR-bills from issuers that take part, marked with the TWINT logo',
      ],
      paragraphs: ['Members choose their own way to pay, and a well-made QR-bill works for all of them.'],
    },
    {
      id: 'reconciliation-with-camt',
      heading: 'Matching payments with camt bank files',
      paragraphs: [
        'Banks report incoming payments in the ISO 20022 camt formats: camt.054, a credit notification, and camt.053, the account statement. You download them from e-banking as XML, or the bank delivers them. Each credit carries the amount, the date, the payer and the reference, if there was one.',
        'Software reads the reference and marks the matching invoice as paid. What remains are a few exceptions (a transfer without a reference, a partial or double payment), the only lines to check by hand. In CadenceE, you upload the bank file and incoming payments are matched against what each member owes: see [club billing and payments](page:billing).',
      ],
    },
    {
      id: 'alternatives',
      heading: 'Alternatives: TWINT, card, direct debit and eBill',
      bullets: [
        'TWINT: instant and familiar, good for event tickets, bar tabs and small amounts',
        'Card: convenient for members abroad and for deposits paid online',
        'Direct debit: SIX ends LSV+ and BDD on 30 September 2028 and has taken no new issuers since 1 January 2026; its successor is eBill Direct Debit, and PostFinance’s CH-DD is not affected',
        'eBill: the bill appears in the member’s e-banking to approve, which suits recurring dues; the club registers through its bank or a service provider',
      ],
      paragraphs: [
        'The QR-bill remains the best fit for annual dues and monthly statements, because every member can pay it.',
      ],
    },
    {
      id: 'common-mistakes',
      heading: 'Common QR-bill mistakes',
      bullets: [
        'A QR reference with an ordinary IBAN, or a Creditor Reference with a QR-IBAN: the bill is rejected',
        'Combined address lines from an old template, still in use after November 2025',
        'The same reference for every member, which makes it useless for matching',
        'An amount changed in the PDF text but not in the QR code',
        'Printing with “fit to page”, which shrinks the payment part below its required size',
        'The treasurer’s private account instead of the club’s',
      ],
      paragraphs: [
        'Most of these mistakes disappear when bills are generated from one clean member record rather than copied from last year’s file.',
      ],
    },
    {
      id: 'automate-with-a-crm',
      heading: 'How a CRM automates the QR-bill',
      paragraphs: [
        'By hand, it means exporting the list, generating bills elsewhere, emailing them one by one and ticking off the statement: plenty of chances to miss a payment.',
        'In CadenceE, the member record holds the address, the household and the dues. Recurring dues are billed on schedule, each member’s PDF statement can carry a Swiss QR-bill, the bank file is matched against what is owed, and the accounting export goes to Bexio or Abacus. Members can also pay by card, TWINT or SEPA; see [Swiss CRM](page:swiss) for the other Swiss specifics.',
        'Billing is one part of [membership management](page:membership). Reminders and renewals are covered in [membership renewals and retention](page:guide-member-retention), and keeping a member list lawful in the [FADP checklist for clubs](page:guide-fadp-checklist). To see a statement with its QR-bill, [try the demo](/login?demo=1).',
      ],
    },
  ],
  faq: {
    heading: 'Swiss QR-bill: frequent questions',
    items: [
      { q: 'Can we still use the orange or red payment slips?', a: 'No. Banks and PostFinance stopped processing them after 30 September 2022, and standing orders based on them stopped too. The QR-bill is the only Swiss payment slip.' },
      { q: 'Does an association need a QR-IBAN?', a: 'No. A QR-IBAN is only needed for QR references. With the club’s ordinary IBAN, you can use a Creditor Reference (RF…) for automatic matching, or no reference at all.' },
      { q: 'What is the difference between a QR reference and a Creditor Reference?', a: 'A QR reference has 27 digits, works only with a QR-IBAN and is used in Switzerland and Liechtenstein. A Creditor Reference starts with RF, has 5 to 25 characters, works with an ordinary IBAN and is an international standard. Both allow automatic matching.' },
      { q: 'Can a QR-bill be sent by email?', a: 'Yes. As a PDF, it is paid in e-banking or mobile banking. The payment part and receipt are outlined with a scissors symbol, so a member can print and cut them out to pay at the counter.' },
      { q: 'Can a QR-bill be in euros, or have no amount?', a: 'It can be in Swiss francs or euros, and no other currency. The amount and the payer’s address are optional: empty boxes replace them, which suits donations.' },
      { q: 'What changed for the QR-bill in November 2025?', a: 'Since 22 November 2025, addresses must be structured, with street, number, postcode and town in separate fields, and more accented characters are allowed. Combined address lines were only tolerated until November 2026.' },
    ],
  },
  cta: {
    heading: 'Dues out, payments matched',
    text: 'CadenceE puts a Swiss QR-bill on each member’s statement and matches your bank file against what every member owes, so the treasurer sees at once who has paid.',
  },
};

export default guide;
