import { api } from './client'
import type { WorkOrderPriority, WorkOrderStatus } from './workOrders'

/** Umbrales del taller que usa el Inicio (D-42). */
export interface DashboardThresholds {
  hotQuoteHours: number
  stuckOrderDays: number
  agedReceivableDays: number
}

export interface RevenueDay {
  /** Día local del taller, `YYYY-MM-DD`. */
  date: string
  amount: number
}

/** Órdenes con saldo desde hace más de N días (D-39). */
export interface AgedReceivable {
  count: number
  amount: number
}

export interface OrdersByStatus {
  pending: number
  inProgress: number
  waitingParts: number
  readyToDeliver: number
  /** Las que llevan más de N días en el mismo estado. */
  stuck: number
}

export interface LiveOrder {
  id: string
  number: number
  status: WorkOrderStatus
  priority: WorkOrderPriority
  customerName: string
  vehiclePlate: string
  vehicleLabel: string | null
  mechanicName: string | null
  statusSince: string
  daysInStatus: number
  isStuck: boolean
}

export interface HotQuote {
  id: string
  number: number
  customerName: string
  customerPhone: string | null
  customerEmail: string | null
  /** El cliente pidió no ser contactado: no se ofrece ningún reenvío. */
  doNotContact: boolean
  vehiclePlate: string
  total: number
  sentAt: string
  hoursSinceSent: number
}

export interface HotQuotes {
  total: number
  items: HotQuote[]
}

export interface TodayAppointment {
  id: string
  scheduledAt: string
  customerName: string
  vehicleId: string
  vehiclePlate: string
  mechanicName: string | null
  status: 'pending' | 'confirmed' | 'completed' | 'cancelled'
}

export interface TodayAppointments {
  total: number
  unassigned: number
  items: TodayAppointment[]
}

export interface CashAccountStatus {
  id: string
  name: string
  kind: 'cash' | 'bank' | 'digital'
  balance: number
  sessionOpenedAt: string | null
  sessionOpenedBy: string | null
}

export interface CashStatus {
  totalBalance: number
  accounts: CashAccountStatus[]
}

export interface LowStockItem {
  id: string
  name: string
  sku: string | null
  stockQty: number
  stockMin: number | null
}

export interface DashboardDto {
  totalReceivable: number
  openWorkOrders: number
  pendingQuotes: number
  lowStockItems: number
  thresholds: DashboardThresholds
  revenueToday: number
  paymentsToday: number
  revenueLast7Days: RevenueDay[]
  agedReceivable: AgedReceivable
  ordersByStatus: OrdersByStatus
  liveOrders: LiveOrder[]
  sentQuotes: number
  sentQuotesTotal: number
  /** Las secciones con permiso propio llegan `null` si el rol no lo tiene. */
  hotQuotes: HotQuotes | null
  todayAppointments: TodayAppointments | null
  cash: CashStatus | null
  lowStock: LowStockItem[] | null
}

export function getDashboard(): Promise<DashboardDto> {
  return api.get<DashboardDto>('/api/v1/dashboard')
}
