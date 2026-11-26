#!/usr/bin/env node
// Prerenders the public site after `vite build` (npm run build runs both):
//
//   dist/index.html, dist/features/index.html, dist/fr/…, dist/de/…   marketing pages, full HTML
//   dist/login/index.html, /help, /privacy, /terms, …                 their head (the app renders the body)
//   dist/404.html                                                      the 404 page (noindex)
//   dist/app.html                                                      the SPA shell for the app's pages (noindex)
//   dist/sitemap.xml, dist/llms.txt                                    from the route manifest
//   dist/staticwebapp.config.json                                      explicit SPA routes and a real 404
//
// How: Vite builds src/entry-prerender.tsx for Node (SSR), and React's renderToString
// renders each page into the index.html Vite produced, so each page keeps the same
// scripts and styles and the app takes over in the browser. No browser is needed, so CI
// only spends a few seconds on it.
//
// Usage: node scripts/seo/prerender.mjs [--dist dist]

import { existsSync, statSync } from 'node:fs';
import { mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { loadRenderer, root } from './ssr.mjs';
import { spaRouteSegments, staticWebAppConfig } from './swa-config.mjs';

const distArg = process.argv.indexOf('--dist');
const dist = path.resolve(root, distArg > -1 ? process.argv[distArg + 1] : 'dist');

const started = Date.now();

function fail(message) {
  console.error(`prerender: ${message}`);
  process.exit(1);
}

if (!existsSync(path.join(dist, 'index.html'))) fail(`no ${path.relative(root, dist)}/index.html: run vite build first`);

// 1. The renderer, built for Node.
const renderer = await loadRenderer();

// 2. The template: the SPA shell Vite wrote. It is kept as app.html before index.html is
//    overwritten with the home page.
//    (Run twice, index.html is already the home page: app.html is the shell then.)
const indexHtml = await readFile(path.join(dist, 'index.html'), 'utf8');
const template = indexHtml.includes('<div id="root"></div>') ? indexHtml : await readFile(path.join(dist, 'app.html'), 'utf8');
if (!template.includes('<!-- seo:head -->') || !template.includes('<!-- /seo:head -->')) fail('index.html has no seo:head markers');
if (!template.includes('<div id="root"></div>')) fail('index.html has no empty #root');

const manifestPath = path.join(dist, '.vite/manifest.json');
const manifest = existsSync(manifestPath) ? JSON.parse(await readFile(manifestPath, 'utf8')) : {};

/**
 * The chunk of a source module and the chunks it imports statically that are worth a
 * preload (8 KB or more): a page waits for these before the app takes over. Dozens of tiny
 * shared chunks (an icon each) would only compete with the stylesheet for the first paint;
 * they arrive with the page chunk anyway.
 */
const MIN_PRELOAD_BYTES = 8 * 1024;
function chunksOf(module) {
  const files = new Set();
  const visit = (key) => {
    const entry = manifest[key];
    if (!entry || files.has(entry.file)) return;
    files.add(entry.file);
    for (const dep of entry.imports ?? []) {
      if (!manifest[dep]?.isEntry) visit(dep);
    }
  };
  visit(module);
  const own = manifest[module]?.file;
  return [...files].filter((file) => file === own || statSync(path.join(dist, file)).size >= MIN_PRELOAD_BYTES);
}

function fill(page) {
  let html = template
    .replace(/<html lang="[^"]*">/, `<html lang="${page.lang}">`)
    .replace(/<!-- seo:head -->[\s\S]*?<!-- \/seo:head -->/, `<!-- seo:head -->\n    ${page.headHtml}\n    <!-- /seo:head -->`)
    .replace('<div id="root"></div>', `<div id="root">${page.bodyHtml}</div>`);
  if (page.module) {
    const preload = chunksOf(page.module)
      .map((file) => `<link rel="modulepreload" crossorigin href="/${file}">`)
      .join('\n    ');
    if (preload) html = html.replace('</head>', `    ${preload}\n  </head>`);
  }
  return html;
}

async function write(relative, content) {
  const file = path.join(dist, relative);
  await mkdir(path.dirname(file), { recursive: true });
  await writeFile(file, content);
  return file;
}

function fileFor(pagePath) {
  return pagePath === '/' ? 'index.html' : `${pagePath.slice(1)}/index.html`;
}

// 3. Checks every written page passes: JSON-LD that parses, one <h1> where there is a body.
const problems = [];
function check(pagePath, html, page) {
  for (const match of html.matchAll(/<script type="application\/ld\+json"[^>]*>([\s\S]*?)<\/script>/g)) {
    try {
      const data = JSON.parse(match[1]);
      if (data['@context'] !== 'https://schema.org') problems.push(`${pagePath}: JSON-LD without the schema.org @context`);
    } catch (error) {
      problems.push(`${pagePath}: JSON-LD does not parse (${error.message})`);
    }
  }
  if (page.bodyHtml) {
    const h1 = (page.bodyHtml.match(/<h1[\s>]/g) ?? []).length;
    if (h1 !== 1) problems.push(`${pagePath}: ${h1} <h1> elements`);
  }
  if (!/<title>[^<]+<\/title>/.test(html)) problems.push(`${pagePath}: no <title>`);
}

const now = Date.now();
await write('app.html', fill(renderer.renderAppShell()));

const written = [];
for (const pagePath of renderer.prerenderPaths()) {
  const page = renderer.renderPage(pagePath, now);
  const html = fill(page);
  check(pagePath, html, page);
  await write(fileFor(pagePath), html);
  written.push(pagePath);
}

const notFound = renderer.renderNotFound();
const notFoundHtml = fill(notFound);
check('/404.html', notFoundHtml, notFound);
await write('404.html', notFoundHtml);

await write('sitemap.xml', renderer.buildSitemapXml());
await write('llms.txt', renderer.buildLlmsTxt(now));

// 4. The server's routes: every app route rewritten to the shell, everything else a real 404.
const appSource = await readFile(path.join(root, 'src/app/App.tsx'), 'utf8');
const configPath = path.join(dist, 'staticwebapp.config.json');
const baseConfig = JSON.parse(await readFile(configPath, 'utf8'));
const segments = spaRouteSegments(appSource, written);
await writeFile(configPath, `${JSON.stringify(staticWebAppConfig(baseConfig, segments, written), null, 2)}\n`);

// The Vite manifest is for this script only; it is not served.
await rm(path.join(dist, '.vite'), { recursive: true, force: true });

if (problems.length) fail(`\n  ${problems.join('\n  ')}`);

console.log(
  `prerender: ${written.length} pages, 404.html, app.html, sitemap.xml, llms.txt and ${segments.length} app routes ` +
    `in ${((Date.now() - started) / 1000).toFixed(1)}s`,
);
