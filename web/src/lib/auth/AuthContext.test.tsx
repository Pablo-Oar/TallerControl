import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AuthProvider, useAuth } from './AuthContext'

const clearCsrfToken = vi.hoisted(() => vi.fn())
vi.mock('../api/client', () => ({ clearCsrfToken }))
vi.mock('../api/auth', () => ({ authApi: { me: vi.fn().mockRejectedValue(new Error('401')), logout: vi.fn() } }))

function Probe() {
  const { invalidate } = useAuth()
  return <button onClick={invalidate}>invalidar</button>
}

describe('AuthProvider.invalidate', () => {
  it('descarta el token antiforgery: el anterior pertenece a la sesión anónima', async () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <AuthProvider>
          <Probe />
        </AuthProvider>
      </QueryClientProvider>,
    )
    clearCsrfToken.mockClear()
    await userEvent.click(screen.getByRole('button', { name: 'invalidar' }))
    expect(clearCsrfToken).toHaveBeenCalledTimes(1)
  })
})
