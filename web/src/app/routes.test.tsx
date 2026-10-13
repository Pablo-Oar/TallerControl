import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { appRoutes } from './routes.tsx'

// Sin sesión activa: /me lanza error 401 → AuthProvider tendrá user: null
vi.mock('../lib/api/auth', () => ({
  authApi: {
    me: vi.fn().mockRejectedValue({ status: 401 }),
    login: vi.fn(),
    logout: vi.fn(),
    forgotPassword: vi.fn(),
    resetPassword: vi.fn(),
  },
}))

// AuthProvider usa AuthContext internamente
// Mockeamos useAuth para evitar depender del QueryClient en estos tests de shell
vi.mock('../lib/auth/AuthContext', () => ({
  AuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useAuth: () => ({ user: null, isLoading: false, logout: vi.fn(), invalidate: vi.fn() }),
}))

function renderEn(ruta: string) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(appRoutes, { initialEntries: [ruta] })
  render(
    <QueryClientProvider client={qc}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

describe('shell de la SPA', () => {
  it('la raíz redirige a /app, que sin sesión redirige a /login', async () => {
    renderEn('/')
    // Sin sesión, RequireAuth redirige a /login → aparece la pantalla de ingreso
    expect(await screen.findByRole('heading', { name: /ingresar/i })).toBeInTheDocument()
  })

  it('/m muestra el área del mecánico', async () => {
    renderEn('/m')
    expect(await screen.findByRole('heading', { name: 'Mis trabajos' })).toBeInTheDocument()
  })

  it('/login muestra la página de ingreso', async () => {
    renderEn('/login')
    expect(await screen.findByRole('heading', { name: /ingresar/i })).toBeInTheDocument()
  })
})
