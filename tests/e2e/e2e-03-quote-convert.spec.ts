import { test, expect, type Page } from '@playwright/test'
import { createCustomer, createVehicle, dialog, loginAsOwner, uid, uniquePlate } from './helpers'

/**
 * E2E-03: Presupuesto → enlace público → aprobación parcial → OT (doc 08 §3, REQ-09..11).
 * El dueño envía el presupuesto y comparte el enlace /p/{token}; el cliente (sin cuenta) destilda el renglón
 * opcional y aprueba; el dueño convierte a OT y solo pasa lo aprobado.
 */

const E2E_RUN = process.env.E2E_RUN === '1'

/** Crea un presupuesto con dos ítems (5.000 + 2.000 neto); el segundo puede quedar como opcional. */
async function createQuote(page: Page, customerName: string, secondOptional: boolean) {
  await page.goto('/app/quotes')
  await page.getByRole('button', { name: /nuevo presupuesto/i }).first().click()
  const d = dialog(page)
  await d.getByRole('combobox', { name: /^cliente/i }).fill(customerName)
  await d.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()
  await d.getByLabel(/^veh[ií]culo/i).selectOption({ index: 1 })

  for (const [i, [description, price]] of [['Cambio de aceite', 5000], ['Filtro de aire', 2000]].entries()) {
    await d.getByRole('button', { name: /\+ agregar ítem/i }).click()
    await d.getByLabel(/^descripción/i).nth(i).fill(String(description))
    await d.getByLabel(/^precio neto/i).nth(i).fill(String(price))
  }
  if (secondOptional) await d.getByLabel(/requerido para aprobación/i).nth(1).uncheck()
  await d.getByRole('button', { name: /crear presupuesto/i }).click()
  await d.waitFor({ state: 'detached' })

  await page.getByRole('row').filter({ hasText: customerName }).getByRole('link', { name: /^ver presupuesto #\d+$/i }).click()
}

test.describe('E2E-03: Presupuesto → aprobación → OT', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  test('crear un presupuesto con dos ítems y enviarlo al cliente', async ({ page }) => {
    const suffix = uid()
    const customerName = `Presup E2E ${suffix}`
    const plate = uniquePlate('BB', suffix)

    await loginAsOwner(page)
    await createCustomer(page, customerName, `pres${suffix}@test.com`)
    await createVehicle(page, customerName, plate, { brand: 'Ford', model: 'Fiesta', year: 2019 })

    // Presupuesto con dos ítems (5.000 + 2.000 neto, IVA 21 % → 8.470)
    await createQuote(page, customerName, false)

    // Detalle del presupuesto recién creado
    await expect(page.getByText('Cambio de aceite')).toBeVisible()
    await expect(page.getByText('Filtro de aire')).toBeVisible()
    await expect(page.getByText(/8\.470/).first()).toBeVisible()

    // Enviar al cliente: pasa a "Enviado" y se puede descargar el PDF
    await page.getByRole('button', { name: /enviar al cliente/i }).click()
    await expect(page.getByText(/enviado/i).first()).toBeVisible()
    await expect(page.getByRole('link', { name: /^pdf$/i })).toBeVisible()
  })

  test('el cliente aprueba parcialmente por el enlace público y el dueño convierte a OT', async ({ page, browser }) => {
    const suffix = uid()
    const customerName = `Aprob E2E ${suffix}`
    const plate = uniquePlate('HH', suffix)

    await loginAsOwner(page)
    await createCustomer(page, customerName, `aprob${suffix}@test.com`)
    await createVehicle(page, customerName, plate, { brand: 'Chevrolet', model: 'Onix', year: 2021 })
    await createQuote(page, customerName, true) // el segundo renglón es opcional

    // Enviar: el panel entrega el enlace para el cliente (copiar / WhatsApp)
    await page.getByRole('button', { name: /enviar al cliente/i }).click()
    const link = page.getByLabel(/^enlace$/i)
    await expect(link).toBeVisible()
    const publicUrl = await link.inputValue()
    expect(publicUrl).toContain('/p/')
    await expect(page.getByRole('link', { name: /whatsapp/i })).toBeVisible()

    // El cliente (sin sesión, móvil) abre el enlace y destilda el renglón opcional
    const clientCtx = await browser.newContext({ viewport: { width: 390, height: 844 } })
    const client = await clientCtx.newPage()
    await client.goto(publicUrl)
    await expect(client.getByText('Cambio de aceite')).toBeVisible()
    await expect(client.getByText(/obligatorio/i)).toBeVisible() // el primero no se puede quitar
    await client.getByLabel(/incluir en la aprobación/i).uncheck()
    await client.getByLabel(/nombre completo/i).fill('Cliente Aprobador')
    await client.getByRole('button', { name: /enviar respuesta/i }).click()
    await expect(client.getByText(/respuesta enviada/i)).toBeVisible()
    await expect(client.getByText(/6\.050/)).toBeVisible() // total aprobado: 5.000 + IVA 21 %
    await clientCtx.close()

    // El dueño ve la aprobación parcial y convierte a OT: solo pasa el renglón aprobado
    await page.reload()
    await expect(page.getByText(/parcial/i).first()).toBeVisible()
    await page.getByRole('button', { name: /convertir a ot/i }).click()
    // Datos de ingreso: el kilometraje viene precargado con el del vehículo.
    await page.getByRole('dialog').getByRole('button', { name: /^crear orden$/i }).click()
    await page.waitForURL(/\/app\/work-orders\/[0-9a-f-]{36}/)
    await expect(page.getByText('Cambio de aceite')).toBeVisible()
    await expect(page.getByText('Filtro de aire')).toHaveCount(0)
  })
})
