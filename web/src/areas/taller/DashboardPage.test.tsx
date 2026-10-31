import { MemoryRouter } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { DashboardPage } from './DashboardPage'
import type { DashboardDto } from '../../lib/api/dashboard'

const auth = vi.hoisted(() => ({ value: { user: null as unknown } }))
vi.mock('../../lib/auth/AuthContext', () => ({ useAuth: () => auth.value }))

const api = vi.hoisted(() => ({ getDashboard: vi.fn() }))
vi.mock('../../lib/api/dashboard', () => api)

vi.mock('../../lib/api/onboarding', () => ({ onboardingApi: { get: vi.fn().mockResolvedValue(null) } }))

// recharts necesita medidas reales de layout: en jsdom se reemplaza por un marcador.
vi.mock('./dashboard/RevenueChart', () => ({ default: () => <div>grafico de ingresos</div> }))

const baseDashboard: DashboardDto = {
  totalReceivable: 1725907,
  openWorkOrders: 26,
  pendingQuotes: 14,
  lowStockItems: 1,
  thresholds: { hotQuoteHours: 48, stuckOrderDays: 3, agedReceivableDays: 30 },
  revenueToday: 324500,
  paymentsToday: 9,
  revenueLast7Days: [{ date: '2026-10-30', amount: 324500 }],
  agedReceivable: { count: 15, amount: 842000 },
  ordersByStatus: { pending: 6, inProgress: 18, waitingParts: 8, readyToDeliver: 15, stuck: 5 },
  liveOrders: [
    {
      id: 'o1',
      number: 158,
      status: 'waiting_parts',
      priority: 'normal',
      customerName: 'Cliente Trabado',
      vehiclePlate: 'AD701ZZ',
      vehicleLabel: 'Fiat Cronos',
      mechanicName: null,
      statusSince: '2026-10-21T10:00:00Z',
      daysInStatus: 9,
      isStuck: true,
    },
  ],
  sentQuotes: 12,
  sentQuotesTotal: 3400000,
  hotQuotes: {
    total: 1,
    items: [
      {
        id: 'q1',
        number: 41,
        customerName: 'Jorge Gómez',
        customerPhone: '+5491122334455',
        vehiclePlate: 'AB123CD',
        total: 95000,
        sentAt: '2026-10-27T10:00:00Z',
        hoursSinceSent: 72,
      },
    ],
  },
  todayAppointments: { total: 0, unassigned: 0, items: [] },
  cash: { totalBalance: 324500, accounts: [] },
  lowStock: [{ id: 's1', name: 'Filtro de aceite', sku: 'FIL-001', stockQty: 1, stockMin: 5 }],
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

// T-UI-13: el Inicio como centro de comando (docs/plan-dashboard-inicio.md).
describe('DashboardPage', () => {
  beforeEach(() => {
    auth.value = {
      user: { role: 'owner', permissions: ['workorders.read', 'workorders.write', 'quotes.write', 'payments.write', 'counter_sales.write'] },
    }
    api.getDashboard.mockResolvedValue(baseDashboard)
  })

  it('muestra los cuatro indicadores con el saldo antiguo según el umbral del taller', async () => {
    renderPage()

    expect(await screen.findByText('Ingresos de hoy')).toBeInTheDocument()
    expect(screen.getByText('Por cobrar')).toBeInTheDocument()
    expect(screen.getByText('Órdenes activas')).toBeInTheDocument()
    expect(screen.getByText('Presupuestos pendientes')).toBeInTheDocument()
    expect(screen.getByText('Atrasado (+30 días)')).toBeInTheDocument()
    expect(screen.getByText('Órdenes atrasadas')).toBeInTheDocument()
  })

  it('«Taller en vivo» avisa cuántas órdenes están trabadas con el umbral del taller', async () => {
    renderPage()

    expect(await screen.findByText(/5 órdenes llevan más de 3 días en el mismo estado/)).toBeInTheDocument()
    expect(screen.getByText(/Cliente Trabado/)).toBeInTheDocument()
    // Se dibuja dos veces (columna de escritorio y línea de celular; una queda oculta por CSS).
    expect(screen.getAllByText('Sin mecánico').length).toBeGreaterThan(0)
  })

  it('muestra los presupuestos sin respuesta con el botón de WhatsApp', async () => {
    renderPage()

    expect(await screen.findByText('Presupuestos sin respuesta')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reenviar WhatsApp' })).toBeInTheDocument()
  })

  it('no muestra las secciones que el servidor no mandó (rol sin permiso)', async () => {
    api.getDashboard.mockResolvedValue({ ...baseDashboard, hotQuotes: null, cash: null, lowStock: null, todayAppointments: null })
    renderPage()

    expect(await screen.findByText('Ingresos de hoy')).toBeInTheDocument()
    expect(screen.queryByText('Presupuestos sin respuesta')).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Caja' })).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Stock bajo' })).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Turnos de hoy' })).not.toBeInTheDocument()
  })

  it('las acciones rápidas aparecen solo si el rol puede hacerlas', async () => {
    auth.value = { user: { role: 'admin_staff', permissions: ['workorders.read', 'workorders.write'] } }
    renderPage()

    expect(await screen.findByRole('link', { name: /Nueva orden/ })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Nuevo presupuesto/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Venta de mostrador/ })).not.toBeInTheDocument()
  })
})
