import { test, expect } from '@playwright/test'

/**
 * E2E-01: Alta del taller y primer usuario (doc 08 §3).
 * Flujo: invitación → definir contraseña → login → acceso al panel.
 *
 * Requiere E2E_RUN=1 y el entorno local levantado (dev.ps1). Los dos primeros tests además necesitan un
 * token de invitación válido en E2E_INVITATION_TOKEN (el token solo viaja por email/outbox: en local se
 * lee de `outbox_messages`, payload "Token"); el de credenciales incorrectas corre siempre.
 *
 * Nota: "configurar logo" (último paso del E2E-01) está pendiente hasta
 * que se implemente la pantalla de configuración del taller (E2 o posterior).
 */

const INVITATION_TOKEN = process.env.E2E_INVITATION_TOKEN ?? ''
const E2E_RUN = process.env.E2E_RUN === '1'
const E2E_PASSWORD = 'E2eTestPass123!'

test.describe('E2E-01: Alta del taller y primer usuario', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  test('invitación → contraseña → login → acceso al panel', async ({ page }) => {
    test.skip(!INVITATION_TOKEN, 'Se requiere E2E_INVITATION_TOKEN en el entorno')
    // 1. Abrir URL de invitación
    await page.goto(`/invitations/${INVITATION_TOKEN}/accept`)
    await expect(page.getByRole('heading', { name: /activar cuenta/i })).toBeVisible()

    // 2. Definir contraseña
    await page.getByLabel(/^contraseña$/i).fill(E2E_PASSWORD)
    await page.getByLabel(/confirmar contraseña/i).fill(E2E_PASSWORD)
    await page.getByRole('button', { name: /activar cuenta/i }).click()

    // 3. Tras activar la cuenta la sesión queda iniciada → redirige al panel
    await expect(page).toHaveURL('/app')

    // 4. El panel muestra el nombre del usuario (sesión activa)
    // La sesión se establece automáticamente al aceptar la invitación
    await expect(page.getByRole('navigation')).toBeVisible()
  })

  test('login con las credenciales recién creadas', async ({ page }) => {
    // Este test depende de que el anterior haya creado el usuario con E2E_PASSWORD
    const email = process.env.E2E_USER_EMAIL ?? ''
    test.skip(!email, 'Se requiere E2E_USER_EMAIL en el entorno')

    await page.goto('/login')
    await page.getByLabel(/email/i).fill(email)
    await page.getByLabel(/contraseña/i).fill(E2E_PASSWORD)
    await page.getByRole('button', { name: /ingresar/i }).click()

    await expect(page).toHaveURL('/app')
  })

  test('credenciales incorrectas muestran error (no 500)', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel(/email/i).fill('noexiste@example.com')
    await page.getByLabel(/contraseña/i).fill('ClaveIncorrecta99')
    await page.getByRole('button', { name: /ingresar/i }).click()

    await expect(page.getByRole('alert')).toContainText(/incorrectos/i)
    await expect(page).toHaveURL('/login')
  })
})
