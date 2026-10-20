import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { EditableStatusBadge } from './EditableStatusBadge'

/** T-UI-07: el badge ofrece solo las opciones válidas, delega el cambio y muestra el error sin mover el estado. */

const OPTIONS = [
  { value: 'in_progress', label: 'En proceso' },
  { value: 'cancelled', label: 'Cancelada' },
]

function setup(onSelect = vi.fn(), options = OPTIONS) {
  render(<EditableStatusBadge status="pending" options={options} onSelect={onSelect} />)
  return onSelect
}

describe('EditableStatusBadge (T-UI-07)', () => {
  it('sin opciones es un sello común, sin botón', () => {
    setup(vi.fn(), [])
    expect(screen.queryByRole('button')).toBeNull()
    expect(screen.getByText('PENDIENTE')).toBeInTheDocument()
  })

  it('abre un menú con SOLO las opciones declaradas y llama a onSelect con el valor', async () => {
    const onSelect = setup()
    await userEvent.click(screen.getByRole('button', { name: /opciones de estado/i }))
    const items = screen.getAllByRole('menuitem')
    expect(items.map(i => i.textContent)).toEqual(['En proceso', 'Cancelada'])

    await userEvent.click(screen.getByRole('menuitem', { name: 'En proceso' }))
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('in_progress'))
    expect(screen.queryByRole('menu')).toBeNull()
  })

  it('si el cambio falla (p. ej. 422) muestra el mensaje y el sello sigue en el estado anterior', async () => {
    const onSelect = vi.fn().mockRejectedValue({ detail: 'Anulá o reintegrá los pagos antes de cancelar.' })
    setup(onSelect)
    await userEvent.click(screen.getByRole('button', { name: /opciones de estado/i }))
    await userEvent.click(screen.getByRole('menuitem', { name: 'Cancelada' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(/reintegrá los pagos/i)
    expect(screen.getByText('PENDIENTE')).toBeInTheDocument()
  })

  it('teclado: abre, navega con flechas, Enter elige, Escape cierra y devuelve el foco', async () => {
    const onSelect = setup()
    const trigger = screen.getByRole('button', { name: /opciones de estado/i })
    trigger.focus()
    await userEvent.keyboard('{Enter}')
    expect(screen.getByRole('menuitem', { name: 'En proceso' })).toHaveFocus()
    await userEvent.keyboard('{ArrowDown}')
    expect(screen.getByRole('menuitem', { name: 'Cancelada' })).toHaveFocus()
    await userEvent.keyboard('{ArrowDown}') // vuelta circular
    expect(screen.getByRole('menuitem', { name: 'En proceso' })).toHaveFocus()

    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('menu')).toBeNull()
    expect(trigger).toHaveFocus()
    expect(onSelect).not.toHaveBeenCalled()

    await userEvent.keyboard('{Enter}{Enter}')
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('in_progress'))
  })

  it('clic fuera cierra el menú', async () => {
    setup()
    await userEvent.click(screen.getByRole('button', { name: /opciones de estado/i }))
    expect(screen.getByRole('menu')).toBeInTheDocument()
    await userEvent.click(document.body)
    expect(screen.queryByRole('menu')).toBeNull()
  })
})
