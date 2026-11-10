import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Tooltip } from './Tooltip'
import { StatusBadge } from './StatusBadge'

describe('T-UI-15 Tooltip accesible', () => {
  it('se abre con el foco del teclado y se cierra con Escape', async () => {
    const user = userEvent.setup()
    render(
      <Tooltip content="Ayuda de prueba" focusable>
        <span>Sello</span>
      </Tooltip>,
    )
    expect(screen.queryByRole('tooltip')).toBeNull()
    await user.tab()
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Ayuda de prueba')
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('tooltip')).toBeNull()
  })

  it('se abre y se cierra al tocar (sin mouse ni teclado)', async () => {
    const user = userEvent.setup()
    render(
      <Tooltip content="Toque">
        <span>Icono</span>
      </Tooltip>,
    )
    await user.click(screen.getByText('Icono'))
    expect(await screen.findByRole('tooltip')).toBeInTheDocument()
    await new Promise((r) => setTimeout(r, 600))
    await user.click(screen.getByText('Icono'))
    expect(screen.queryByRole('tooltip')).toBeNull()
  })

  it('el sello de estado explica su significado al enfocarlo', async () => {
    const user = userEvent.setup()
    render(<StatusBadge status="converted" />)
    await user.tab()
    expect(await screen.findByRole('tooltip')).toHaveTextContent(/Orden de Trabajo/)
  })
})
