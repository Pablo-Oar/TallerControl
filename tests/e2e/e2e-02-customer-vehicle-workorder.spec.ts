import { test, expect } from '@playwright/test'
import { createCustomer, createVehicle, createWorkOrder, loginAsOwner, openWorkOrder, uid, uniquePlate, workOrderRow } from './helpers'

/**
 * E2E-02: Alta cliente → vehículo → OT → historial (doc 08 §3).
 * Flujo completo desde la UI: crear cliente, crear vehículo, crear OT y verificar
 * que el historial de estado inicial aparece en el detalle.
 */

const E2E_RUN = process.env.E2E_RUN === '1'

test.describe('E2E-02: Cliente → vehículo → OT → historial', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  test('crear cliente, vehículo y orden de trabajo', async ({ page }) => {
    const suffix = uid()
    const customerName = `Cliente E2E ${suffix}`
    const plate = uniquePlate('AA', suffix)

    await loginAsOwner(page)

    await createCustomer(page, customerName, `e2e${suffix}@test.com`)
    await expect(page.getByText(customerName).first()).toBeVisible()

    await createVehicle(page, customerName, plate, { brand: 'Toyota', model: 'Corolla', year: 2020 })
    await expect(page.getByText(plate).first()).toBeVisible()

    await createWorkOrder(page, customerName, plate)

    // La OT nueva queda Pendiente en la lista
    await expect(workOrderRow(page, plate).getByText(/pendiente/i)).toBeVisible()

    // Detalle → el historial de estados muestra la creación
    await openWorkOrder(page, plate)
    await expect(page.getByText(/historial de estados/i)).toBeVisible()
    await expect(page.getByText(/pendiente/i).first()).toBeVisible()
  })
})
