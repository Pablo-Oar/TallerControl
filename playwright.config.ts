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
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  // En CI el entorno ya está levantado por dev.ps1; localmente arrancar manualmente
})
