# SEO for www.cadencee.ai

What the public site does for search engines and link previews, what the owner still has to
do, how to add a page, and what to expect. Code lives in `src/app/marketing/` and
`scripts/seo/`.

## Before 2027: the launch offer

CadenceE is **free until 31 December 2026** for everyone who signs up. The offer appears on
the home page (hero ribbon, "Start free" buttons, a strip by the final call to action), on
every landing page (a badge in the hero, "Start free", a strip above the footer), on
`/pricing`, `/fr/tarifs` and `/de/preise`, on the free-CRM page (below), in the titles of the
home pages ("Free Swiss CRM for Clubs and Sales Teams", "CRM gratuit pour clubs et équipes de
vente", "Kostenloses CRM für Clubs und Vertrieb"), in the H1 of the French and German home
pages, in the meta description of every page (ending "Free until end of 2026." / "Gratuit
jusqu’à fin 2026." / "Kostenlos bis Ende 2026."), in the home, pricing and free-CRM OG images,
in the footer (a link to the free-CRM page), in `llms.txt`, and as a schema.org `Offer`
(CHF 0, `priceValidUntil` 2026-12-31) on the SoftwareApplication data.

### The free-CRM page (offer only)

`/free-crm`, `/fr/crm-gratuit`, `/de/kostenloses-crm` (`id: 'free-crm'`, words in
`src/app/marketing/content/freeCrm.ts`) is for people who search for a free CRM. Its angle is
not the price (that is the pricing page, so the two do not compete) but what the free offer
is: every feature, the whole team, no credit card, sign up in minutes, built in Switzerland,
English, French and German, data exportable, and "try the demo" (`/login?demo=1`, a demo
workspace with sample data, no account). It is a `landing` page marked `offerOnly` in
`routes.ts`, so it is never in the topic grids, menus or 404 suggestions (`LANDING_PAGE_IDS`);
it is linked from the footer's Product column and from the pricing page (a related card and a
FAQ answer) while the offer runs.

On 1 January 2027 it turns itself off: the page shows its `afterOffer` words ("the launch
offer has ended", ask for pricing, try the demo), its robots meta becomes `noindex, follow`,
it leaves `sitemap.xml` (42 URLs → 39) and `llms.txt`, its OG image becomes the plain one,
and the footer and pricing links to it disappear. The URL keeps answering (no 404), so old
links still land somewhere true. **Owner to-do in 2027:** once Search Console shows the
three URLs dropped from the index, either keep them as they are, redirect them to the pricing
page, or, if 2027 brings a real free plan, rewrite the page for it and remove `offerOnly`.

It ends by itself. Everything asks `isLaunchOfferActive()` in `src/app/lib/launchOffer.ts`
(true through 31 December 2026, 23:59:59 Europe/Zurich), which also holds the offer's words in
the three languages:

- the prerender, run at each deploy, leaves the offer out once it has ended;
- the browser checks again with its own clock when the app starts, and `index.html` hides
  every `[data-launch-offer]` element before the first paint from 1 January 2027, so HTML built
  in 2026 never shows a stale offer to a visitor;
- `deploy.yml` redeploys `main` at 00:07 in Zurich on 1 January, so the static HTML that
  crawlers read loses it too. (GitHub turns scheduled workflows off after 60 days without a
  commit; any push to `main` after New Year does the same job.)

From 1 January 2027 the pricing pages show their `afterOffer` words ("ask us for the current
pricing"), the French and German home pages their former H1 ("Le CRM suisse qui gère votre
club et vos ventes", "Das Schweizer CRM für Ihren Club und Ihren Vertrieb"), and every title
and description its plain wording. **Owner to-do before 2027:** decide the 2027 pricing, announce it well before
31 December as the pages promise, and replace `afterOffer` in
`src/app/marketing/content/pricing.ts` (and the pricing meta in `meta.ts`) with the real
plans. When a real price exists, add an `offers` with it in `softwareApplicationJsonLd`
(`src/app/marketing/jsonld.ts`).

## What was done

**Crawlability**

- `public/robots.txt`: the public site open, `/api/` and the app's pages closed (each as an
  exact path and its sub-paths, so `/members` never closes `/membership-management-software`),
  the sitemap named.
- `sitemap.xml`, written at build from the route manifest: every public page in English,
  French and German with `lastmod` and hreflang alternates (en, fr, de, x-default), plus
  /help, /privacy and /terms.
- Real 404s. `scripts/seo/swa-config.mjs` turns `public/staticwebapp.config.json` into the
  deployed config: each prerendered page rewritten to its own file, each top-level segment of
  the app's routes (read from `src/app/App.tsx`, so new app routes are covered automatically)
  rewritten to `app.html` (the SPA shell, noindex) with `X-Robots-Tag: noindex`, and every
  other address a 404 with the prerendered 404 page. The committed config keeps a navigation
  fallback, so a build without the prerender still works as before.
- Headers: a year's cache for hashed assets, a week for OG images, `no-cache` for pages; MIME
  types for `.xml`, `.txt`, `.webmanifest`; `nosniff`, `Referrer-Policy`, HSTS,
  `Permissions-Policy` (camera, microphone, geolocation off) and a CSP limited to `base-uri`,
  `object-src` and `frame-ancestors` (nothing restricts what the app loads; the public forms
  under `/capture`, `/apply` and `/member-portal` may still be framed by a club's website).

**Head per route** (`src/app/marketing/seo.ts`, `SeoHead.tsx`): title, description,
canonical on `https://www.cadencee.ai` (no query string, no trailing slash), robots (index
for the public site and /login; `noindex, follow` for the password and email pages and the
404; `noindex, nofollow` for every app page), Open Graph and Twitter cards with an absolute
1200×630 PNG, hreflang, and JSON-LD. The same function writes the prerendered HTML and keeps
the head right in the browser; the app's own pages keep their titles.

**Prerendering** (`scripts/seo/prerender.mjs`, run by `npm run build` after `vite build`):
Vite builds `src/entry-prerender.tsx` for Node and React's `renderToString` writes each public
page into the `index.html` Vite produced. Chosen over a Playwright crawl because it needs no
browser in CI (about two seconds, no install), is deterministic (no timing, no API calls, no
half-played animations captured) and renders exactly the components the app renders. The app
then takes over: `main.tsx` fetches the page's chunk before the first render, so React
replaces the static HTML with the same page in one step (no loading screen, no layout shift).
The build fails if a page's JSON-LD does not parse or a page has other than one H1.

**Structured data** (`jsonld.ts`): Organization (name, url, logo, Swiss address country),
WebSite, SoftwareApplication (BusinessApplication, Web, feature list per language; no rating,
no review; the Offer only during the launch offer) on the home and features and pricing pages,
BreadcrumbList on every subpage, FAQPage on every page with an FAQ. Tests check it parses and
has what it should.

**Social images**: `public/og/<page>-<lang>.png` for every page and language, offer versions
for home, pricing and free CRM (`OFFER_OG_PAGES` in `seo.ts`: every page with `afterOffer`
words belongs there, its plain image drawn from those words), and `default.png`; `logo-512.png`, `apple-touch-icon.png`,
`favicon-32.png`. Redraw with `npm run seo:og-images` (needs Playwright:
`npm i -D playwright` or `NODE_PATH=$(npm root -g)` with a global install) and commit.

**Content**: twelve pages in each language, plus the free-CRM page while the launch offer
runs, French and German written for Switzerland with their own slugs; 600 to 1,300 words each, one H1, sections tied to real features, how it
works, five to seven FAQ questions, related pages, tagged calls to action. The home page lost
every claim CadenceE cannot back (ratings, user counts, SOC 2, "#1", AES-256 zero-knowledge,
uptime SLA, data centres, "free forever") and gained one keyword H1, descriptive headings and
links to every topic page.

**Performance**: the main JS chunk went from 1,222 KB to 604 KB (the home page, login and
password pages, the signed-in layout pieces and the email composer load on demand); the home
page no longer re-renders on every mouse move and skips rendering off-screen sections
(`content-visibility: auto`). The site uses the system font stack: there is no web font to
preload, and no font swap can shift the layout.

**Search engines**: `VITE_GOOGLE_SITE_VERIFICATION` and `VITE_BING_SITE_VERIFICATION`
(repository variables, empty by default) add the verification meta tags at build. IndexNow:
the key file `public/c1393c209ec461dc12a843b6d71362b0.txt` (public by design) and
`scripts/seo/indexnow.mjs`, which `deploy.yml` runs after each deploy with the new and changed
URLs; it never fails a deploy. `llms.txt` describes the site for AI search, built at deploy.

**Analytics hooks**: calls to action carry `data-track="cta-signup"`, `cta-login`,
`cta-demo`, `cta-contact`, and `cta-try-demo` for "try the demo" links in the copy
(`/login?demo=1`); internal navigation `nav-<page>` (`nav-features`, `nav-pricing`,
`nav-private-clubs`, …) and the language switch `nav-lang-<en|fr|de>`.

## Owner's to-do list

1. **Contact address.** Set the repository variable `VITE_CONTACT_EMAIL` (e.g. a monitored
   inbox) so "Contact us" and /help's "Contact support" open an email; until then they lead
   to /help, which then says how to reach the team without an address.
2. **Google Search Console**: add the property `https://www.cadencee.ai` (or the domain
   property `cadencee.ai` with a DNS TXT record). With the HTML tag method, put the code in
   the repository variable `VITE_GOOGLE_SITE_VERIFICATION` and deploy. Then submit
   `https://www.cadencee.ai/sitemap.xml`, and use URL Inspection on `/`, `/fr` and `/de` to
   request indexing.
3. **Bing Webmaster Tools**: import the site from Search Console, or verify with
   `VITE_BING_SITE_VERIFICATION`; submit the sitemap. IndexNow then keeps Bing current.
4. **Pricing for 2027**: see "Before 2027" above.
5. **Backlinks** (the biggest off-site lever for a new domain):
   - a link from Pavillon 46's website (e.g. "Member management by CadenceE" in its footer,
     to `https://www.cadencee.ai/` or `/fr`);
   - a CadenceE company page on LinkedIn linking to the site, and posts linking to the
     topic pages;
   - Swiss directories and listings: local.ch / search.ch, Swiss software and startup
     directories, the Vaud and Lausanne business associations, software review sites such as
     Capterra or GetApp (real reviews only);
   - Bexio's and Abacus's partner or integration directories, Lightspeed's app marketplace,
     TWINT's merchant listings, if CadenceE qualifies;
   - the lists Swiss clubs and associations search: Swiss Made Software's list of Swiss CRM
     providers (swissmadesoftware.org), PostFinance's association-software partners, and
     European membership-tool directories (e.g. europeanpurpose.com);
   - the guides (below) are what other sites link to: share them where clubs, associations
     and sales teams ask these questions.
6. **Google Business Profile**: only if CadenceE has a staffed address customers may visit
   or a service area; a software company without one does not need it.
7. Watch Search Console's Pages report for "Crawled, currently not indexed" and Core Web
   Vitals; improve the pages that get impressions but few clicks (titles, descriptions).
8. When the app gains a route, nothing is needed for routing (it is read from `App.tsx`);
   add its path to `public/robots.txt` if it should not be crawled.

## Adding a public page

1. `src/app/marketing/routes.ts`: add the id to `MARKETING_PAGE_IDS` and an entry to
   `MARKETING_PAGES` with its address in each language and `lastmod` (today). Kind
   `landing` puts it in the menus and the solutions grids, unless it is `offerOnly` (a page
   about the launch offer: indexed and listed only while it runs, with `afterOffer` words
   for after, and its id in `OFFER_OG_PAGES`).
2. `src/app/marketing/meta.ts`: its title (≤ 60 characters, ending "| CadenceE"),
   description (≤ 155) and short `nav` name in `en`, `fr` and `de`.
3. `src/app/marketing/content/{en,fr,de}.ts`: its words (see `content/types.ts`: eyebrow, H1,
   lead, intro, sections, features, steps, FAQ, related pages, call to action). Link other
   pages in the text with `[label](page:id)`.
4. Optionally list it in the header (`HEADER_PAGES`) or footer (`FOOTER_PRODUCT`,
   `FOOTER_SOLUTIONS`), and redraw the OG images (`npm run seo:og-images`).
5. `npx vitest run src/app/marketing` checks unique titles and descriptions, lengths, word
   count, FAQ size, JSON-LD and links; `npm run build` prerenders it, adds it to the sitemap
   and to the server routes.

When a page's words change, bump its `lastmod` so the sitemap and IndexNow say so.

## Adding a guide

The guides (`/guides`, `/fr/guides`, `/de/ratgeber`) are long-form articles on the questions
clubs, venues and sales teams search for. Each is an Article in the structured data (with
its FAQPage and a Home › Guides › guide breadcrumb), links into the topic pages, and is
listed under "Further reading" on the topic pages named in its `topics`.

1. `routes.ts`: add the id to `GUIDE_IDS` and an entry (`kind: 'guide'`, `published`,
   `lastmod`, `topics`, an address per language) to `MARKETING_PAGES`.
2. `content/guides/meta/<id>.ts`: title, description and short name per language (same
   limits as any page), registered in `content/guides/meta/index.ts`.
3. `content/guides/{en,fr,de}/<id>.ts`: the words (see `content/guides/types.ts`), registered
   in each language's `index.ts`. No price and no launch offer in a guide: the page adds the
   offer itself while it runs, and a guide outlives it.
4. `npx vitest run src/app/marketing/guides.test.ts` checks length (≥ 1,000 words per
   language), shape, links into the site and claims; redraw the OG images.

## Keywords per page

| Page | English | French | German |
|---|---|---|---|
| Home | Swiss CRM, CRM for private clubs, CRM for sales teams (until 2027: free Swiss CRM) | CRM suisse, CRM club privé (until 2027: CRM gratuit) | Schweizer CRM, CRM für Privatclubs (until 2027: kostenloses CRM) |
| Features | CRM features, club management software | fonctionnalités CRM, logiciel de gestion de club | CRM-Funktionen, Clubsoftware |
| Private clubs | CRM for private members' clubs, members club software | CRM pour club privé, club de membres | CRM für Privatclubs, Members' Club Software |
| Membership | membership management software, member renewals | logiciel de gestion des membres, gestion des adhésions | Mitgliederverwaltung Software, Mitgliederverwaltung Club |
| Hospitality | hospitality CRM, restaurant CRM, event venue CRM | CRM hôtellerie restauration, CRM restaurant | CRM Gastronomie, CRM Hotellerie |
| Sales | sales CRM, lead tracking, lead management | CRM commercial, suivi des prospects | Vertriebs-CRM, Lead-Management |
| E-signature | e-signature contracts, contract deposit at signing | signature électronique contrat, acompte | Verträge elektronische Signatur, E-Signatur |
| Swiss | Swiss CRM, QR-bill, TWINT, Bexio, Abacus, FADP | CRM suisse, facture QR, TWINT, Bexio, nLPD | Schweizer CRM, QR-Rechnung, TWINT, Bexio, DSG |
| Billing | club billing software, membership dues, statements | facturation club, cotisations | Club-Abrechnung, Mitgliederbeiträge |
| Email | email automation CRM, newsletters, sequences | CRM automatisation e-mails, newsletter | E-Mail-Automatisierung CRM, Newsletter |
| AI | AI CRM assistant, AI sales writer, Claude | assistant IA CRM | KI-Assistent CRM, KI-Texter |
| Pricing | CadenceE pricing, CRM price | tarifs CRM, prix CRM | CRM Preise, CRM Kosten |
| Free CRM (until 2027) | free CRM, free CRM software, free CRM Switzerland, free CRM for clubs, free membership management software, free sales CRM | CRM gratuit, logiciel CRM gratuit, CRM gratuit Suisse, CRM gratuit pour club / association | kostenloses CRM, CRM kostenlos, CRM Software kostenlos, kostenloses CRM Schweiz, Mitgliederverwaltung kostenlos |

| Guide: run a club | how to run a private members club, members club management | gérer un club privé, gestion d'un club de membres | Privatclub führen, Clubverwaltung |
| Guide: member retention | membership renewal, member retention strategies | renouvellement des adhésions, fidélisation des membres | Mitgliederbindung, Mitgliedschaft verlängern |
| Guide: QR-bill | Swiss QR-bill, QR-bill for associations | facture QR, QR-facture association | QR-Rechnung, QR-Rechnung Verein |
| Guide: Excel to CRM | Excel vs CRM, move from Excel to CRM | passer d'Excel à un CRM, Excel ou CRM | Excel vs CRM, von Excel zum CRM |
| Guide: sales follow-up | sales follow-up email, follow-up email templates | email de relance commerciale, modèle de relance | Nachfass-E-Mail, Follow-up-E-Mail Vorlage |
| Guide: FADP checklist | Swiss FADP checklist, member data protection | checklist nLPD, protection des données association | nDSG Checkliste, Datenschutz Verein |
| Guide: membership applications | membership application process, new member onboarding | processus d'adhésion, admission de nouveaux membres | Aufnahmeprozess Verein, Mitgliedsantrag |
| Guide: choose a CRM | how to choose a CRM, CRM selection checklist | comment choisir un CRM, critères de choix | CRM auswählen, CRM-Auswahl Kriterien |

These come from what the product does and how Swiss buyers describe it, not from a keyword
tool's volumes; check them against Search Console's queries after a few weeks and adjust.

## Checking

- `npm run build`, then serve `dist` with the Static Web Apps CLI to test routing as Azure
  does: `npx @azure/static-web-apps-cli start dist --swa-config-location dist`.
- `curl -s https://www.cadencee.ai/sales-crm | grep -E '<title>|canonical|ld\+json'`, and
  `curl -sI https://www.cadencee.ai/no-such-page` (expect 404).
- Rich Results Test (search.google.com/test/rich-results) and the Schema Markup Validator
  (validator.schema.org) on a few pages; the LinkedIn Post Inspector for the OG images.

## Realistic expectations

A new domain with no backlinks takes weeks to be fully indexed and usually months to rank
for competitive terms such as "Swiss CRM" or "membership management software". Long-tail,
specific searches ("CRM club privé Vaud", "QR-Rechnung CRM Verein", "CRM with TWINT and
Bexio") are where a site like this can rank first. Links from real, related sites (Pavillon
46, Swiss directories, partners) and pages that answer real questions matter more than
anything on this list. Nobody can guarantee a ranking.
