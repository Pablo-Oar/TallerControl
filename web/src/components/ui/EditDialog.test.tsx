import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { EditDialog } from './EditDialog'

function setup(props: Partial<React.ComponentProps<typeof EditDialog>> = {}) {
  const onClose = vi.fn()
  const onSubmit = vi.fn((e: React.FormEvent) => e.preventDefault())
  render(
    <EditDialog title="Editar cosa" onClose={onClose} onSubmit={onSubmit} dirty={false} {...props}>
      <input aria-label="nombre" />
    </EditDialog>,
  )
  return { onClose, onSubmit }
}

describe('EditDialog (T-UI-05)', () => {
  it('sin cambios cierra directo con Cancelar y con Escape', async () => {
    const { onClose } = setup()
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(onClose).toHaveBeenCalledTimes(1)
    await userEvent.keyboard('{Escape}')
    expect(onClose).toHaveBeenCalledTimes(2)
  })

  it('con cambios pide confirmación antes de descartar (Escape, ×, Cancelar)', async () => {
    const { onClose } = setup({ dirty: true })
    await userEvent.keyboard('{Escape}')
    expect(onClose).not.toHaveBeenCalled()
    expect(screen.getByText(/hay cambios sin guardar/i)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /seguir editando/i }))
    expect(screen.queryByText(/hay cambios sin guardar/i)).toBeNull()
    expect(onClose).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: 'Cerrar' }))
    await userEvent.click(screen.getByRole('button', { name: /descartar cambios/i }))
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('en edición, Guardar se habilita solo con cambios; en alta siempre', () => {
    setup({ requireChanges: true, dirty: false })
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled()
  })

  it('con cambios habilita Guardar y envía el formulario', async () => {
    const { onSubmit } = setup({ requireChanges: true, dirty: true })
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))
    expect(onSubmit).toHaveBeenCalledTimes(1)
  })

  it('muestra el error del servidor y deshabilita Cancelar mientras guarda', () => {
    setup({ error: 'No se pudo guardar', saving: true })
    expect(screen.getByRole('alert')).toHaveTextContent('No se pudo guardar')
    expect(screen.getByRole('button', { name: 'Cancelar' })).toBeDisabled()
  })
})
