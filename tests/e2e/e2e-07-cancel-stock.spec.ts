import { test, expect, type Page } from '@playwright/test'
import {
  adjustStock,
  changeStatus,
  createCatalogProduct,
  createCustomer,
  createVehicle,
  createWorkOrder,
  loginAsOwner,
  openWorkOrder,
  readReceivable,
  readStock,
  uid,
  uniquePlate,
} from './helpers'

/**
 * E2E-07: Cancelar una OT repone el stock y deja de contar como deuda (doc 08 §3, D-07).
 */

const E2E_RUN = process.env.E2E_RUN === '1'

/** Producto con stock 5 → OT con 2 unidades (stock 3) → OT cancelada. Devuelve los datos para verificar. */
async function cancelOrderWithProduct(page: Page) {
  const suffix = uid()
  const productName = `Filtro E2E ${suffix}`
  const customerName = `Cancel E2E ${suffix}`
  const plate = uniquePlate('FF', suffix)

  await loginAsOwner(page)

  // Producto con stock inicial 5 (el alta no pide stock: se carga con un ajuste)
  await createCatalogProduct(page, productName, 3000)
  await adjustStock(page, productName, 5)
  expect(await readStock(page, productName)).toBe(5)

  const receivableBefore = await readReceivable(page)

  await createCustomer(page, customerName, `cancel${suffix}@test.com`)
  await createVehicle(page, customerName, plate, { brand: 'Peugeot', model: '208', year: 2023 })

  // OT con 2 unidades: el stock baja a 3 y "Por cobrar" suma 2 × $ 3.000 + IVA 21 % = $ 7.260
  await createWorkOrder(page, customerName, plate, { catalogName: productName, quantity: 2 })
  expect(await readStock(page, productName)).toBe(3)
  expect(await readReceivable(page)).toBeCloseTo(receivableBefore + 7260, 2)

  await openWorkOrder(page, plate)
  await changeStatus(page, /cancelada/i, { cancelReason: 'Prueba E2E cancelación' })
  await expect(page.getByText(/cancelada/i).first()).toBeVisible()

  return { productName, receivableBefore }
}

test.describe('E2E-07: Cancelar OT', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  test('cancelar una OT con producto la excluye de "Por cobrar"', async ({ page }) => {
    const { receivableBefore } = await cancelOrderWithProduct(page)
    // Regla V2: "por cobrar" = saldo de las OT no canceladas
    expect(await readReceivable(page)).toBeCloseTo(receivableBefore, 2)
  })

  test('cancelar una OT con producto repone el stock', async ({ page }) => {
    // BUG conocido (hallado al actualizar los E2E): ChangeWorkOrderStatusHandler no genera el movimiento
    // StockMovementReason.WorkOrderCancel, así que el stock queda en 3. Cuando se corrija, este test pasará
    // "inesperadamente" y Playwright avisará: quitar el test.fail().
    test.fail(true, 'Cancelar una OT no repone el stock (WorkOrderCancel sin usar)')
    const { productName } = await cancelOrderWithProduct(page)
    expect(await readStock(page, productName)).toBe(5)
  })
})
