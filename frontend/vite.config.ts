import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    // استضافة عامة اختيارية (نفق): تُحدَّد عبر VITE_ALLOWED_HOSTS (قائمة مفصولة بفواصل)
    // فلا يبقى فحص الـHost مفتوحًا افتراضيًا على جهاز المطوّر.
    allowedHosts: (process.env.VITE_ALLOWED_HOSTS ?? '')
      .split(',')
      .map((h) => h.trim())
      .filter(Boolean),
    proxy: {
      '/api': {
        target: process.env.VITE_API_TARGET || 'http://localhost:5199',
        changeOrigin: true,
      },
    },
  },
})
