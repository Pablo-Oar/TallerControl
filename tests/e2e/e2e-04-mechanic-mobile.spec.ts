import { test, expect } from '@playwright/test'
import { loginAsOwner, loginAsMechanic, MECHANIC_EMAIL, uid } from './helpers'

/**
 * E2E-04: Portal del mecánico en viewport móvil → autoasignación → inicio →
 * foto adjunta → completar (doc 08 §3).
 * Requiere E2E_MECHANIC_EMAIL y E2E_MECHANIC_PASSWORD adicionales.
 */

const E2E_RUN = process.env.E2E_RUN === '1'
const HAS_MECHANIC = !!MECHANIC_EMAIL

test.describe('E2E-04: Mecánico móvil → asignación → completar', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')
  test.skip(!HAS_MECHANIC, 'Requiere E2E_MECHANIC_EMAIL y E2E_MECHANIC_PASSWORD')

  test('mecánico se autoasigna y completa OT desde móvil', async ({ browser }) => {
    const suffix = uid()
    const customerName = `Mec E2E ${suffix}`
    const plate = `CC${suffix.slice(0, 3)}DD`

    // Setup: crear OT como owner
    const ownerCtx = await browser.newContext()
    const ownerPage = await ownerCtx.newPage()
    await loginAsOwner(ownerPage)

    await ownerPage.goto('/app/customers')
    await ownerPage.getByRole('button', { name: /nuevo cliente/i }).click()
    await ownerPage.getByLabel(/nombre/i).fill(customerName)
    await ownerPage.getByLabel(/email/i).fill(`mec${suffix}@test.com`)
    await ownerPage.getByRole('button', { name: /guardar/i }).click()

    await ownerPage.goto('/app/vehicles')
    await ownerPage.getByRole('button', { name: /nuevo vehículo/i }).click()
    await ownerPage.getByRole('combobox', { name: /cliente/i }).fill(customerName.split(' ')[0])
    await ownerPage.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()
    await ownerPage.getByLabel(/patente/i).fill(plate)
    await ownerPage.getByLabel(/marca/i).fill('VW')
    await ownerPage.getByLabel(/modelo/i).fill('Gol')
    await ownerPage.getByLabel(/año/i).fill('2018')
    await ownerPage.getByRole('button', { name: /guardar/i }).click()

    await ownerPage.goto('/app/work-orders')
    await ownerPage.getByRole('button', { name: /nueva orden/i }).click()
    const clientInput = ownerPage.getByPlaceholder(/buscar cliente/i).first()
    await clientInput.fill(customerName.split(' ')[0])
    await ownerPage.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()
    const vehicleInput = ownerPage.getByPlaceholder(/buscar vehículo/i).first()
    await vehicleInput.fill(plate)
    await ownerPage.getByRole('option', { name: new RegExp(plate, 'i') }).first().click()
    await ownerPage.getByRole('button', { name: /guardar/i }).click()
    await ownerCtx.close()

    // Mecánico: viewport móvil
    const mechCtx = await browser.newContext({
      viewport: { width: 390, height: 844 }, // iPhone 14
    })
    const mechPage = await mechCtx.newPage()
    await loginAsMechanic(mechPage)

    // Verificar que la OT aparece en la lista del mecánico
    await expect(mechPage.getByText(plate)).toBeVisible()

    // Abrir detalle de la OT
    await mechPage.getByText(plate).click()

    // Autoasignarse
    await mechPage.getByRole('button', { name: /asignarme|tomar esta orden/i }).click()
    await expect(mechPage.getByText(/asignado|en progreso/i)).toBeVisible()

    // Iniciar trabajo
    await mechPage.getByRole('button', { name: /iniciar|comenzar/i }).click()
    await expect(mechPage.getByText(/en proceso/i)).toBeVisible()

    // Agregar nota de mecánico
    const notesArea = mechPage.getByPlaceholder(/notas del mecánico/i)
    await notesArea.fill(`Revisión completada - ${suffix}`)
    await mechPage.getByRole('button', { name: /guardar nota/i }).click()

    // Completar la OT
    await mechPage.getByRole('button', { name: /completar/i }).click()
    await mechPage.getByRole('button', { name: /confirmar|aceptar/i }).click()
    await expect(mechPage.getByText(/completada/i)).toBeVisible()

    await mechCtx.close()
  })
})
