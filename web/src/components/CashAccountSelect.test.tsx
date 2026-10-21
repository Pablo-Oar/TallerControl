import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useCashAccountChoice } from './CashAccountSelect'

const auth = vi.hoisted(() => ({ value: { user: null as unknown } }))
vi.mock('../lib/auth/AuthContext', () => ({ useAuth: () => auth.value }))

const api = vi.hoisted(() => ({ listAccounts: vi.fn() }))
vi.mock('../lib/api/cash', () => ({ cashApi: api }))

function Probe() {
  const { accountId, field } = useCashAccountChoice('Ingresa a la cuenta')
  return (
    <div>
      {field}
      <p data-testid="chosen">{accountId ?? 'ninguna'}</p>
    </div>
  )
}

function renderProbe() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <Probe />
    </QueryClientProvider>,
  )
}

// T-UI-11 (F-02): el cobro/compra/venta ofrece las cuentas activas y recuerda la última elegida.
describe('useCashAccountChoice', () => {
  beforeEach(() => {
    localStorage.clear()
    auth.value = { user: { permissions: ['cash.read'] } }
    api.listAccounts.mockResolvedValue([
      { id: 'a1', name: 'Caja chica', kind: 'cash', isActive: true, sessionOpenedAt: '2026-10-21T10:00:00Z' },
      { id: 'a2', name: 'Banco', kind: 'bank', isActive: true, sessionOpenedAt: null },
      { id: 'a3', name: 'Vieja', kind: 'cash', isActive: false, sessionOpenedAt: null },
    ])
  })

  it('lista solo las cuentas activas y arranca sin cuenta elegida', async () => {
    renderProbe()
    expect(await screen.findByRole('option', { name: 'Caja chica · Efectivo' })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: /Vieja/ })).not.toBeInTheDocument()
    expect(screen.getByTestId('chosen')).toHaveTextContent('ninguna')
  })

  it('elegir una cuenta la devuelve y la recuerda para la próxima vez', async () => {
    renderProbe()
    await screen.findByRole('option', { name: 'Banco · Banco' })
    await userEvent.selectOptions(screen.getByLabelText('Ingresa a la cuenta'), 'a2')
    expect(screen.getByTestId('chosen')).toHaveTextContent('a2')
    expect(screen.getByText(/no tiene la sesión abierta/i)).toBeInTheDocument()
    expect(localStorage.getItem('tc_cash_account')).toBe('a2')
  })

  it('no muestra el selector a quien no puede ver la caja', () => {
    auth.value = { user: { permissions: ['workorders.read'] } }
    renderProbe()
    expect(screen.queryByLabelText('Ingresa a la cuenta')).not.toBeInTheDocument()
    expect(screen.getByTestId('chosen')).toHaveTextContent('ninguna')
  })
})
