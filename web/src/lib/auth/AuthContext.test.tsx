import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { MeResponse } from '../api/auth'
import { AuthProvider, useAuth } from './AuthContext'

const clearCsrfToken = vi.hoisted(() => vi.fn())
vi.mock('../api/client', () => ({ clearCsrfToken }))
vi.mock('../api/auth', () => ({ authApi: { me: vi.fn().mockRejectedValue(new Error('401')), logout: vi.fn() } }))

const owner = { user: { id: '1' }, role: 'owner', home: '/app' } as unknown as MeResponse

function Probe() {
  const { invalidate, user } = useAuth()
  return (
    <>
      <button onClick={() => invalidate()}>invalidar</button>
      <button onClick={() => invalidate(owner)}>invalidar con usuario</button>
      <span data-testid="role">{user?.role ?? 'nadie'}</span>
    </>
  )
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

  it('con el usuario recién obtenido, el guard no ve al usuario anterior mientras se refresca', async () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <AuthProvider>
          <Probe />
        </AuthProvider>
      </QueryClientProvider>,
    )
    expect(screen.getByTestId('role')).toHaveTextContent('nadie')
    await userEvent.click(screen.getByRole('button', { name: 'invalidar con usuario' }))
    expect(screen.getByTestId('role')).toHaveTextContent('owner')
  })
})
