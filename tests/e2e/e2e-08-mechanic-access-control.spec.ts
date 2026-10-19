import { test, expect } from '@playwright/test'
import { loginAsMechanic, MECHANIC_EMAIL } from './helpers'

/**
 * E2E-08: Mecánico no tiene menú de Pagos, y si fuerza /app/* es redirigido a /m (doc 08 §3).
 * Verifica que el rol Mechanic no puede acceder al área del panel del taller.
 */

const E2E_RUN = process.env.E2E_RUN === '1'
const HAS_MECHANIC = !!MECHANIC_EMAIL

test.describe('E2E-08: Control de acceso del mecánico', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')
  test.skip(!HAS_MECHANIC, 'Requiere E2E_MECHANIC_EMAIL y E2E_MECHANIC_PASSWORD')

  test('mecánico no ve enlace de Pagos en su área', async ({ page }) => {
    await loginAsMechanic(page)

    // El área /m no tiene menú de panel: aterriza en /m y no expone ningún enlace a Pagos/Cobros.
    await expect(page).toHaveURL(/\/m(\/|$)/)
    await expect(page.getByRole('link', { name: /pagos|cobros/i })).toHaveCount(0)
  })

  test('mecánico que fuerza /app/* es redirigido al área de mecánico', async ({ page }) => {
    await loginAsMechanic(page)

    // Intentar navegar a una ruta del panel del taller
    await page.goto('/app/work-orders')

    // Debe redirigir al área de mecánico (/m)
    await expect(page).toHaveURL(/\/m(\/|$)/)
  })

  test('mecánico que intenta /app/quotes es redirigido a /m', async ({ page }) => {
    await loginAsMechanic(page)
    await page.goto('/app/quotes')
    await expect(page).toHaveURL(/\/m(\/|$)/)
  })

  test('mecánico que intenta /app/catalog es redirigido a /m', async ({ page }) => {
    await loginAsMechanic(page)
    await page.goto('/app/catalog')
    await expect(page).toHaveURL(/\/m(\/|$)/)
  })
})
