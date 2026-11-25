#!/usr/bin/env node
// Draws the site's Open Graph images (1200×630 PNG, public/og/) and icons (public/logo-512.png,
// apple-touch-icon.png, favicon-32.png) by screenshotting an HTML template in the brand style
// with headless Chromium. Run by hand when a page's headline changes, and commit the PNGs;
// the build does not run it, so CI needs no browser.
//
//   npm run seo:og-images                       (with playwright installed: npm i -D playwright)
//   NODE_PATH=$(npm root -g) npm run seo:og-images   (with a global playwright)
//
// What each image says comes from src/app/marketing/og.ts. ImageMagick, when installed,
// reduces each PNG to 256 colours, which keeps them small enough to commit.

import { execFileSync } from 'node:child_process';
import { mkdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import path from 'node:path';
import { loadRenderer, root } from './ssr.mjs';

const require = createRequire(import.meta.url);
let chromium;
try {
  ({ chromium } = require('playwright'));
} catch {
  try {
    ({ chromium } = require('playwright-core'));
  } catch {
    console.error('og-images: needs playwright (npm i -D playwright, or NODE_PATH pointing at a global install)');
    process.exit(1);
  }
}

const SPARKLES =
  '<svg viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg"><path d="M16 8l1.5 4.5L22 14l-4.5 1.5L16 20l-1.5-4.5L10 14l4.5-1.5L16 8z" fill="white"/><path d="M22 18l.75 2.25L25 21l-2.25.75L22 24l-.75-2.25L19 21l2.25-.75L22 18z" fill="white" opacity="0.9"/></svg>';

const escape = (s = '') => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

function ogHtml(spec) {
  const titleLength = (spec.title + ' ' + (spec.accent ?? '')).length;
  const size = titleLength > 70 ? 58 : titleLength > 50 ? 66 : 76;
  return `<!doctype html><html lang="${spec.lang}"><head><meta charset="utf-8"><style>
  * { box-sizing: border-box; margin: 0; }
  body { width: 1200px; height: 630px; font-family: "Inter", "Helvetica Neue", Arial, "Liberation Sans", sans-serif;
    background: #0f172a; color: #fff; position: relative; overflow: hidden; }
  .glow { position: absolute; border-radius: 50%; filter: blur(90px); }
  .g1 { width: 620px; height: 620px; right: -160px; top: -220px; background: rgba(234, 88, 12, .55); }
  .g2 { width: 420px; height: 420px; left: -140px; bottom: -200px; background: rgba(245, 158, 11, .35); }
  .wrap { position: absolute; inset: 64px 72px; display: flex; flex-direction: column; }
  .brand { display: flex; align-items: center; gap: 18px; }
  .tile { width: 64px; height: 64px; border-radius: 18px; background: linear-gradient(135deg, #ea580c, #f59e0b 55%, #ea580c);
    display: flex; align-items: center; justify-content: center; box-shadow: 0 12px 32px rgba(234, 88, 12, .45); }
  .tile svg { width: 44px; height: 44px; }
  .word { font-size: 34px; font-weight: 800; letter-spacing: -.5px; }
  .word span { background: linear-gradient(135deg, #f97316, #ec4899, #d946ef); -webkit-background-clip: text; color: transparent; }
  .tag { font-size: 13px; font-weight: 700; letter-spacing: 3px; text-transform: uppercase; color: #fb923c; margin-top: 4px; }
  .kicker { margin-top: 56px; display: inline-flex; align-self: flex-start; padding: 10px 20px; border-radius: 999px;
    background: rgba(255, 255, 255, .08); border: 1px solid rgba(251, 146, 60, .45); color: #fdba74; font-size: 22px; font-weight: 600; }
  h1 { margin-top: 26px; font-size: ${size}px; line-height: 1.06; font-weight: 800; letter-spacing: -1.5px; max-width: 1000px; }
  h1 .accent { background: linear-gradient(90deg, #fb923c, #fbbf24, #fb923c); -webkit-background-clip: text; color: transparent; }
  .foot { margin-top: auto; display: flex; align-items: center; justify-content: space-between; font-size: 22px; color: #cbd5e1; }
  .badge { display: inline-flex; align-items: center; gap: 10px; padding: 12px 22px; border-radius: 999px; font-size: 24px; font-weight: 700;
    background: linear-gradient(90deg, #059669, #0d9488); color: #fff; box-shadow: 0 10px 30px rgba(5, 150, 105, .4); }
  </style></head><body>
  <div class="glow g1"></div><div class="glow g2"></div>
  <div class="wrap">
    <div class="brand"><div class="tile">${SPARKLES}</div><div><div class="word">Cadence<span>E</span></div><div class="tag">Cadence Engine</div></div></div>
    <div class="kicker">${escape(spec.kicker)}</div>
    <h1>${escape(spec.title)}${spec.accent ? ` <span class="accent">${escape(spec.accent)}</span>` : ''}</h1>
    <div class="foot"><span>${escape(spec.footer)}</span>${spec.badge ? `<span class="badge">${escape(spec.badge)}</span>` : ''}</div>
  </div></body></html>`;
}

function iconHtml(size, rounded = true) {
  return `<!doctype html><html><head><style>*{margin:0}body{width:${size}px;height:${size}px;background:transparent}
  .tile{width:${size}px;height:${size}px;border-radius:${rounded ? Math.round(size * 0.22) : 0}px;background:linear-gradient(135deg,#ea580c,#f59e0b 55%,#ea580c);
  display:flex;align-items:center;justify-content:center}.tile svg{width:${Math.round(size * 0.72)}px;height:${Math.round(size * 0.72)}px}</style></head>
  <body><div class="tile">${SPARKLES}</div></body></html>`;
}

function hasImageMagick() {
  try {
    execFileSync('convert', ['-version'], { stdio: 'ignore' });
    return true;
  } catch {
    return false;
  }
}

const renderer = await loadRenderer();
const specs = renderer.ogImageSpecs();
const magick = hasImageMagick();
const browser = await chromium.launch(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {});
const page = await browser.newPage({ viewport: { width: 1200, height: 630 }, deviceScaleFactor: 1 });

async function shoot(html, file, { width, height, transparent = false, quantize = true }) {
  await page.setViewportSize({ width, height });
  await page.setContent(html, { waitUntil: 'load' });
  const buffer = await page.screenshot({ type: 'png', omitBackground: transparent, clip: { x: 0, y: 0, width, height } });
  const out = path.join(root, 'public', file);
  await mkdir(path.dirname(out), { recursive: true });
  await writeFile(out, buffer);
  if (magick && quantize) execFileSync('convert', [out, '-strip', '-dither', 'FloydSteinberg', '-colors', '256', `PNG8:${out}`]);
  return out;
}

for (const spec of specs) {
  await shoot(ogHtml(spec), spec.path.slice(1), { width: 1200, height: 630 });
}
await shoot(iconHtml(512), 'logo-512.png', { width: 512, height: 512, transparent: true, quantize: false });
await shoot(iconHtml(180, false), 'apple-touch-icon.png', { width: 180, height: 180, quantize: false });
await shoot(iconHtml(32), 'favicon-32.png', { width: 32, height: 32, transparent: true, quantize: false });

await browser.close();
console.log(`og-images: ${specs.length} Open Graph images and 3 icons in public/`);
