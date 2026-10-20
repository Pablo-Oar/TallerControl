import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LoadError } from './LoadError'

describe('LoadError', () => {
  afterEach(() => vi.restoreAllMocks())

  it('con red: mensaje genérico y Reintentar llama a onRetry', async () => {
    const onRetry = vi.fn()
    render(<LoadError what="la orden" onRetry={onRetry} />)
    expect(screen.getByRole('alert')).toHaveTextContent('No se pudo cargar la orden.')
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(onRetry).toHaveBeenCalledOnce()
  })

  it('sin conexión: lo dice (modo avión en /m)', () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    render(<LoadError what="los trabajos" onRetry={() => {}} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Sin conexión')
  })
})
