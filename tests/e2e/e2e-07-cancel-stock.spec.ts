import { test, expect } from '@playwright/test'
import { loginAsOwner, uid } from './helpers'

/**
 * E2E-07: OT con productos → cancelar → stock repuesto, balance 0 (doc 08 §3).
 * Verifica que al cancelar una OT con ítems de catálogo el stock se restaura
 * y el balance de la OT queda en 0.
 */

const E2E_RUN = process.env.E2E_RUN === '1'

test.describe('E2E-07: Cancelar OT repone stock y balance = 0', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  test('cancelar OT con producto repone stock y deja balance 0', async ({ page }) => {
    const suffix = uid()
    const productName = `Filtro E2E ${suffix}`
    const customerName = `Cancel E2E ${suffix}`
    const plate = `FF${suffix.slice(0, 3)}GG`

    await loginAsOwner(page)

    // 1. Crear producto con stock conocido
    await page.goto('/app/catalog')
    await page.getByRole('button', { name: /nuevo ítem/i }).click()
    await page.getByLabel(/nombre/i).fill(productName)

    // Seleccionar tipo "Repuesto"
    const kindSelect = page.getByLabel(/tipo/i)
    await kindSelect.selectOption({ label: /repuesto|spare_part/i })

    await page.getByLabel(/precio de venta/i).fill('3000')

    // Habilitar control de stock y poner stock inicial
    const trackStockCheck = page.getByLabel(/controlar stock/i)
    if (!(await trackStockCheck.isChecked())) {
      await trackStockCheck.check()
    }
    await page.getByLabel(/stock inicial/i).fill('5')

    await page.getByRole('button', { name: /guardar/i }).click()
    await expect(page.getByText(productName)).toBeVisible()

    // Verificar stock inicial = 5
    await expect(page.getByText('5').first()).toBeVisible()

    // 2. Crear cliente + vehículo
    await page.goto('/app/customers')
    await page.getByRole('button', { name: /nuevo cliente/i }).click()
    await page.getByLabel(/nombre/i).fill(customerName)
    await page.getByLabel(/email/i).fill(`cancel${suffix}@test.com`)
    await page.getByRole('button', { name: /guardar/i }).click()

    await page.goto('/app/vehicles')
    await page.getByRole('button', { name: /nuevo vehículo/i }).click()
    await page.getByRole('combobox', { name: /cliente/i }).fill(customerName.split(' ')[0])
    await page.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()
    await page.getByLabel(/patente/i).fill(plate)
    await page.getByLabel(/marca/i).fill('Peugeot')
    await page.getByLabel(/modelo/i).fill('208')
    await page.getByLabel(/año/i).fill('2023')
    await page.getByRole('button', { name: /guardar/i }).click()

    // 3. Crear OT con 2 unidades del producto
    await page.goto('/app/work-orders')
    await page.getByRole('button', { name: /nueva orden/i }).click()
    const clientInput = page.getByPlaceholder(/buscar cliente/i).first()
    await clientInput.fill(customerName.split(' ')[0])
    await page.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()
    const vehicleInput = page.getByPlaceholder(/buscar vehículo/i).first()
    await vehicleInput.fill(plate)
    await page.getByRole('option', { name: new RegExp(plate, 'i') }).first().click()

    // Agregar ítem del catálogo
    await page.getByRole('button', { name: /agregar del catálogo/i }).click()
    await page.getByPlaceholder(/buscar en catálogo/i).fill(productName.split(' ')[0])
    await page.getByRole('option', { name: new RegExp(productName, 'i') }).first().click()

    // Poner cantidad 2
    await page.getByLabel(/cantidad/i).last().fill('2')
    await page.getByRole('button', { name: /guardar/i }).click()

    // 4. Verificar stock bajó a 3
    await page.goto('/app/catalog')
    await page.getByRole('searchbox', { name: /buscar/i }).fill(productName.split(' ')[0])
    await expect(page.getByText('3')).toBeVisible()

    // 5. Cancelar la OT
    await page.goto('/app/work-orders')
    await page.getByRole('link', { name: /ver detalle/i }).first().click()
    await page.getByRole('button', { name: /cancelar orden/i }).click()
    await page.getByLabel(/motivo/i).fill('Prueba E2E cancelación')
    await page.getByRole('button', { name: /confirmar|cancelar/i }).last().click()
    await expect(page.getByText(/cancelada/i)).toBeVisible()

    // Verificar balance = 0 (o "$0,00")
    await expect(page.getByText(/\$\s*0|balance.*0/i)).toBeVisible()

    // 6. Verificar que el stock volvió a 5
    await page.goto('/app/catalog')
    await page.getByRole('searchbox', { name: /buscar/i }).fill(productName.split(' ')[0])
    await expect(page.getByText('5')).toBeVisible()
  })
})
