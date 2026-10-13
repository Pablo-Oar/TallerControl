import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router'
import { AcceptInvitationPage } from './AcceptInvitationPage'
import { ApiError } from '../../../lib/api/client'
import * as usersModule from '../../../lib/api/users'

vi.mock('../../../lib/api/users', () => ({
  invitationsApi: { accept: vi.fn() },
  usersApi: { listMembers: vi.fn(), updateMember: vi.fn() },
}))

vi.mock('../../../lib/auth/AuthContext', () => ({
  useAuth: () => ({ user: null, isLoading: false, logout: vi.fn(), invalidate: vi.fn() }),
}))

function renderPage(token = 'valid-token') {
  return render(
    <MemoryRouter initialEntries={[`/invitations/${token}/accept`]}>
      <Routes>
        <Route path="/invitations/:token/accept" element={<AcceptInvitationPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('AcceptInvitationPage', () => {
  beforeEach(() => vi.clearAllMocks())

  it('valida que las contraseñas coincidan', async () => {
    renderPage()
    await userEvent.type(screen.getByLabelText(/^contraseña$/i), 'MiPass123!')
    await userEvent.type(screen.getByLabelText(/confirmar/i), 'OtraPass!')
    await userEvent.click(screen.getByRole('button', { name: /activar/i }))
    expect(await screen.findByText(/no coinciden/i)).toBeInTheDocument()
    expect(vi.mocked(usersModule.invitationsApi.accept)).not.toHaveBeenCalled()
  })

  it('muestra error cuando la invitación es inválida (422)', async () => {
    vi.mocked(usersModule.invitationsApi.accept).mockRejectedValue(new ApiError(422, 'Token inválido'))
    renderPage()
    await userEvent.type(screen.getByLabelText(/^contraseña$/i), 'MiPass123!')
    await userEvent.type(screen.getByLabelText(/confirmar/i), 'MiPass123!')
    await userEvent.click(screen.getByRole('button', { name: /activar/i }))
    expect(await screen.findByRole('alert')).toHaveTextContent(/inválida o expiró/i)
  })
})
