import { test, expect } from '@playwright/test'
import { loginAsOwner, uid } from './helpers'

/**
 * E2E-05: Pago parcial → pago completo → entrega → enlace PDF (doc 08 §3).
 * El OT pasa por InProgress y Completed antes de los cobros para que la
 * transición a Delivered sea válida.
 */

const E2E_RUN = process.env.E2E_RUN === '1'

test.describe('E2E-05: Cobros y entrega', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  test('pago parcial → pago completo → entregar → PDF', async ({ page }) => {
    const suffix = uid()
    const customerName = `Cobro E2E ${suffix}`
    const plate = `DD${suffix.slice(0, 3)}EE`

    await loginAsOwner(page)

    // Crear cliente + vehículo + OT con un servicio
    await page.goto('/app/customers')
    await page.getByRole('button', { name: /nuevo cliente/i }).click()
    await page.getByLabel(/nombre/i).fill(customerName)
    await page.getByLabel(/email/i).fill(`cobro${suffix}@test.com`)
    await page.getByRole('button', { name: /guardar/i }).click()

    await page.goto('/app/vehicles')
    await page.getByRole('button', { name: /nuevo vehículo/i }).click()
    await page.getByRole('combobox', { name: /cliente/i }).fill(customerName.split(' ')[0])
    await page.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()
    await page.getByLabel(/patente/i).fill(plate)
    await page.getByLabel(/marca/i).fill('Renault')
    await page.getByLabel(/modelo/i).fill('Sandero')
    await page.getByLabel(/año/i).fill('2021')
    await page.getByRole('button', { name: /guardar/i }).click()

    await page.goto('/app/work-orders')
    await page.getByRole('button', { name: /nueva orden/i }).click()
    const clientInput = page.getByPlaceholder(/buscar cliente/i).first()
    await clientInput.fill(customerName.split(' ')[0])
    await page.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()
    const vehicleInput = page.getByPlaceholder(/buscar vehículo/i).first()
    await vehicleInput.fill(plate)
    await page.getByRole('option', { name: new RegExp(plate, 'i') }).first().click()

    // Agregar servicio con precio
    await page.getByRole('button', { name: /agregar ítem/i }).click()
    await page.getByPlaceholder(/descripción del servicio/i).last().fill('Alineación y balanceo')
    await page.getByLabel(/precio unitario/i).last().fill('10000')

    await page.getByRole('button', { name: /guardar/i }).click()

    // Abrir detalle de la OT
    await page.getByRole('link', { name: /ver detalle/i }).first().click()

    // Avanzar estado: Pending → InProgress → Completed
    await page.getByRole('button', { name: /cambiar estado/i }).click()
    await page.getByRole('option', { name: /en proceso/i }).click()
    await page.getByRole('button', { name: /confirmar/i }).click()
    await expect(page.getByText(/en proceso/i)).toBeVisible()

    await page.getByRole('button', { name: /cambiar estado/i }).click()
    await page.getByRole('option', { name: /completada/i }).click()
    await page.getByRole('button', { name: /confirmar/i }).click()
    await expect(page.getByText(/completada/i)).toBeVisible()

    // Registrar pago parcial (5000 de 10000)
    await page.getByRole('button', { name: /registrar cobro/i }).click()
    await page.getByLabel(/monto/i).fill('5000')
    await page.getByRole('button', { name: /guardar|confirmar/i }).click()
    await expect(page.getByText(/5\.000|5,000/)).toBeVisible()

    // Registrar segundo pago (los 5000 restantes)
    await page.getByRole('button', { name: /registrar cobro/i }).click()
    await page.getByLabel(/monto/i).fill('5000')
    await page.getByRole('button', { name: /guardar|confirmar/i }).click()

    // OT pagada completamente → entregar
    await page.getByRole('button', { name: /cambiar estado/i }).click()
    await page.getByRole('option', { name: /entregada/i }).click()
    await page.getByRole('button', { name: /confirmar/i }).click()
    await expect(page.getByText(/entregada/i)).toBeVisible()

    // Verificar que el enlace de PDF del recibo es accesible
    const pdfLinks = page.getByRole('link', { name: /pdf|recibo/i })
    await expect(pdfLinks.first()).toBeVisible()
  })
})
