import { test, expect } from '@playwright/test'
import {
  changeStatus,
  createCustomer,
  createVehicle,
  createWorkOrder,
  dialog,
  loginAsOwner,
  openWorkOrder,
  uid,
  uniquePlate,
} from './helpers'

/**
 * E2E-05: Cobros y entrega (doc 08 §3).
 * OT con un servicio de $ 10.000 neto (+ IVA 21 % = $ 12.100): pago parcial → pago del saldo →
 * entrega → comprobante PDF del cobro.
 */

const E2E_RUN = process.env.E2E_RUN === '1'

test.describe('E2E-05: Cobros y entrega', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  test('pago parcial → pago completo → entregar → PDF', async ({ page }) => {
    const suffix = uid()
    const customerName = `Cobro E2E ${suffix}`
    const plate = uniquePlate('DD', suffix)

    await loginAsOwner(page)
    await createCustomer(page, customerName, `cobro${suffix}@test.com`)
    await createVehicle(page, customerName, plate, { brand: 'Renault', model: 'Sandero', year: 2021 })
    await createWorkOrder(page, customerName, plate, { description: 'Alineación y balanceo', price: 10000 })
    await openWorkOrder(page, plate)

    // Pending → En proceso → Completada
    await changeStatus(page, /en proceso/i)
    await expect(page.getByText(/en proceso/i).first()).toBeVisible()
    await changeStatus(page, /completada/i)
    await expect(page.getByText(/completada/i).first()).toBeVisible()

    // Pago parcial: 5.000 de 12.100
    await page.getByRole('button', { name: /registrar cobro/i }).click()
    await dialog(page).getByLabel(/^monto/i).fill('5000')
    await dialog(page).getByRole('button', { name: /^registrar$/i }).click()
    await dialog(page).getByRole('button', { name: /^listo$/i }).click()
    await dialog(page).waitFor({ state: 'detached' })
    await expect(page.getByText(/7\.100/).first()).toBeVisible() // saldo pendiente

    // Segundo pago: el saldo restante
    await page.getByRole('button', { name: /registrar cobro/i }).click()
    await dialog(page).getByLabel(/^monto/i).fill('7100')
    await dialog(page).getByRole('button', { name: /^registrar$/i }).click()
    await dialog(page).getByRole('button', { name: /^listo$/i }).click()
    await dialog(page).waitFor({ state: 'detached' })

    // Saldo cubierto: ya no se ofrece registrar más cobros
    await expect(page.getByRole('button', { name: /registrar cobro/i })).toHaveCount(0)

    // Entregar
    await changeStatus(page, /entregada/i)
    await expect(page.getByText(/entregada/i).first()).toBeVisible()

    // Comprobantes PDF de ambos cobros
    await expect(page.getByRole('link', { name: /^pdf$/i })).toHaveCount(2)
  })
})
