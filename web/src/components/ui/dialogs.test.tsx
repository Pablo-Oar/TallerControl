import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { confirmDialog, DialogHost, notify } from './dialogs'

describe('dialogs (reemplazo de confirm/alert)', () => {
  it('confirmDialog resuelve true al aceptar y false al cancelar', async () => {
    const user = userEvent.setup()
    render(<DialogHost />)

    let result: Promise<boolean> = Promise.resolve(false)
    act(() => {
      result = confirmDialog('¿Eliminar la venta?', { confirmLabel: 'Eliminar', danger: true })
    })
    expect(await screen.findByText('¿Eliminar la venta?')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Eliminar' }))
    await expect(result).resolves.toBe(true)

    act(() => {
      result = confirmDialog('¿Cancelar?')
    })
    await user.click(await screen.findByRole('button', { name: 'Cancelar' }))
    await expect(result).resolves.toBe(false)
  })

  it('notify muestra un aviso con role=alert que se puede cerrar', async () => {
    const user = userEvent.setup()
    render(<DialogHost />)
    act(() => notify('No se pudo guardar'))
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo guardar')
    await user.click(screen.getByRole('button', { name: /cerrar aviso/i }))
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
