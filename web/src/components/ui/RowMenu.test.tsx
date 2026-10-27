import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { RowMenu } from './RowMenu'

describe('RowMenu', () => {
  it('abre el menú, ejecuta la acción elegida y se cierra', async () => {
    const onSelect = vi.fn()
    render(<RowMenu items={[{ label: 'Eliminar', onSelect, tone: 'danger' }]} />)
    expect(screen.queryByRole('menu')).toBeNull()
    await userEvent.click(screen.getByRole('button', { name: 'Más acciones' }))
    await userEvent.click(screen.getByRole('menuitem', { name: 'Eliminar' }))
    expect(onSelect).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('menu')).toBeNull()
  })

  it('Escape lo cierra y no se dibuja sin acciones', async () => {
    const { container } = render(<RowMenu items={[{ label: 'A', onSelect: () => {} }]} />)
    await userEvent.click(screen.getByRole('button', { name: 'Más acciones' }))
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('menu')).toBeNull()
    const empty = render(<RowMenu items={[]} />)
    expect(empty.container).toBeEmptyDOMElement()
    expect(container).not.toBeEmptyDOMElement()
  })
})
