import { type Page, type APIRequestContext } from '@playwright/test'

export const BASE_URL = process.env.E2E_BASE_URL ?? 'http://localhost:5173'
export const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5233'

export const OWNER_EMAIL = process.env.E2E_OWNER_EMAIL ?? 'owner@tallercontrol.dev'
export const OWNER_PASSWORD = process.env.E2E_OWNER_PASSWORD ?? 'TallerControl2026!'
export const MECHANIC_EMAIL = process.env.E2E_MECHANIC_EMAIL ?? ''
export const MECHANIC_PASSWORD = process.env.E2E_MECHANIC_PASSWORD ?? ''

/** Login via UI; returns after redirect to home page. */
export async function loginAsOwner(page: Page) {
  await page.goto('/login')
  await page.getByLabel(/email/i).fill(OWNER_EMAIL)
  await page.getByLabel(/contraseña/i).fill(OWNER_PASSWORD)
  await page.getByRole('button', { name: /ingresar/i }).click()
  await page.waitForURL('/app')
}

export async function loginAsMechanic(page: Page) {
  await page.goto('/login')
  await page.getByLabel(/email/i).fill(MECHANIC_EMAIL)
  await page.getByLabel(/contraseña/i).fill(MECHANIC_PASSWORD)
  await page.getByRole('button', { name: /ingresar/i }).click()
  await page.waitForURL('/m')
}

/** Login via API (request context). Returns the CSRF token for subsequent mutations. */
export async function apiLogin(
  request: APIRequestContext,
  email: string,
  password: string,
): Promise<string> {
  // Get CSRF token first
  const csrfResp = await request.get(`${API_URL}/api/v1/auth/me`, { failOnStatusCode: false })
  const csrfToken = csrfResp.headers()['x-csrf-token'] ?? ''

  const resp = await request.post(`${API_URL}/api/v1/auth/login`, {
    data: { email, password, rememberMe: false },
    headers: { 'X-CSRF-TOKEN': csrfToken, 'Content-Type': 'application/json' },
  })
  if (!resp.ok()) throw new Error(`Login failed: ${resp.status()}`)
  return csrfToken
}

/** Unique suffix for test data to avoid collisions between runs. */
export function uid() {
  return Date.now().toString(36).toUpperCase()
}
