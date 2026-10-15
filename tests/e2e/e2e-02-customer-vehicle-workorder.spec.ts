import { test, expect } from '@playwright/test'
import { loginAsOwner, uid } from './helpers'

/**
 * E2E-02: Alta cliente → vehículo → OT → historial (doc 08 §3).
 * Flujo completo desde UI: crear cliente, crear vehículo, crear OT y verificar
 * que el historial de estado inicial aparece en el detalle.
 */

const E2E_RUN = process.env.E2E_RUN === '1'

test.describe('E2E-02: Cliente → vehículo → OT → historial', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  test('crear cliente, vehículo y orden de trabajo', async ({ page }) => {
    const suffix = uid()
    const customerName = `Cliente E2E ${suffix}`
    const plate = `AA${suffix.slice(0, 3)}BB`

    await loginAsOwner(page)

    // 1. Crear cliente
    await page.goto('/app/customers')
    await page.getByRole('button', { name: /nuevo cliente/i }).click()
    await page.getByLabel(/nombre/i).fill(customerName)
    await page.getByLabel(/email/i).fill(`e2e${suffix}@test.com`)
    await page.getByRole('button', { name: /guardar/i }).click()
    await expect(page.getByText(customerName)).toBeVisible()

    // 2. Crear vehículo para ese cliente
    await page.goto('/app/vehicles')
    await page.getByRole('button', { name: /nuevo vehículo/i }).click()

    // Seleccionar cliente en el combobox
    await page.getByRole('combobox', { name: /cliente/i }).fill(customerName.split(' ')[0])
    await page.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()

    await page.getByLabel(/patente/i).fill(plate)
    await page.getByLabel(/marca/i).fill('Toyota')
    await page.getByLabel(/modelo/i).fill('Corolla')
    await page.getByLabel(/año/i).fill('2020')
    await page.getByRole('button', { name: /guardar/i }).click()
    await expect(page.getByText(plate)).toBeVisible()

    // 3. Crear orden de trabajo
    await page.goto('/app/work-orders')
    await page.getByRole('button', { name: /nueva orden/i }).click()

    // Seleccionar cliente y vehículo
    const clientInput = page.getByPlaceholder(/buscar cliente/i).first()
    await clientInput.fill(customerName.split(' ')[0])
    await page.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()

    const vehicleInput = page.getByPlaceholder(/buscar vehículo/i).first()
    await vehicleInput.fill(plate)
    await page.getByRole('option', { name: new RegExp(plate, 'i') }).first().click()

    await page.getByRole('button', { name: /guardar/i }).click()

    // 4. Verificar que la OT aparece en la lista y tiene estado Pendiente
    await expect(page.getByText(/pendiente/i).first()).toBeVisible()

    // 5. Abrir detalle → historial de estados muestra la creación
    await page.getByRole('row').filter({ hasText: plate }).getByRole('link').first().click()
    await expect(page.getByText(/creada|pendiente/i).first()).toBeVisible()
  })
})
