import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// Un solo origen: en desarrollo la SPA reenvía /api y /health a la API de ASP.NET Core.
const apiTarget = process.env.VITE_API_TARGET ?? 'http://localhost:5233'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': apiTarget,
      '/health': apiTarget,
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // La cadena lazy / → /app → /login carga más módulos con cada etapa;
    // 15 s evita falsos negativos sin ser exageradamente permisivo.
    testTimeout: 15000,
  },
})