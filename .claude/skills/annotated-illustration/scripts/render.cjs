#!/usr/bin/env node
/**
 * Render an SVG to PNG so you can look at it before showing anyone.
 *
 * The linter checks geometry; only a rendered image shows whether the drawing
 * reads as the thing it is meant to be. Uses Playwright's Chromium, found
 * either as a local dependency or as a global install.
 *
 *   node render.cjs figure.svg            -> figure.png beside it
 *   node render.cjs figure.svg out.png
 *   node render.cjs figure.svg out.png --dark   (render with the dark palette)
 */
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

function loadPlaywright() {
  try { return require('playwright'); } catch { /* not local */ }
  try {
    const root = execSync('npm root -g', { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim();
    return require(path.join(root, 'playwright'));
  } catch { /* not global either */ }
  return null;
}

(async () => {
  const args = process.argv.slice(2);
  const dark = args.includes('--dark');
  const [svgPath, outArg] = args.filter((a) => a !== '--dark');
  if (!svgPath) {
    console.error('usage: node render.cjs figure.svg [out.png] [--dark]');
    process.exit(2);
  }
  const src = path.resolve(svgPath);
  const out = path.resolve(outArg || src.replace(/\.svg$/i, '') + '.png');

  // Size the viewport from the SVG itself so tall figures are not cropped.
  const head = fs.readFileSync(src, 'utf8').slice(0, 2000);
  const vb = /viewBox="\s*[\d.-]+[\s,]+[\d.-]+[\s,]+([\d.]+)[\s,]+([\d.]+)/.exec(head);
  const width = Math.ceil(Number(vb ? vb[1] : (/width="([\d.]+)/.exec(head) || [, 1200])[1]));
  const height = Math.ceil(Number(vb ? vb[2] : (/height="([\d.]+)/.exec(head) || [, 800])[1]));

  const pw = loadPlaywright();
  if (!pw) {
    console.error('Playwright is not installed. Install it (npm i -D playwright), or open the SVG in any browser instead.');
    process.exit(1);
  }
  const browser = await pw.chromium.launch();
  const page = await browser.newPage({ viewport: { width, height }, colorScheme: dark ? 'dark' : 'light' });
  await page.goto('file://' + src);
  await page.screenshot({ path: out });
  await browser.close();
  console.log(`${out} (${width}x${height}${dark ? ', dark' : ''})`);
})().catch((e) => { console.error(e.message || e); process.exit(1); });
