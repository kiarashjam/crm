// Builds src/entry-prerender.tsx for Node with Vite's SSR mode and imports it.
// Shared by the prerender (every build) and the OG image generator (run by hand).

import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { build } from 'vite';

export const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

export async function loadRenderer() {
  const outDir = path.join(root, 'node_modules/.cache/cadencee-prerender');
  await build({
    root,
    configFile: path.join(root, 'vite.config.ts'),
    logLevel: 'warn',
    build: {
      ssr: 'src/entry-prerender.tsx',
      outDir,
      emptyOutDir: true,
      manifest: false,
      ssrManifest: false,
      copyPublicDir: false,
      minify: false,
      rollupOptions: { output: { entryFileNames: 'entry-prerender.mjs' } },
    },
  });
  return import(pathToFileURL(path.join(outDir, 'entry-prerender.mjs')).href);
}
