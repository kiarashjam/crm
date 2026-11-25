import { defineConfig, loadEnv, type Plugin } from 'vite'
import path from 'path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { LAUNCH_OFFER_ENDS_AT_MS } from './src/app/lib/launchOffer'

/**
 * What index.html needs from the build: the launch offer's end (one source of truth,
 * src/app/lib/launchOffer.ts) and the search engines' site-verification tags, from
 * VITE_GOOGLE_SITE_VERIFICATION and VITE_BING_SITE_VERIFICATION when they are set.
 */
function siteHtml(env: Record<string, string>): Plugin {
  const escape = (value: string) => value.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;')
  return {
    name: 'cadencee-site-html',
    transformIndexHtml: {
      order: 'pre',
      handler(html) {
        const tags: string[] = []
        const google = (env.VITE_GOOGLE_SITE_VERIFICATION ?? '').trim()
        const bing = (env.VITE_BING_SITE_VERIFICATION ?? '').trim()
        if (google) tags.push(`<meta name="google-site-verification" content="${escape(google)}" />`)
        if (bing) tags.push(`<meta name="msvalidate.01" content="${escape(bing)}" />`)
        const withEnd = html.replace('__LAUNCH_OFFER_ENDS_AT_MS__', String(LAUNCH_OFFER_ENDS_AT_MS))
        return tags.length ? withEnd.replace('<!-- seo:head -->', `${tags.join('\n    ')}\n    <!-- seo:head -->`) : withEnd
      },
    },
  }
}

export default defineConfig(({ mode }) => {
  // .env files and the VITE_* variables of the environment (CI sets them in deploy.yml).
  const env = loadEnv(mode, process.cwd(), 'VITE_')
  return {
    plugins: [
      // The React and Tailwind plugins are both required for Make, even if
      // Tailwind is not being actively used – do not remove them
      react(),
      tailwindcss(),
      siteHtml(env),
    ],
    resolve: {
      alias: {
        // Alias @ to the src directory
        '@': path.resolve(__dirname, './src'),
      },
    },
    build: {
      // The prerender (scripts/seo/prerender.mjs) reads which chunks each page needs.
      manifest: true,
      rollupOptions: {
        output: {
          // Icons and helpers shared by two lazy pages otherwise each get a chunk of a few
          // hundred bytes, and a page waits for dozens of them; small chunks are merged.
          experimentalMinChunkSize: 10_000,
        },
      },
    },
    server: {
      proxy: {
        // Dev proxy to the .NET API (launchSettings default http:5160)
        '/api': {
          target: 'http://localhost:5160',
          changeOrigin: true,
          secure: false,
        },
      },
    },
  }
})

