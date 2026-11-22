import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

/**
 * P6-05: تقرير حزمة محلي بلا اعتمادية إضافية — يجمع أحجام كل ملفات `dist`
 * في `bundle-report.json` ليتحقق منه `scripts/bundle-budget.mjs` في `CI`.
 */
function bundleReport(): Plugin {
  return {
    name: 'docgen-bundle-report',
    apply: 'build',
    generateBundle(_options, bundle) {
      const files = Object.values(bundle).map((item) => ({
        file: item.fileName,
        bytes: item.type === 'chunk' ? Buffer.byteLength(item.code, 'utf8') : item.source.length,
        type: item.type,
      }))
      this.emitFile({
        type: 'asset',
        fileName: 'bundle-report.json',
        source: JSON.stringify({ files }, null, 2),
      })
    },
  }
}

export default defineConfig({
  plugins: [react(), tailwindcss(), bundleReport()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_API_TARGET || 'http://localhost:5199',
        changeOrigin: true,
      },
    },
  },
})
