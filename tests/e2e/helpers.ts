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
  await page.getByLabel(/^contraseña$/i).fill(OWNER_PASSWORD)
  await page.getByRole('button', { name: /ingresar/i }).click()
  await page.waitForURL('/app')
}

export async function loginAsMechanic(page: Page) {
  await page.goto('/login')
  await page.getByLabel(/email/i).fill(MECHANIC_EMAIL)
  await page.getByLabel(/^contraseña$/i).fill(MECHANIC_PASSWORD)
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

/** Patente única por corrida: los primeros caracteres de Date.now() en base36 casi no cambian, usar los últimos. */
export function uniquePlate(prefix: string, suffix: string) {
  return `${prefix}${suffix.slice(-3)}ZZ`
}

// ── Flujos de UI compartidos (selectores reales de la SPA rediseñada) ─────────

export const dialog = (page: Page) => page.getByRole('dialog')

/** Cliente nuevo desde /app/customers. */
export async function createCustomer(page: Page, name: string, email?: string) {
  await page.goto('/app/customers')
  await page.getByRole('button', { name: /nuevo cliente/i }).first().click()
  await dialog(page).getByLabel(/^nombre/i).fill(name)
  if (email) await dialog(page).getByLabel(/^email/i).fill(email)
  await dialog(page).getByRole('button', { name: /^guardar$/i }).click()
  await dialog(page).waitFor({ state: 'detached' })
}

/** Vehículo nuevo para un cliente existente (el cliente es un select). */
export async function createVehicle(
  page: Page,
  customerName: string,
  plate: string,
  opts: { brand?: string; model?: string; year?: number } = {},
) {
  await page.goto('/app/vehicles')
  await page.getByRole('button', { name: /nuevo veh[ií]culo/i }).first().click()
  const d = dialog(page)
  await d.getByRole('combobox', { name: /^cliente/i }).fill(customerName)
  await d.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()
  await d.getByLabel(/^patente/i).fill(plate)
  if (opts.brand) await d.getByLabel(/^marca/i).fill(opts.brand)
  if (opts.model) await d.getByLabel(/^modelo/i).fill(opts.model)
  if (opts.year) await d.getByLabel(/^año/i).fill(String(opts.year))
  await d.getByRole('button', { name: /^guardar$/i }).click()
  await d.waitFor({ state: 'detached' })
}

/**
 * Orden de trabajo nueva; opcionalmente con un renglón: manual (descripción + precio neto) o de catálogo
 * (nombre del ítem), con cantidad.
 */
export async function createWorkOrder(
  page: Page,
  customerName: string,
  plate: string,
  item?: { description?: string; price?: number; catalogName?: string; quantity?: number },
) {
  await page.goto('/app/work-orders')
  await page.getByRole('button', { name: /nueva orden/i }).first().click()
  const d = dialog(page)
  await d.getByRole('combobox', { name: /^cliente/i }).fill(customerName)
  await d.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()
  await d.getByLabel(/^veh[ií]culo/i).selectOption({ index: 1 })
  if (item) {
    await d.getByRole('button', { name: /\+ agregar/i }).click()
    if (item.catalogName) {
      await d.getByRole('combobox', { name: /^ítem de catálogo/i }).fill(item.catalogName)
      await d.getByRole('option', { name: new RegExp(item.catalogName, 'i') }).first().click()
    }
    if (item.description) await d.getByLabel(/^descripción/i).fill(item.description)
    if (item.price != null) await d.getByLabel(/^precio neto/i).fill(String(item.price))
    if (item.quantity != null) await d.getByLabel(/^cantidad/i).fill(String(item.quantity))
  }
  await d.getByRole('button', { name: /crear orden/i }).click()
  await d.waitFor({ state: 'detached' })
}

/** Fila (li) de la lista de órdenes que contiene la patente. */
export const workOrderRow = (page: Page, plate: string) => page.getByRole('row').filter({ hasText: plate })

/** Abre el detalle de la OT de una patente desde la lista. */
export async function openWorkOrder(page: Page, plate: string) {
  await page.goto('/app/work-orders')
  await workOrderRow(page, plate).getByRole('link', { name: /ver orden/i }).click()
  await page.waitForURL(/\/app\/work-orders\/[0-9a-f-]+$/)
}

/** Cambia el estado de la OT abierta; extra = motivo de cancelación si corresponde. */
export async function changeStatus(page: Page, label: RegExp, extra?: { cancelReason?: string }) {
  await page.getByRole('button', { name: /cambiar estado/i }).click()
  const d = dialog(page)
  const select = d.getByLabel(/^nuevo estado/i)
  const options = await select.locator('option').allTextContents()
  const match = options.find(o => label.test(o))
  if (!match) throw new Error(`Estado ${label} no disponible: ${options.join(', ')}`)
  await select.selectOption({ label: match })
  if (extra?.cancelReason) await d.getByLabel(/motivo de cancelación/i).fill(extra.cancelReason)
  // La página se refresca en varias consultas (orden, historial, cobros): el siguiente cambio de estado arma sus
  // opciones con la orden, así que se espera a que ESA consulta vuelva con el estado nuevo.
  const orderRefreshed = page.waitForResponse(
    r => r.request().method() === 'GET' && /\/api\/v1\/work-orders\/[0-9a-f-]+$/.test(r.url()),
  )
  await d.getByRole('button', { name: /confirmar/i }).click()
  await d.waitFor({ state: 'detached' })
  await orderRefreshed
}

/** Producto con control de stock desde /app/catalog. */
export async function createCatalogProduct(page: Page, name: string, netPrice: number) {
  await page.goto('/app/catalog')
  await page.getByRole('button', { name: /nuevo ítem/i }).first().click()
  const d = dialog(page)
  await d.getByLabel(/^tipo/i).selectOption({ label: 'Producto' })
  await d.getByLabel(/^nombre/i).fill(name)
  await d.getByLabel(/^precio neto/i).fill(String(netPrice))
  await d.getByLabel(/controlar stock/i).check()
  await d.getByRole('button', { name: /^guardar$/i }).click()
  await d.waitFor({ state: 'detached' })
}

/** Abre el modal "Ajuste de stock" del ítem (busca por nombre para que quede en la primera página). */
async function openStockModal(page: Page, name: string) {
  await page.goto('/app/catalog')
  await page.getByPlaceholder(/buscar por nombre/i).fill(name)
  await page.getByRole('button', { name: /^buscar$/i }).click()
  await page.getByRole('listitem').filter({ hasText: name }).getByRole('button', { name: /stock/i }).click()
}

/** Suma (o resta, con negativo) stock con una nota. */
export async function adjustStock(page: Page, name: string, quantity: number) {
  await openStockModal(page, name)
  const d = dialog(page)
  await d.getByLabel(/^cantidad/i).fill(String(quantity))
  await d.getByLabel(/^nota/i).fill('Ajuste E2E')
  await d.getByRole('button', { name: /^registrar$/i }).click()
  await d.waitFor({ state: 'detached' })
}

/** Lee "Stock actual" del modal de ajuste y lo cierra sin cambios. */
export async function readStock(page: Page, name: string): Promise<number> {
  await openStockModal(page, name)
  const d = dialog(page)
  const text = (await d.getByText(/stock actual/i).first().innerText()).replace(/\s+/g, ' ')
  await d.getByRole('button', { name: /^cancelar$/i }).click()
  await d.waitFor({ state: 'detached' })
  const m = text.match(/stock actual:?\s*(-?[\d.,]+)/i)
  if (!m) throw new Error(`No se pudo leer el stock: ${text}`)
  return Number(m[1].replace(/\./g, '').replace(',', '.'))
}

/** "Por cobrar" del Inicio (suma de saldos de OT no canceladas, definición única V2). */
export async function readReceivable(page: Page): Promise<number> {
  await page.goto('/app')
  const label = page.getByText('Por cobrar', { exact: true })
  await label.waitFor()
  const text = (await label.locator('xpath=..').innerText()).replace(/\s+/g, ' ')
  const m = text.match(/\$\s*([\d.]+)(?:,(\d+))?/)
  if (!m) throw new Error(`No se pudo leer "Por cobrar": ${text}`)
  return Number(m[1].replace(/\./g, '') + '.' + (m[2] ?? '0'))
}

/** Fila del cliente en /app/customers: la lista está paginada, así que se busca por nombre. */
export async function customerRow(page: Page, name: string) {
  await page.goto('/app/customers')
  await page.getByPlaceholder(/buscar por nombre/i).fill(name)
  await page.getByRole('button', { name: /^buscar$/i }).click()
  return page.getByRole('listitem').filter({ hasText: name })
}
