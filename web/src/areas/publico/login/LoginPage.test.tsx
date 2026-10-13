import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router'
import { LoginPage } from './LoginPage'
import * as authModule from '../../../lib/api/auth'
import { ApiError } from '../../../lib/api/client'

vi.mock('../../../lib/auth/AuthContext', () => ({
  useAuth: () => ({ user: null, isLoading: false, logout: vi.fn(), invalidate: vi.fn() }),
}))

vi.mock('../../../lib/api/auth', () => ({
  authApi: { login: vi.fn() },
}))

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <LoginPage />
    </MemoryRouter>,
  )
}

describe('LoginPage', () => {
  beforeEach(() => vi.clearAllMocks())

  it('muestra errores de validación sin llamar a la API', async () => {
    renderPage()
    await userEvent.click(screen.getByRole('button', { name: /ingresar/i }))
    expect(await screen.findByText(/email inválido/i)).toBeInTheDocument()
    expect(vi.mocked(authModule.authApi.login)).not.toHaveBeenCalled()
  })

  it('muestra error de credenciales incorrectas (401)', async () => {
    vi.mocked(authModule.authApi.login).mockRejectedValue(new ApiError(401, 'Unauthorized'))
    renderPage()
    await userEvent.type(screen.getByLabelText(/email/i), 'a@b.com')
    await userEvent.type(screen.getByLabelText(/contraseña/i), 'pass1234')
    await userEvent.click(screen.getByRole('button', { name: /ingresar/i }))
    expect(await screen.findByRole('alert')).toHaveTextContent(/email o contraseña incorrectos/i)
  })

  it('muestra mensaje de cuenta bloqueada (423)', async () => {
    vi.mocked(authModule.authApi.login).mockRejectedValue(new ApiError(423, 'Locked'))
    renderPage()
    await userEvent.type(screen.getByLabelText(/email/i), 'a@b.com')
    await userEvent.type(screen.getByLabelText(/contraseña/i), 'pass1234')
    await userEvent.click(screen.getByRole('button', { name: /ingresar/i }))
    expect(await screen.findByRole('alert')).toHaveTextContent(/bloqueada/i)
  })

  it('llama a login con los datos correctos cuando el formulario es válido', async () => {
    vi.mocked(authModule.authApi.login).mockResolvedValue(undefined)
    renderPage()
    await userEvent.type(screen.getByLabelText(/email/i), 'dueno@taller.com')
    await userEvent.type(screen.getByLabelText(/contraseña/i), 'MiPass123!')
    await userEvent.click(screen.getByRole('button', { name: /ingresar/i }))
    await waitFor(() =>
      expect(vi.mocked(authModule.authApi.login)).toHaveBeenCalledWith({
        email: 'dueno@taller.com',
        password: 'MiPass123!',
        rememberMe: false,
      }),
    )
  })
})
