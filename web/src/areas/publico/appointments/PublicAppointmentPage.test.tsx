import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { PublicAppointmentPage } from './PublicAppointmentPage'

const api = vi.hoisted(() => ({ get: vi.fn(), confirm: vi.fn(), decline: vi.fn() }))
vi.mock('../../../lib/api/appointments', () => ({ publicAppointmentApi: api }))

function renderPage() {
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter initialEntries={['/t/abc']}>
        <Routes>
          <Route path="t/:token" element={<PublicAppointmentPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const turno = {
  scheduledAt: '2026-10-27T13:00:00Z',
  timeZone: 'America/Argentina/Buenos_Aires',
  totalDurationMinutes: 75,
  status: 'pending',
  vehicleLabel: 'VW Gol',
  services: ['Cambio de aceite'],
}

describe('PublicAppointmentPage (A-03)', () => {
  it('muestra el turno sin datos del taller y permite confirmarlo', async () => {
    api.get.mockResolvedValue(turno)
    api.confirm.mockResolvedValue({ message: 'ok' })
    renderPage()

    expect(await screen.findByText('Cambio de aceite')).toBeInTheDocument()
    expect(screen.getByText('VW Gol')).toBeInTheDocument()
    expect(screen.getByText(/10:00/)).toBeInTheDocument() // 13:00 UTC = 10:00 en Argentina

    api.get.mockResolvedValue({ ...turno, status: 'confirmed' })
    await userEvent.click(screen.getByRole('button', { name: 'Confirmar mi turno' }))
    expect(api.confirm).toHaveBeenCalledWith('abc')
    expect(await screen.findByText('Confirmado')).toBeInTheDocument()
  })

  it('«No puedo ir» cancela y avisa que el taller fue notificado', async () => {
    api.get.mockResolvedValue(turno)
    api.decline.mockResolvedValue({ message: 'ok' })
    renderPage()

    await userEvent.click(await screen.findByRole('button', { name: 'No puedo ir' }))
    expect(api.decline).toHaveBeenCalledWith('abc')
    expect(await screen.findByText(/Avisamos al taller/)).toBeInTheDocument()
  })

  it('un enlace inválido o vencido muestra un aviso uniforme', async () => {
    api.get.mockRejectedValue(new Error('404'))
    renderPage()
    expect(await screen.findByText('Este enlace ya no está disponible')).toBeInTheDocument()
  })
})
