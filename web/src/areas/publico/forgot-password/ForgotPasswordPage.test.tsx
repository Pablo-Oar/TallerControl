import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router'
import { ForgotPasswordPage } from './ForgotPasswordPage'
import * as authModule from '../../../lib/api/auth'

vi.mock('../../../lib/api/auth', () => ({
  authApi: { forgotPassword: vi.fn().mockResolvedValue(undefined) },
}))

function renderPage() {
  return render(
    <MemoryRouter>
      <ForgotPasswordPage />
    </MemoryRouter>,
  )
}

describe('ForgotPasswordPage', () => {
  beforeEach(() => vi.clearAllMocks())

  it('muestra error de validación con email inválido', async () => {
    renderPage()
    await userEvent.type(screen.getByLabelText(/email/i), 'noesun@')
    await userEvent.click(screen.getByRole('button', { name: /enviar/i }))
    expect(await screen.findByText(/email inválido/i)).toBeInTheDocument()
  })

  it('muestra confirmación tras enviar un email válido', async () => {
    vi.mocked(authModule.authApi.forgotPassword).mockResolvedValue(undefined)
    renderPage()
    await userEvent.type(screen.getByLabelText(/email/i), 'alguien@taller.com')
    await userEvent.click(screen.getByRole('button', { name: /enviar/i }))
    expect(await screen.findByText(/si el email está registrado/i)).toBeInTheDocument()
  })

  it('muestra confirmación aunque la API falle (T-AUTH-07: no revelar existencia)', async () => {
    vi.mocked(authModule.authApi.forgotPassword).mockRejectedValueOnce(new Error('Network error'))
    renderPage()
    await userEvent.type(screen.getByLabelText(/email/i), 'alguien@taller.com')
    await userEvent.click(screen.getByRole('button', { name: /enviar/i }))
    // Siempre muestra confirmación, nunca revela si el email existe
    expect(await screen.findByText(/si el email está registrado/i)).toBeInTheDocument()
  })
})
