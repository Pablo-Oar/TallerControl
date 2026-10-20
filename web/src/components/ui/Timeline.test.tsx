import { MemoryRouter } from 'react-router'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { Timeline, type TimelineItem } from './Timeline'

/** T-UI-08b: Timeline — render de ítems, enlaces, detalle plegable operable con teclado y estado vacío. */

const LONG = 'Detalle muy largo. '.repeat(15)

const ITEMS: TimelineItem[] = [
  { id: '1', date: '2026-10-20T14:30:00Z', title: 'Pendiente → En proceso', author: 'Ana', kind: 'status', detail: 'Arrancó' },
  { id: '2', date: '2026-10-19T10:00:00Z', title: 'Orden #3', href: '/app/work-orders/3', kind: 'order', badge: <span>ENTREGADA</span> },
  { id: '3', date: '2026-10-18T10:00:00Z', title: 'Cliente Prueba', kind: 'response', detail: LONG, emphasis: true },
]

function renderIt(items = ITEMS) {
  return render(
    <MemoryRouter>
      <Timeline items={items} label="Historial" />
    </MemoryRouter>,
  )
}

describe('Timeline', () => {
  it('es una lista con nombre accesible y un ítem por evento, con <time dateTime>', () => {
    renderIt()
    const list = screen.getByRole('list', { name: 'Historial' })
    expect(list.querySelectorAll('li')).toHaveLength(3)
    const times = list.querySelectorAll('time')
    expect(times[0]).toHaveAttribute('datetime', '2026-10-20T14:30:00Z')
  })

  it('muestra autor, badge y detalle corto; el título con href es un enlace', () => {
    renderIt()
    expect(screen.getByText('por Ana')).toBeInTheDocument()
    expect(screen.getByText('ENTREGADA')).toBeInTheDocument()
    expect(screen.getByText('Arrancó')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Orden #3' })).toHaveAttribute('href', '/app/work-orders/3')
  })

  it('el detalle largo se pliega y se expande con un botón (toque y teclado)', async () => {
    renderIt()
    const toggle = screen.getByRole('button', { name: /ver más/i })
    expect(toggle).toHaveAttribute('aria-expanded', 'false')
    toggle.focus()
    await userEvent.keyboard('{Enter}')
    expect(screen.getByRole('button', { name: /ver menos/i })).toHaveAttribute('aria-expanded', 'true')
    await userEvent.keyboard('{Enter}')
    expect(screen.getByRole('button', { name: /ver más/i })).toHaveAttribute('aria-expanded', 'false')
  })

  it('los detalles cortos no llevan botón', () => {
    renderIt([ITEMS[0]])
    expect(screen.queryByRole('button')).toBeNull()
  })

  it('sin ítems muestra el texto vacío', () => {
    renderIt([])
    expect(screen.getByText(/sin movimientos todavía/i)).toBeInTheDocument()
    expect(screen.queryByRole('list')).toBeNull()
  })
})
