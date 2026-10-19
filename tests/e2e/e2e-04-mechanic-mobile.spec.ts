import { test, expect } from '@playwright/test'
import {
  createCustomer,
  createVehicle,
  createWorkOrder,
  loginAsMechanic,
  loginAsOwner,
  MECHANIC_EMAIL,
  uid,
  uniquePlate,
} from './helpers'

/**
 * E2E-04: Portal del mecánico en viewport móvil (doc 08 §3).
 * El mecánico ve las OT sin asignar, trabaja una (en proceso → nota → completada) y no ve precios.
 * Requiere E2E_MECHANIC_EMAIL y E2E_MECHANIC_PASSWORD.
 *
 * La foto adjunta desde /m no tiene pantalla todavía (ver test.fixme).
 */

const E2E_RUN = process.env.E2E_RUN === '1'
const HAS_MECHANIC = !!MECHANIC_EMAIL

test.describe('E2E-04: Mecánico móvil', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')
  test.skip(!HAS_MECHANIC, 'Requiere E2E_MECHANIC_EMAIL y E2E_MECHANIC_PASSWORD')

  test('el mecánico se asigna una OT y la trabaja: asignarme → en proceso → nota → completada (sin ver precios)', async ({ browser }) => {
    const suffix = uid()
    const customerName = `Mec E2E ${suffix}`
    const plate = uniquePlate('CC', suffix)

    // Preparación como dueño: cliente + vehículo + OT con un renglón con precio
    const ownerCtx = await browser.newContext()
    const ownerPage = await ownerCtx.newPage()
    await loginAsOwner(ownerPage)
    await createCustomer(ownerPage, customerName, `mec${suffix}@test.com`)
    await createVehicle(ownerPage, customerName, plate, { brand: 'VW', model: 'Gol', year: 2018 })
    await createWorkOrder(ownerPage, customerName, plate, { description: 'Revisión general', price: 8000 })
    await ownerCtx.close()

    // Mecánico en viewport móvil (iPhone 14)
    const mechCtx = await browser.newContext({ viewport: { width: 390, height: 844 } })
    const m = await mechCtx.newPage()
    await loginAsMechanic(m)

    // La OT sin asignar aparece en su lista
    const card = m.getByRole('link').filter({ hasText: plate })
    await expect(card).toBeVisible()
    await expect(card.getByText(/sin asignar/i)).toBeVisible()
    await card.click()

    // Detalle: ve el trabajo pero ningún monto (el rol no tiene prices.view)
    await expect(m.getByText('Revisión general')).toBeVisible()
    await expect(m.getByText(/\$\s*\d/)).toHaveCount(0)

    // Se asigna la orden sin asignar (doc 06: "Asignarme")
    await expect(m.getByText(/no tiene mecánico asignado/i)).toBeVisible()
    await m.getByRole('button', { name: /^asignarme$/i }).click()
    await expect(m.getByText(/no tiene mecánico asignado/i)).toHaveCount(0)

    // Iniciar el trabajo
    await m.getByRole('button', { name: /→ en proceso/i }).click()
    await m.getByRole('button', { name: /^confirmar$/i }).click()
    await expect(m.getByText(/en proceso/i).first()).toBeVisible()

    // Nota del mecánico
    const note = `Revisión completada - ${suffix}`
    await m.getByRole('button', { name: /^agregar$/i }).click()
    await m.getByLabel('Notas del trabajo').fill(note)
    await m.getByRole('button', { name: /^guardar$/i }).click()
    await expect(m.getByText(note)).toBeVisible()

    // Completar
    await m.getByRole('button', { name: /→ completada/i }).click()
    await m.getByRole('button', { name: /^confirmar$/i }).click()
    await expect(m.getByText(/completada/i).first()).toBeVisible()

    await mechCtx.close()
  })

  test('el dueño asigna un mecánico desde el detalle de la OT', async ({ page }) => {
    const suffix = uid()
    const customerName = `Asig E2E ${suffix}`
    const plate = uniquePlate('GG', suffix)

    await loginAsOwner(page)
    await createCustomer(page, customerName, `asig${suffix}@test.com`)
    await createVehicle(page, customerName, plate, { brand: 'Fiat', model: 'Cronos', year: 2022 })
    await createWorkOrder(page, customerName, plate)
    await openWorkOrder(page, plate)

    await expect(page.getByText(/sin asignar/i).first()).toBeVisible()
    await page.getByLabel(/^asignar a/i).selectOption({ index: 1 })
    await expect(page.getByText(/sin asignar/i)).toHaveCount(0)
    await expect(page.getByLabel(/^reasignar a/i)).toBeVisible()
  })

  test.fixme('el mecánico adjunta una foto desde el celular', async () => {
    // Sin UI: no hay carga de fotos en /m (doc 06: <input capture>). La API de adjuntos ya existe.
  })
})
