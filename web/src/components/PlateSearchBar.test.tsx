import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { PlateSearchBar } from './PlateSearchBar'

describe('PlateSearchBar', () => {
  it('navega a la pantalla indicada con la patente en mayúsculas (el mecánico usa /m)', async () => {
    const onNavigate = vi.fn()
    render(<PlateSearchBar onNavigate={onNavigate} target="/m" />)
    await userEvent.type(screen.getByLabelText('Buscar patente'), 'ab123cd{enter}')
    expect(onNavigate).toHaveBeenCalledWith('/m?q=AB123CD')
  })

  it('por defecto busca en los vehículos del taller', async () => {
    const onNavigate = vi.fn()
    render(<PlateSearchBar onNavigate={onNavigate} />)
    await userEvent.type(screen.getByLabelText('Buscar patente'), 'x1{enter}')
    expect(onNavigate).toHaveBeenCalledWith('/app/vehicles?q=X1')
  })

  it('evita el autocorrector del teclado del celular', () => {
    render(<PlateSearchBar onNavigate={() => {}} />)
    const input = screen.getByLabelText('Buscar patente')
    expect(input).toHaveAttribute('autocapitalize', 'characters')
    expect(input).toHaveAttribute('autocorrect', 'off')
  })
})
