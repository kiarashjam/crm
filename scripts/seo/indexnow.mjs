#!/usr/bin/env node
// Tells IndexNow (Bing, Yandex, Seznam, Naver and the others that share it) which public
// pages are new or changed, after a deploy. Google does not use IndexNow; it reads the
// sitemap named in robots.txt.
//
//   node scripts/seo/indexnow.mjs --sitemap dist/sitemap.xml [--previous previous-sitemap.xml] [--dry-run]
//
// With --previous (the sitemap the live site served before this deploy), only addresses
// that are new or whose <lastmod> moved are sent; without one, or on the first deploy,
// all of them. The key is public by design: it is the file the search engines fetch from
// the site to check the ping comes from its owner. The script never fails a deploy: if the
// key file is not live yet or the endpoint refuses, it says so and exits 0.

import { existsSync, readFileSync } from 'node:fs';

const KEY = 'c1393c209ec461dc12a843b6d71362b0';
const HOST = 'www.cadencee.ai';
const KEY_LOCATION = `https://${HOST}/${KEY}.txt`;
const ENDPOINT = 'https://api.indexnow.org/indexnow';

const args = process.argv.slice(2);
const arg = (name) => {
  const i = args.indexOf(name);
  return i > -1 ? args[i + 1] : undefined;
};
const sitemapPath = arg('--sitemap') ?? 'dist/sitemap.xml';
const previousPath = arg('--previous');
const dryRun = args.includes('--dry-run');

/** loc → lastmod, from a sitemap's text; empty for anything that is not one. */
export function readSitemap(xml) {
  const urls = new Map();
  if (!xml || !xml.includes('<urlset')) return urls;
  for (const block of xml.matchAll(/<url>([\s\S]*?)<\/url>/g)) {
    const loc = block[1].match(/<loc>([^<]+)<\/loc>/)?.[1]?.replace(/&amp;/g, '&');
    const lastmod = block[1].match(/<lastmod>([^<]+)<\/lastmod>/)?.[1] ?? '';
    if (loc) urls.set(loc, lastmod);
  }
  return urls;
}

/** What to send: addresses on the host that are new or have a new lastmod. */
export function changedUrls(current, previous) {
  const out = [];
  for (const [loc, lastmod] of current) {
    if (!loc.startsWith(`https://${HOST}/`)) continue;
    if (!previous.has(loc) || previous.get(loc) !== lastmod) out.push(loc);
  }
  return out;
}

async function main() {
  if (!existsSync(sitemapPath)) {
    console.log(`indexnow: no ${sitemapPath}, nothing to send`);
    return;
  }
  const current = readSitemap(readFileSync(sitemapPath, 'utf8'));
  const previous = previousPath && existsSync(previousPath) ? readSitemap(readFileSync(previousPath, 'utf8')) : new Map();
  const urls = changedUrls(current, previous).slice(0, 10000);
  if (urls.length === 0) {
    console.log('indexnow: no new or changed pages');
    return;
  }
  if (dryRun) {
    console.log(`indexnow (dry run): would send ${urls.length} URLs\n  ${urls.join('\n  ')}`);
    return;
  }
  // The search engines fetch the key file to check the ping: send nothing until it is live.
  const keyFile = await fetch(KEY_LOCATION).then((r) => (r.ok ? r.text() : ''), () => '');
  if (keyFile.trim() !== KEY) {
    console.log(`indexnow: ${KEY_LOCATION} does not serve the key yet; skipped`);
    return;
  }
  const response = await fetch(ENDPOINT, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json; charset=utf-8' },
    body: JSON.stringify({ host: HOST, key: KEY, keyLocation: KEY_LOCATION, urlList: urls }),
  }).catch((error) => ({ ok: false, status: 0, statusText: String(error) }));
  // 200 OK and 202 Accepted are both success.
  console.log(`indexnow: sent ${urls.length} URLs, answer ${response.status} ${response.statusText ?? ''}`.trim());
}

if (import.meta.url === `file://${process.argv[1]}`) {
  await main().catch((error) => console.log(`indexnow: ${error.message}; skipped`));
}
