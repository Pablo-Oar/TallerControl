import { test, expect } from '@playwright/test'
import { createCustomer, createVehicle, createWorkOrder, dialog, loginAsOwner, openWorkOrder, uid, uniquePlate } from './helpers'

/**
 * E2E-09: Editar una orden ya creada, con el PDF y el enlace para el cliente al alcance (Fase 2, O-01/O-02).
 * Una orden con un renglón se corrige (otro precio, otro texto, notas internas) sin recrearla.
 */

const E2E_RUN = process.env.E2E_RUN === '1'

test.describe('E2E-09: Editar una orden', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  test('corregir renglones y notas, ver el PDF y generar el enlace del cliente', async ({ page }) => {
    const suffix = uid()
    const customerName = `Edicion E2E ${suffix}`
    const plate = uniquePlate('EE', suffix)

    await loginAsOwner(page)
    await createCustomer(page, customerName, `edicion${suffix}@test.com`)
    await createVehicle(page, customerName, plate, { brand: 'Fiat', model: 'Cronos', year: 2022 })
    await createWorkOrder(page, customerName, plate, { description: 'Alineación', price: 1000 })
    await openWorkOrder(page, plate)
    await expect(page.getByText('Alineación')).toBeVisible()

    // El PDF y el enlace para el cliente están en el encabezado
    await expect(page.getByRole('link', { name: /^pdf$/i }).first()).toHaveAttribute('href', /\/api\/v1\/work-orders\/.+\/pdf/)
    await page.getByRole('button', { name: /enlace para el cliente/i }).click()
    await expect(dialog(page).getByLabel(/^enlace$/i)).toHaveValue(/\/o\/.+/)
    await dialog(page).getByRole('button', { name: /^cerrar$/i }).click()

    // Editar: cambia el texto y el precio del renglón, y deja una nota interna
    await page.getByRole('button', { name: /^editar$/i }).click()
    const d = dialog(page)
    await expect(d.getByText(/no se pueden cambiar/i)).toBeVisible()
    await d.getByLabel(/^descripción/i).fill('Alineación y balanceo')
    await d.getByLabel(/^precio neto/i).fill('2000')
    await d.getByLabel(/^notas internas/i).fill('Cliente pide factura A')
    await d.getByRole('button', { name: /guardar cambios/i }).click()
    await d.waitFor({ state: 'detached' })

    await expect(page.getByText('Alineación y balanceo')).toBeVisible()
    await expect(page.getByText(/2\.420/).first()).toBeVisible() // 2.000 + IVA 21 %
    await expect(page.getByText('Cliente pide factura A')).toBeVisible()
  })
})
