# The accounting journal export

Club settings → Payments → Accounts sets the accounts; Payments & bank → Accounting export
downloads the journal for a period (money.manage). The API is
`GET /api/accounting/journal?from=…&to=…&layout=gross|net&format=csv` (JSON without `format`).

The journal is the club ledger's entries as double-entry bookings: every line debits one
account and credits another with the same amount. Each entry is booked in the period it counts
in, so the journals of consecutive periods add up to the journal of the whole: at any date,
the receivables account equals what open charges still owe, and customer prepayments the
credit members hold.

## The file

- UTF-8 with a byte-order mark, lines ending CR LF, fields separated by semicolons (as Swiss
  Excel writes them), a header line first.
- Dates `dd.MM.yyyy`, the day in the workspace's time zone.
- Amounts with a full stop and no thousands separator (`1949.75`), always positive: which
  account is debited says the direction.
- A text field that would start a spreadsheet formula (`=`, `+`, `-`, `@`) gets a leading `'`.

| Column | What it holds |
| --- | --- |
| Date | The day the entry counts on. |
| Document | A short document number: a letter for the kind of entry and the first 8 characters of its id (`C-1A2B3C4D`). C charge, V void of a charge, O opening balance, A correction, P payment, R refund, W payment voided, K credit used or released, G gift card. The invoice printed for a charge (House accounts, Invoice) carries the same number. |
| Debit | The account debited. |
| Credit | The account credited. |
| Amount | The amount. With layout `gross`, VAT included. |
| Currency | The account's currency (ISO 4217). |
| VatCode | Your accounting program's VAT code for the rate, as set under Accounts (empty until set). |
| VatRate | The VAT rate in percent the amount includes (8.1, 2.6, 3.8), when the charge's rate is known. |
| VatAmount | The VAT the amount includes. |
| Text | What the entry was, and whose. |
| Member | The member's name, for entries on a member's account. |
| Type | Charge, ChargeVoid, Opening, Correction, Payment, PaymentVoid, Refund, CreditApplied, CreditReleased, GiftCardIssue, GiftCardTopUp, GiftCardRedeem, GiftCardExpiry, GiftCardVoid, GiftCardAdjustment; with `net`, a VAT line is the type with `Vat` after it. |
| EntryId | The ledger entry's id, to trace a line back. |

### Layouts

- `gross` (the default): one line per charge, for the gross amount, with its VAT code. Bexio
  and Abacus book the VAT themselves from the code; set your program's codes for 8.1, 2.6 and
  3.8 under Accounts first.
- `net`: the revenue line without VAT, and the VAT on a line of its own, debiting receivables
  and crediting VAT owed (2200). For a program that takes no VAT codes, or a plain ledger.

## What is booked where (Swiss SME chart defaults)

| Entry | Debit | Credit |
| --- | --- | --- |
| A charge (dues, dining, bar, events, spa, bookings, retail) | 1100 receivables | revenue by kind: 3400 dues, events, spa, bookings; 3200 dining, bar, retail; 3600 other |
| A charge voided | the same revenue account | 1100 |
| A balance brought over, owed / in credit | 1100 / 9100 | 9100 / 2030 |
| A balance set by hand, owed more / credited | 1100 / 3800 | 3600 / 2030 |
| Money received: cash, bank transfer or cheque, card, TWINT or SEPA (Stripe or a terminal) | 1000 / 1020 / 1090 | 1100 for what it paid when it came in, 2030 for what it left as credit |
| Credit used on a charge later | 2030 | 1100 |
| A cancelled charge's payment kept as credit | 1100 | 2030 |
| A refund, or a payment voided | 1100 for the charges that owe again, 2030 for the rest | the account the money came in to (or the refund's own method's) |
| A gift card sold or topped up | 1000 (no payment is recorded for the sale) | 2030 gift card liability |
| A gift card spent / expired | 2030 | 3600 / 3600 |
| A gift card voided | 2030 | 1000 |

Every account above can be changed. The chart has no account of its own for gift cards; a club
that keeps them apart from members' prepayments sets one (2035, say).

Stripe pays out net of its fees; book the payout from 1090 to 1020 and the fees to 6940 (bank
charges) from Stripe's payout report. TWINT and card terminals the same way.

### Sales at the till and from the POS

A sale rung up at the house-charge screen, and a ticket imported from Lightspeed and posted to a
member, are charges like any other: one per VAT rate on the sale, each at its rate. What was
paid at the till (card, cash or TWINT) is a payment of those charges, booked to 1090 or 1000 as
above. What was put on the house account stays in 1100 until the member pays it.

Lightspeed keeps its own sales journal, and its accounting export books the same tickets again.
Book the sales that reach members' accounts from one system only: either leave the POS tickets'
charges out of what you import from Cadence, or leave the members' bills out of Lightspeed's
export. A POS ticket's lines in the journal name its receipt in their text ("Main Dining, receipt
R40: …"), which tells them apart. Tickets that are not a member's (paid at the POS with nobody on
them) never reach Cadence's ledger.

## Importing

- **Bexio**: Accounting → Manual entries → Import (CSV). Map Date, Document (reference),
  Debit, Credit, Amount, Text and, with `gross`, VatCode (the tax). Bexio's accounts must exist
  with these numbers.
- **Abacus**: FIBU → Buchungen importieren (CSV/AbaConnect). Map the same columns; the field
  separator is a semicolon.

The column mapping is done once in each program and kept. No connection to Bexio's or
Abacus's API is made: the export is a file you import.
