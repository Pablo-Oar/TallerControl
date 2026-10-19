import { defineConfig, devices } from '@playwright/test'

/**
 * Configuración de Playwright para tests E2E de TallerControl (doc 08 §3).
 * Requiere el entorno local levantado: API en :5233, SPA en :5173.
 */
export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 1 : undefined,
  // Los flujos E2E encadenan alta de cliente, vehículo, OT, etc.: 30 s por defecto no alcanzan
  timeout: 90_000,
  // 5 s (por defecto) se quedan cortos con el API recién iniciado o con la máquina cargada
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
    // E2E_CHANNEL=chrome usa el Chrome instalado (sin descargar el Chromium de Playwright)
    channel: process.env.E2E_CHANNEL || undefined,
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  // En CI el entorno ya está levantado por dev.ps1; localmente arrancar manualmente
})
