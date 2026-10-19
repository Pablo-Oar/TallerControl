import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// Un solo origen: en desarrollo la SPA reenvía /api y /health a la API de ASP.NET Core.
const apiTarget = process.env.VITE_API_TARGET ?? 'http://localhost:5233'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  optimizeDeps: {
    // Scan lazy route areas so all their transitive deps are pre-bundled at startup,
    // preventing mid-render re-optimization that creates duplicate React instances.
    entries: [
      'src/main.tsx',
      'src/areas/taller/TallerArea.tsx',
      'src/areas/mecanico/MecanicoArea.tsx',
      'src/areas/publico/PublicoArea.tsx',
    ],
    include: [
      'react',
      'react-dom',
      'react-dom/client',
      'react-router',
      '@tanstack/react-query',
      'react-hook-form',
      'zod',
      '@hookform/resolvers/zod',
    ],
  },
  server: {
    host: true,
    allowedHosts: ['tallercontrol.com.ar'],
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