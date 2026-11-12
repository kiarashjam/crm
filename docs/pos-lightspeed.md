# The POS: Lightspeed Restaurant K-Series

Cadence imports the tickets the club's Lightspeed Restaurant (K-Series) POS closes and posts each
member's to their house account. A ticket put on the house account is owed; a ticket paid at the
POS is recorded as paid. Either way the member earns the points the club's settings give. A club
without a POS rings up sales on the house-charge screen (`/pos`) instead.

## Why Lightspeed K-Series

A closed K-Series sale, as its Financial API returns it, carries everything a member's account
needs:
- its lines, with each line's tax rate and amount;
- its payments, by payment method, including a method the club can define for the house account;
- the staff member and the table;
- the customer on it.

K-Series began as iKentoo, in Geneva, and is common in Swiss restaurants and clubs.

SumUp's public API was the other candidate. It reports card and cash payments with their
products and VAT, but it has no table and no customer on a payment taken in person, and no way
to leave a bill on an account. Its webhooks only cover checkouts started through its own API.

## How tickets come in

K-Series has no webhook that reports every closed ticket. The notifications its public
documentation describes belong to its ordering and reservation-platform APIs and cover the orders
placed through them. So Cadence reads the Financial API:
`GET /f/v2/business-location/{id}/sales?from=…&to=…&include=staff,table,consumer,payments`,
page by page. It does this every five minutes for every workspace that turned the import on, and
when someone presses "Import now".

- **One import at a time.** Each run claims the workspace with one conditional update (a
  10-minute lease on its POS settings). Two app instances, or the job and a click, never import
  the same workspace at once.
- **None passed over.** A run reads its window a day at a time, oldest first, and notes how far
  it has read only past the days it read in full. A run cut short (Lightspeed not answering, or
  the 60 requests one run makes at most) leaves the rest to the next run, which starts again at
  the end of the last whole day. Each run also reads back six hours before that point, for tills
  that were offline.
- **Once each.** The POS's id for a ticket (location and receipt) is unique in the workspace, so
  a ticket read again adds nothing. A ticket is posted as one sale, keyed by the ticket. If the
  sale is posted and the ticket could not say so at that moment, the ticket is marked posted
  from the sale the next time anyone or any import looks at it.

## Whose a ticket is

A ticket is a member's when:
1. the POS recorded one of their **codes**. Give a member a code on their page under "Codes at
   the POS": their existing member number, the code on their card (a QR code holds the same), or
   a new nine-digit code with a check digit. At the POS, scan or type the code as the bill's
   name, or keep it on the Lightspeed customer (card number, customer number or external
   reference); or
2. its Lightspeed **customer** is one staff already matched to them (matching a ticket remembers
   the customer); or
3. the customer's **email** is the email of exactly one member.

What happens to a ticket:
- **Posted.** Put on the house account, or paid at the POS, with a member found.
- **Needs a member.** Put on the house account with no code on it, or naming a customer no code
  matches. Staff match it on the POS tickets page.
- **Not posted.** The member is known, but a payment method can't be read, the lines and the
  payments don't add up, or it is a refund (refunds are not posted on their own). Fix the cause
  and try again.
- **Not a member sale.** Paid at the POS with nobody on it, cancelled at the POS, or paid by a
  method the settings call "not a member sale" (staff meals, say). A ticket put partly on a
  house account is never set aside this way: it waits as "not posted", saying why, because a
  member owes that part.

Payment methods are read from their names (Visa, Mastercard, Karte, Cash, Bar, TWINT, House
account, Konto…). One that can't be told keeps its tickets waiting until the POS settings say
what it means.

A tip on the house account is owed with the ticket and earns no points. A tip paid at the POS is
the staff's and is not posted. A difference of up to five centimes between the lines and the
payments (Swiss cash rounding) is evened out on the payment, so each line's VAT stays as the
POS charged it. A bigger difference keeps the ticket waiting.

## Setting it up

Once per server (the owner):
1. Ask Lightspeed for API access as a partner, and register an API client for Cadence with the
   redirect address `https://<app host>/pos-tickets`: the app's POS tickets page, which the
   Connection tab shows. Lightspeed sends the browser back there with a code, and the page,
   signed in, passes it to the API. The API finishes connecting only for the person who started
   it, in the workspace they started it in, so a Lightspeed link someone else made connects
   nothing.
2. Set the app settings `Pos__Lightspeed__ClientId` and `Pos__Lightspeed__ClientSecret`. Set
   `Pos__Lightspeed__ApiBase` only for Lightspeed's trial environment
   (`https://api.trial.lsk.lightspeed.app/`), and `Pos__Lightspeed__Scope` only if Lightspeed
   names another scope than `financial-api`.
3. `Email__FrontendBaseUrl` must be the app's public address: it builds the redirect address.

Per club (someone with money.manage), on POS tickets → Connection:
1. Connect Lightspeed, and allow Cadence on Lightspeed's page.
2. Choose the location, check the date to import from (by default the day the import is turned
   on, so nothing older is billed), and say where its sales count as sold.
3. List any payment method whose name doesn't say what it is. In particular, list the method the
   club uses for the house account if its name has neither "account" nor "konto" in it.
4. Turn the import on.

The tokens Lightspeed gives are kept encrypted with the server's Data Protection keys and never
shown. If those keys are lost, connect again. A copy of a workspace keeps no tokens and imports
nothing.

## Not built

Refund tickets from the POS are not posted on their own: void the charge or record a credit on
the house account page. Gift cards and points are not ways to pay at the POS. One location is
imported per workspace. Nothing here has been tried against a live Lightspeed account: the HTTP
tests answer for Lightspeed in the shapes its documentation gives, and the field names are read
generously (older and newer names) for that reason. Try it on Lightspeed's trial environment
before turning the import on for real tickets.
