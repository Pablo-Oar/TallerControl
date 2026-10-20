import { useState } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { TabNav, TabPanel, Tabs } from './Tabs'

/** T-UI-06: pestañas accesibles en modo controlado y en modo navegación. */

const ITEMS = [
  { id: 'a', label: 'Ingresos' },
  { id: 'b', label: 'Stock' },
  { id: 'c', label: 'Compras' },
]

function Controlled() {
  const [tab, setTab] = useState('a')
  return (
    <>
      <Tabs label="Reportes" idPrefix="rep" items={ITEMS} value={tab} onChange={setTab} />
      <TabPanel idPrefix="rep" tabId={tab} active>
        contenido {tab}
      </TabPanel>
    </>
  )
}

describe('Tabs controlado (T-UI-06)', () => {
  it('expone tablist/tab/tabpanel relacionados por aria-controls y aria-labelledby', () => {
    render(<Controlled />)
    expect(screen.getByRole('tablist', { name: 'Reportes' })).toBeInTheDocument()
    const selected = screen.getByRole('tab', { name: 'Ingresos' })
    expect(selected).toHaveAttribute('aria-selected', 'true')
    const panel = screen.getByRole('tabpanel')
    expect(panel).toHaveAttribute('id', selected.getAttribute('aria-controls'))
    expect(panel).toHaveAttribute('aria-labelledby', selected.id)
    expect(panel).toHaveTextContent('contenido a')
  })

  it('el clic cambia de pestaña y de panel', async () => {
    render(<Controlled />)
    await userEvent.click(screen.getByRole('tab', { name: 'Stock' }))
    expect(screen.getByRole('tab', { name: 'Stock' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tabpanel')).toHaveTextContent('contenido b')
  })

  it('solo la pestaña activa entra en el orden de Tab (tabulación rodante)', () => {
    render(<Controlled />)
    expect(screen.getByRole('tab', { name: 'Ingresos' })).toHaveAttribute('tabindex', '0')
    expect(screen.getByRole('tab', { name: 'Stock' })).toHaveAttribute('tabindex', '-1')
  })

  it('flechas, Home y End mueven la selección con vuelta circular y llevan el foco', async () => {
    render(<Controlled />)
    screen.getByRole('tab', { name: 'Ingresos' }).focus()
    await userEvent.keyboard('{ArrowRight}')
    expect(screen.getByRole('tab', { name: 'Stock' })).toHaveAttribute('aria-selected', 'true')
    await userEvent.keyboard('{End}')
    expect(screen.getByRole('tab', { name: 'Compras' })).toHaveAttribute('aria-selected', 'true')
    await userEvent.keyboard('{ArrowRight}')
    expect(screen.getByRole('tab', { name: 'Ingresos' })).toHaveAttribute('aria-selected', 'true')
    await userEvent.keyboard('{ArrowLeft}')
    expect(screen.getByRole('tab', { name: 'Compras' })).toHaveAttribute('aria-selected', 'true')
    await userEvent.keyboard('{Home}')
    expect(screen.getByRole('tab', { name: 'Ingresos' })).toHaveAttribute('aria-selected', 'true')
    await vi_waitFrame()
    expect(screen.getByRole('tab', { name: 'Ingresos' })).toHaveFocus()
  })
})

const vi_waitFrame = () => new Promise<void>(resolve => requestAnimationFrame(() => resolve()))

describe('TabNav (T-UI-06)', () => {
  function renderAt(path: string) {
    return render(
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route
            path="*"
            element={
              <TabNav
                label="Facturación"
                items={[
                  { to: '/app/invoicing', label: 'Facturas', end: true },
                  { to: '/app/invoicing/iva-book', label: 'Libro IVA' },
                  { to: '/app/invoicing/setup', label: 'Configuración ARCA' },
                ]}
              />
            }
          />
        </Routes>
      </MemoryRouter>,
    )
  }

  it('son enlaces dentro de una navegación y la activa lleva aria-current="page"', () => {
    renderAt('/app/invoicing/iva-book')
    expect(screen.getByRole('navigation', { name: 'Facturación' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Libro IVA' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('link', { name: 'Facturas' })).not.toHaveAttribute('aria-current')
  })

  it('la pestaña raíz solo está activa en la ruta exacta (end)', () => {
    renderAt('/app/invoicing')
    expect(screen.getByRole('link', { name: 'Facturas' })).toHaveAttribute('aria-current', 'page')
  })

  it('navegar cambia la pestaña activa', async () => {
    renderAt('/app/invoicing')
    await userEvent.click(screen.getByRole('link', { name: 'Configuración ARCA' }))
    expect(screen.getByRole('link', { name: 'Configuración ARCA' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('link', { name: 'Facturas' })).not.toHaveAttribute('aria-current')
  })
})
