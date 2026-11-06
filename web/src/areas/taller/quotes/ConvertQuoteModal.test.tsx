import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ConvertQuoteModal } from './ConvertQuoteModal'

const api = vi.hoisted(() => ({ get: vi.fn() }))
const wo = vi.hoisted(() => ({ listMechanics: vi.fn() }))
vi.mock('../../../lib/api/vehicles', () => ({ vehiclesApi: api }))
vi.mock('../../../lib/api/workOrders', () => ({ workOrdersApi: wo }))

const MECHANICS = [{ membershipId: 'm1', displayName: 'Carlos' }, { membershipId: 'm2', displayName: 'Luis' }]

function renderModal(onConfirm = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <ConvertQuoteModal vehicleId="v1" vehiclePlate="AB123CD" busy={false} error={null} onConfirm={onConfirm} onClose={() => {}} />
    </QueryClientProvider>,
  )
  return onConfirm
}

// T-UI-12 (O-03): convertir un presupuesto pide el kilometraje de ingreso, precargado con el del vehículo.
describe('ConvertQuoteModal', () => {
  beforeEach(() => wo.listMechanics.mockResolvedValue(MECHANICS))

  it('precarga el kilometraje del vehículo y lo envía al confirmar', async () => {
    api.get.mockResolvedValue({ odometerKm: 45000 })
    const onConfirm = renderModal()

    expect(await screen.findByDisplayValue('45000')).toBeInTheDocument()
    await userEvent.selectOptions(await screen.findByLabelText(/mecánico/i), 'm2')
    await userEvent.type(screen.getByLabelText(/problema reportado/i), 'Ruido en el tren delantero')
    await userEvent.click(screen.getByRole('button', { name: /crear orden/i }))

    expect(onConfirm).toHaveBeenCalledWith({ odometerKm: 45000, fuelLevel: null, reportedIssue: 'Ruido en el tren delantero', assignedMechanicId: 'm2' })
  })

  it('permite corregir el kilometraje antes de crear la orden', async () => {
    api.get.mockResolvedValue({ odometerKm: 45000 })
    const onConfirm = renderModal()

    const km = await screen.findByDisplayValue('45000')
    await userEvent.clear(km)
    await userEvent.type(km, '46210')
    await userEvent.selectOptions(await screen.findByLabelText(/mecánico/i), 'm1')
    await userEvent.click(screen.getByRole('button', { name: /crear orden/i }))

    expect(onConfirm).toHaveBeenCalledWith(expect.objectContaining({ odometerKm: 46210 }))
  })

  it('no deja crear la orden con el kilometraje vacío', async () => {
    api.get.mockResolvedValue({ odometerKm: 100 })
    renderModal()

    const km = await screen.findByDisplayValue('100')
    await userEvent.clear(km)

    expect(screen.getByRole('button', { name: /crear orden/i })).toBeDisabled()
  })

  it('no deja crear la orden sin elegir un mecánico', async () => {
    api.get.mockResolvedValue({ odometerKm: 100 })
    renderModal()

    await screen.findByDisplayValue('100')
    await screen.findByRole('option', { name: 'Carlos' })
    expect(screen.getByRole('button', { name: /crear orden/i })).toBeDisabled()
  })
})
