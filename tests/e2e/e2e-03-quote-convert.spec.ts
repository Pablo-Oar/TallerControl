import { test, expect } from '@playwright/test'
import { createCustomer, createVehicle, dialog, loginAsOwner, uid, uniquePlate } from './helpers'

/**
 * E2E-03: Presupuesto → aprobación → OT (doc 08 §3).
 *
 * Cubierto hoy: crear un presupuesto con dos ítems y enviarlo al cliente.
 * Pendiente de producto (ver test.fixme): el cliente no tiene cómo recibir el enlace público del
 * presupuesto (SendQuoteHandler solo guarda el hash del token y no lo devuelve; el detalle solo ofrece
 * el enlace de la OT) y la página pública aprueba o rechaza todo el presupuesto, sin aprobación por ítem.
 */

const E2E_RUN = process.env.E2E_RUN === '1'

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
    await d.getByRole('button', { name: /crear presupuesto/i }).click()
    await d.waitFor({ state: 'detached' })

    // Detalle del presupuesto recién creado
    await page.getByRole('listitem').filter({ hasText: customerName }).getByRole('link', { name: /^#\d+$/ }).click()
    await expect(page.getByText('Cambio de aceite')).toBeVisible()
    await expect(page.getByText('Filtro de aire')).toBeVisible()
    await expect(page.getByText(/8\.470/).first()).toBeVisible()

    // Enviar al cliente: pasa a "Enviado" y se puede descargar el PDF
    await page.getByRole('button', { name: /enviar al cliente/i }).click()
    await expect(page.getByText(/enviado/i).first()).toBeVisible()
    await expect(page.getByRole('link', { name: /^pdf$/i })).toBeVisible()
  })

  test.fixme('el cliente aprueba parcialmente por el enlace público y el dueño convierte a OT', async () => {
    // Sin UI/flujo: (1) no hay forma de obtener el enlace /p/{token} del presupuesto enviado;
    // (2) /p/{token} no permite aprobar ítems sueltos (solo "Apruebo"/"Rechazo" para todo el presupuesto).
    // Habilitar al resolver ambos puntos (REQ de aprobación parcial, T-QUO-*).
  })
})
