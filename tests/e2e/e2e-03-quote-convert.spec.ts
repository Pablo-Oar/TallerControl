import { test, expect, type Browser } from '@playwright/test'
import { loginAsOwner, OWNER_EMAIL, OWNER_PASSWORD, uid } from './helpers'

/**
 * E2E-03: Presupuesto → aprobación parcial del cliente → conversión a OT (doc 08 §3).
 * Dos contextos de navegador: owner crea y envía el presupuesto, cliente abre
 * el enlace público y aprueba parcialmente, owner convierte a OT.
 */

const E2E_RUN = process.env.E2E_RUN === '1'

test.describe('E2E-03: Presupuesto → aprobación parcial → OT', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  test('presupuesto parcialmente aprobado se convierte en OT', async ({ browser }: { browser: Browser }) => {
    const suffix = uid()

    // Contexto del owner
    const ownerCtx = await browser.newContext()
    const ownerPage = await ownerCtx.newPage()
    await loginAsOwner(ownerPage)

    // 1. Crear cliente y vehículo vía UI (reutiliza el flujo de E2E-02)
    const customerName = `Presup E2E ${suffix}`
    const plate = `BB${suffix.slice(0, 3)}CC`

    await ownerPage.goto('/app/customers')
    await ownerPage.getByRole('button', { name: /nuevo cliente/i }).click()
    await ownerPage.getByLabel(/nombre/i).fill(customerName)
    await ownerPage.getByLabel(/email/i).fill(`pres${suffix}@test.com`)
    await ownerPage.getByRole('button', { name: /guardar/i }).click()
    await expect(ownerPage.getByText(customerName)).toBeVisible()

    await ownerPage.goto('/app/vehicles')
    await ownerPage.getByRole('button', { name: /nuevo vehículo/i }).click()
    await ownerPage.getByRole('combobox', { name: /cliente/i }).fill(customerName.split(' ')[0])
    await ownerPage.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()
    await ownerPage.getByLabel(/patente/i).fill(plate)
    await ownerPage.getByLabel(/marca/i).fill('Ford')
    await ownerPage.getByLabel(/modelo/i).fill('Fiesta')
    await ownerPage.getByLabel(/año/i).fill('2019')
    await ownerPage.getByRole('button', { name: /guardar/i }).click()

    // 2. Crear presupuesto con dos ítems
    await ownerPage.goto('/app/quotes')
    await ownerPage.getByRole('button', { name: /nuevo presupuesto/i }).click()

    const clientInput = ownerPage.getByPlaceholder(/buscar cliente/i).first()
    await clientInput.fill(customerName.split(' ')[0])
    await ownerPage.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()

    const vehicleInput = ownerPage.getByPlaceholder(/buscar vehículo/i).first()
    await vehicleInput.fill(plate)
    await ownerPage.getByRole('option', { name: new RegExp(plate, 'i') }).first().click()

    // Agregar ítem 1
    await ownerPage.getByRole('button', { name: /agregar ítem/i }).click()
    await ownerPage.getByPlaceholder(/descripción del servicio/i).last().fill('Cambio de aceite')
    await ownerPage.getByLabel(/precio unitario/i).last().fill('5000')

    // Agregar ítem 2
    await ownerPage.getByRole('button', { name: /agregar ítem/i }).click()
    await ownerPage.getByPlaceholder(/descripción del servicio/i).last().fill('Filtro de aire')
    await ownerPage.getByLabel(/precio unitario/i).last().fill('2000')

    await ownerPage.getByRole('button', { name: /guardar/i }).click()

    // 3. Enviar el presupuesto
    await ownerPage.getByRole('link', { name: /ver detalle/i }).first().click()
    await ownerPage.getByRole('button', { name: /enviar al cliente/i }).click()
    await expect(ownerPage.getByText(/enviado/i)).toBeVisible()

    // 4. Obtener el enlace público del presupuesto
    await ownerPage.getByRole('button', { name: /enlace público/i }).click()
    const publicLinkInput = ownerPage.getByRole('textbox', { name: /enlace/i })
    const publicUrl = await publicLinkInput.inputValue()
    expect(publicUrl).toContain('/p/')

    await ownerCtx.close()

    // 5. Contexto del cliente: abrir el enlace público
    const clientCtx = await browser.newContext()
    const clientPage = await clientCtx.newPage()
    await clientPage.goto(publicUrl)
    await expect(clientPage.getByText(/presupuesto/i)).toBeVisible()

    // Aprobar solo el primer ítem (aprobación parcial)
    const checkboxes = clientPage.getByRole('checkbox')
    const count = await checkboxes.count()
    if (count >= 2) {
      await checkboxes.first().check()
      // Dejar el segundo sin marcar (aprobación parcial)
    }

    await clientPage.getByRole('button', { name: /enviar respuesta|aprobar/i }).click()
    await expect(clientPage.getByText(/respuesta enviada|gracias/i)).toBeVisible()
    await clientCtx.close()

    // 6. Volver como owner: convertir presupuesto parcialmente aprobado a OT
    const ownerCtx2 = await browser.newContext()
    const ownerPage2 = await ownerCtx2.newPage()
    await loginAsOwner(ownerPage2)
    await ownerPage2.goto('/app/quotes')

    await ownerPage2.getByRole('link', { name: /ver detalle/i }).first().click()
    await ownerPage2.getByRole('button', { name: /convertir a orden/i }).click()
    await ownerPage2.getByRole('button', { name: /confirmar|aceptar/i }).click()

    await expect(ownerPage2.getByText(/convertido|orden de trabajo/i)).toBeVisible()
    await ownerCtx2.close()
  })
})
