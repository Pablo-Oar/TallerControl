import { test, expect } from '@playwright/test'
import { createCustomer, createVehicle, dialog, loginAsOwner, uid, uniquePlate } from './helpers'

/**
 * E2E-06: QR → lote → asignación → portal público del dueño (doc 08 §3, D-01).
 * La patente nunca alcanza: el portal exige además un código (OTP por email o código de acceso del cliente).
 * El último paso (ver el historial) necesita ese código; se ejecuta solo si E2E_QR_ACCESS_CODE está definido.
 */

const E2E_RUN = process.env.E2E_RUN === '1'

test.describe('E2E-06: QR batch → verificación → vista del dueño', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  test('generar lote, asignar al vehículo y verificar identidad en el portal público', async ({ page, browser }) => {
    const suffix = uid()
    const customerName = `QR E2E ${suffix}`
    const plate = uniquePlate('EE', suffix)

    await loginAsOwner(page)
    await createCustomer(page, customerName, `qr${suffix}@test.com`)
    await createVehicle(page, customerName, plate, { brand: 'Honda', model: 'Civic', year: 2022 })

    // 1. Generar un lote de 1 tarjeta: el código nuevo es el que no estaba antes en la lista
    await page.goto('/app/qr')
    // El orden de la tabla no es cronológico: la tarjeta nueva es la que antes no estaba entre las libres
    const freeCodes = async () => {
      const resp = await page.request.get('/api/v1/qr/cards?status=free&pageSize=200')
      expect(resp.ok()).toBeTruthy()
      return new Set<string>(((await resp.json()).items as { code: string }[]).map(c => c.code))
    }
    const before = await freeCodes()
    await expect(page.getByRole('button', { name: /generar lote/i })).toBeVisible()
    await page.getByRole('button', { name: /generar lote/i }).click()
    await dialog(page).getByLabel(/^cantidad/i).fill('1')
    await dialog(page).getByRole('button', { name: /^generar$/i }).click()
    await dialog(page).waitFor({ state: 'detached' })
    await expect.poll(async () => [...(await freeCodes())].filter(c => !before.has(c)).length).toBe(1)
    const code = [...(await freeCodes())].find(c => !before.has(c))!

    // 2. Asignarla por código al vehículo (selector de vehículo, sin pegar UUIDs)
    await page.getByRole('button', { name: /asignar por código/i }).click()
    await dialog(page).getByLabel(/código de la tarjeta/i).fill(code)
    await dialog(page).getByRole('combobox', { name: /^veh[ií]culo/i }).fill(plate)
    await dialog(page).getByRole('option', { name: new RegExp(plate) }).click()
    await dialog(page).getByRole('button', { name: /^asignar$/i }).click()
    await dialog(page).waitFor({ state: 'detached' })
    await expect(page.getByRole('row').filter({ hasText: code })).toContainText(/asignada/i)
    await expect(page.getByRole('row').filter({ hasText: code })).toContainText(plate)

    // 3. Portal público como dueño del vehículo (sin sesión)
    const ownerCtx = await browser.newContext({ viewport: { width: 390, height: 844 } })
    const pub = await ownerCtx.newPage()
    await pub.goto(`/q/${code}`)
    await expect(pub.getByText(/portal del dueño/i)).toBeVisible()

    // La patente por sí sola no habilita nada: tras enviarla se pide un código de verificación
    await pub.getByLabel(/patente del veh/i).fill(plate)
    await pub.getByRole('button', { name: /verificar identidad/i }).click()
    await expect(pub.getByLabel(/código de verificación/i)).toBeVisible()

    // 4. Con el código de acceso del cliente se entra al historial del vehículo
    const accessCode = process.env.E2E_QR_ACCESS_CODE
    if (accessCode) {
      await pub.getByLabel(/código de verificación/i).fill(accessCode)
      await pub.getByRole('button', { name: /^acceder$/i }).click()
      await expect(pub).toHaveURL(new RegExp(`/q/${code}/mi-vehiculo`))
      await expect(pub.getByText(plate)).toBeVisible()
    }
    await ownerCtx.close()
  })
})
