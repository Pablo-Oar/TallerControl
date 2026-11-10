import { test, expect } from '@playwright/test'
import AxeBuilder from '@axe-core/playwright'
import { loginAsOwner } from './helpers'

/**
 * E2E-10: auditoría automática de accesibilidad (axe, WCAG 2 A/AA) de las pantallas principales.
 * Falla ante violaciones `critical` o `serious`; las demás se listan en el reporte (ver el adjunto del test).
 */

const E2E_RUN = process.env.E2E_RUN === '1'

const PUBLIC_PAGES = ['/', '/login', '/registro']
const APP_PAGES = ['/app', '/app/work-orders', '/app/customers', '/app/quotes', '/app/catalog', '/app/cash', '/app/reports']

test.describe('E2E-10: Accesibilidad (axe)', () => {
  test.skip(!E2E_RUN, 'Requiere E2E_RUN=1 y entorno local levantado')

  async function scan(page: import('@playwright/test').Page, path: string) {
    await page.goto(path)
    await page.waitForLoadState('networkidle')
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze()
    const blocking = results.violations.filter((v) => v.impact === 'critical' || v.impact === 'serious')
    const summary = results.violations.map((v) => `${v.impact} ${v.id} (${v.nodes.length})`).join('\n')
    await test.info().attach(`axe ${path}`, { body: summary || 'sin violaciones', contentType: 'text/plain' })
    expect(blocking.map((v) => `${v.id}: ${v.nodes.length} elementos`), `${path}\n${summary}`).toEqual([])
  }

  for (const path of PUBLIC_PAGES) {
    test(`pantalla pública ${path}`, async ({ page }) => {
      await scan(page, path)
    })
  }

  test('pantallas del panel', async ({ page }) => {
    await loginAsOwner(page)
    for (const path of APP_PAGES) await scan(page, path)
  })
})
