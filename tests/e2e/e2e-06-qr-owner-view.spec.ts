import { test, expect } from '@playwright/test'
import { loginAsOwner, API_URL, uid } from './helpers'

/**
 * E2E-06: QR batch → asignación → /q/{code} → verificación Base32 → vista dueño (doc 08 §3).
 * Usa D-01 opción a: código Base32 de 8 chars del cliente como alternativa al OTP email.
 * El flujo público usa el campo "código de acceso" en lugar del email OTP.
 */

const E2E_RUN = process.env.E2E_RUN === '1'

test.describe('E2E-06: QR batch → verificación → vista del dueño', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  test('generar batch, asignar, escanear y ver historial del vehículo', async ({ page }) => {
    const suffix = uid()
    const customerName = `QR E2E ${suffix}`
    const plate = `EE${suffix.slice(0, 3)}FF`

    await loginAsOwner(page)

    // 1. Crear cliente + vehículo
    await page.goto('/app/customers')
    await page.getByRole('button', { name: /nuevo cliente/i }).click()
    await page.getByLabel(/nombre/i).fill(customerName)
    await page.getByLabel(/email/i).fill(`qr${suffix}@test.com`)
    await page.getByRole('button', { name: /guardar/i }).click()

    await page.goto('/app/vehicles')
    await page.getByRole('button', { name: /nuevo vehículo/i }).click()
    await page.getByRole('combobox', { name: /cliente/i }).fill(customerName.split(' ')[0])
    await page.getByRole('option', { name: new RegExp(customerName, 'i') }).first().click()
    await page.getByLabel(/patente/i).fill(plate)
    await page.getByLabel(/marca/i).fill('Honda')
    await page.getByLabel(/modelo/i).fill('Civic')
    await page.getByLabel(/año/i).fill('2022')
    await page.getByRole('button', { name: /guardar/i }).click()

    // 2. Generar un batch de tarjetas QR (1 tarjeta)
    await page.goto('/app/qr')
    await page.getByRole('button', { name: /generar tarjetas/i }).click()
    await page.getByLabel(/cantidad/i).fill('1')
    await page.getByRole('button', { name: /generar/i }).click()
    await expect(page.getByText(/generada|libre/i).first()).toBeVisible()

    // 3. Asignar la tarjeta al vehículo
    await page.getByRole('button', { name: /asignar/i }).first().click()

    const plateInput = page.getByLabel(/patente/i)
    await plateInput.fill(plate)

    // Obtener el código QR mostrado en la tabla (para usarlo en el paso 5)
    const qrCodeCell = page.getByRole('cell').filter({ hasText: /^[0-9A-Z]{10}$/ }).first()
    const qrCode = await qrCodeCell.textContent()

    await page.getByRole('button', { name: /confirmar|asignar/i }).click()
    await expect(page.getByText(/asignada/i).first()).toBeVisible()

    // Obtener código QR de la tarjeta ahora asignada
    const assignedCode = await page
      .getByRole('row')
      .filter({ hasText: plate })
      .getByRole('cell')
      .nth(1)
      .textContent()
    const code = (qrCode ?? assignedCode ?? '').trim()
    expect(code.length).toBeGreaterThan(0)

    // 4. Ir al portal público del QR como dueño del vehículo
    await page.goto(`/q/${code}`)
    await expect(page.getByText(/taller|verificar/i)).toBeVisible()

    // 5. Paso de verificación: placa + código de acceso (D-01 Base32)
    // El sistema muestra primero el resultado del escaneo, luego el formulario
    await expect(page.getByRole('button', { name: /continuar|verificar/i })).toBeVisible()
    await page.getByRole('button', { name: /continuar|verificar/i }).click()

    // Ingresar patente y seleccionar "código de acceso"
    await page.getByLabel(/patente/i).fill(plate)

    // Seleccionar método código de acceso si hay selector
    const accessCodeOption = page.getByRole('radio', { name: /código de acceso/i })
    if (await accessCodeOption.isVisible()) {
      await accessCodeOption.check()
    }

    await page.getByRole('button', { name: /continuar|enviar/i }).click()

    // 6. Ingresar el código Base32 del cliente
    // El backend aceptará cualquier valor como "otp" para el challenge type=access_code
    // En el entorno de prueba se puede obtener el hash via API o usar el código del seeder
    // Por ahora verificamos que la pantalla de código se muestre
    await expect(page.getByLabel(/código/i)).toBeVisible()

    // Nota: en un entorno E2E real se obtendría el código Base32 via el API de /api/v1/qr-cards
    // y se ingresaría aquí. Por ahora verificamos que el flujo llega hasta este punto.
    const accessCodeInput = page.getByLabel(/código/i)
    const testCode = process.env.E2E_QR_ACCESS_CODE ?? ''
    test.skip(!testCode, 'Requiere E2E_QR_ACCESS_CODE para completar la verificación Base32')

    await accessCodeInput.fill(testCode)
    await page.getByRole('button', { name: /acceder|verificar/i }).click()

    // 7. Vista del dueño: historial del vehículo
    await expect(page).toHaveURL(new RegExp(`/q/${code}/mi-vehiculo`))
    await expect(page.getByText(new RegExp(plate, 'i'))).toBeVisible()
  })
})
