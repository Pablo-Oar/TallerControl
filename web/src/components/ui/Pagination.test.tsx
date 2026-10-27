import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Pagination, pageWindow } from './Pagination'

describe('Pagination', () => {
  it('pageWindow: pocas páginas se muestran todas; muchas, con elipsis', () => {
    expect(pageWindow(1, 5)).toEqual([1, 2, 3, 4, 5])
    expect(pageWindow(1, 12)).toEqual([1, 2, null, 12])
    expect(pageWindow(6, 12)).toEqual([1, null, 5, 6, 7, null, 12])
    expect(pageWindow(12, 12)).toEqual([1, null, 11, 12])
  })

  it('muestra el rango, marca la página actual y navega por número', async () => {
    const onPage = vi.fn()
    render(<Pagination page={2} pageSize={10} total={57} noun="clientes" onPage={onPage} />)
    expect(screen.getByText('Mostrando 11–20 de 57 clientes')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Página 2' })).toHaveAttribute('aria-current', 'page')
    await userEvent.click(screen.getByRole('button', { name: 'Página 6' }))
    expect(onPage).toHaveBeenCalledWith(6)
    await userEvent.click(screen.getByRole('button', { name: 'Anterior' }))
    expect(onPage).toHaveBeenCalledWith(1)
  })

  it('no se dibuja si todo entra en una página', () => {
    const { container } = render(<Pagination page={1} pageSize={10} total={10} noun="x" onPage={() => {}} />)
    expect(container).toBeEmptyDOMElement()
  })
})
