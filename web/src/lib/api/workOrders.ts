import { api } from './client'
import type { Page } from './customers'

export type WorkOrderStatus = 'pending' | 'in_progress' | 'waiting_parts' | 'completed' | 'delivered' | 'cancelled'
export type WorkOrderPriority = 'low' | 'normal' | 'high'
export type WorkOrderItemKind = 'service' | 'product' | 'manual'

export interface AssignableMechanic {
  membershipId: string
  displayName: string
}

export const STATUS_LABELS: Record<WorkOrderStatus, string> = {
  pending: 'Pendiente',
  in_progress: 'En proceso',
  waiting_parts: 'Esperando repuestos',
  completed: 'Completada',
  delivered: 'Entregada',
  cancelled: 'Cancelada',
}

/** Transiciones válidas por estado (espejo de `WorkOrder.CambiarEstado`; el servidor las vuelve a validar). */
export const WORK_ORDER_TRANSITIONS: Record<WorkOrderStatus, WorkOrderStatus[]> = {
  pending: ['in_progress', 'cancelled'],
  in_progress: ['waiting_parts', 'completed', 'cancelled'],
  waiting_parts: ['in_progress', 'cancelled'],
  completed: ['delivered', 'cancelled'],
  delivered: [],
  cancelled: [],
}

export const PRIORITY_LABELS: Record<WorkOrderPriority, string> = {
  low: 'Baja',
  normal: 'Normal',
  high: 'Alta',
}

export const STATUS_COLORS: Record<WorkOrderStatus, string> = {
  pending: 'bg-yellow-100 text-yellow-700',
  in_progress: 'bg-blue-100 text-blue-700',
  waiting_parts: 'bg-orange-100 text-orange-700',
  completed: 'bg-green-100 text-green-700',
  delivered: 'bg-slate-100 text-slate-600',
  cancelled: 'bg-red-100 text-red-600',
}

export interface WorkOrderItemDto {
  id: string
  kind: WorkOrderItemKind
  description: string
  catalogItemId: string | null
  quantity: number
  unitNetPrice: number
  vatRate: number
  lineNet: number
  lineVat: number
  lineTotal: number
  position: number
}

export interface WorkOrderListDto {
  id: string
  number: number
  status: WorkOrderStatus
  priority: WorkOrderPriority
  customerId: string
  customerName: string
  vehicleId: string
  vehiclePlate: string
  vehicleBrand: string | null
  vehicleModel: string | null
  assignedMechanicId: string | null
  assignedMechanicName: string | null
  total: number | null
  balance: number | null
  createdAt: string
}

export interface WorkOrderDto {
  id: string
  number: number
  status: WorkOrderStatus
  priority: WorkOrderPriority
  customerId: string
  customerName: string
  vehicleId: string
  vehiclePlate: string
  vehicleBrand: string | null
  vehicleModel: string | null
  assignedMechanicId: string | null
  assignedMechanicName: string | null
  odometerKm: number
  fuelLevel: number | null
  reportedIssue: string | null
  mechanicNotes: string | null
  internalNotes: string | null
  sourceQuoteId: string | null
  subtotalNet: number | null
  discountAmount: number | null
  vatAmount: number | null
  total: number | null
  paidAmount: number | null
  balance: number | null
  completedAt: string | null
  deliveredAt: string | null
  cancelledAt: string | null
  cancelReason: string | null
  items: WorkOrderItemDto[]
  version: number
  createdAt: string
}

export interface StatusChangeDto {
  id: string
  fromStatus: WorkOrderStatus | null
  toStatus: WorkOrderStatus
  note: string | null
  changedBy: string | null
  changedByName: string | null
  changedAt: string
}

export interface WorkOrderItemRequest {
  kind: WorkOrderItemKind
  description: string
  catalogItemId: string | null
  quantity: number
  unitNetPrice: number
  vatRate: number
}

export interface CreateWorkOrderRequest {
  customerId: string
  vehicleId: string
  odometerKm: number
  fuelLevel?: number | null
  reportedIssue?: string | null
  priority?: WorkOrderPriority
  assignedMechanicId?: string | null
  items?: WorkOrderItemRequest[]
}

export interface UpdateWorkOrderRequest {
  odometerKm: number
  fuelLevel: number | null
  reportedIssue: string | null
  internalNotes: string | null
  priority: WorkOrderPriority
  assignedMechanicId: string | null
  items: WorkOrderItemRequest[]
}

export interface ChangeStatusRequest {
  toStatus: WorkOrderStatus
  note?: string | null
  cancelReason?: string | null
}

export const workOrdersApi = {
  search: (
    status?: WorkOrderStatus | null,
    mechanicId?: string | null,
    vehicleId?: string | null,
    withBalance?: boolean | null,
    from?: string | null,
    to?: string | null,
    page = 1,
    pageSize = 25,
    /** Pestañas del portal del mecánico: "mine" (mías) / "unassigned" (sin asignar). */
    assignment?: 'mine' | 'unassigned' | null,
    /** Orden en el servidor (`number|status|total|balance|createdat`). */
    sort?: { key: string; dir: 'asc' | 'desc' } | null,
  ) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (assignment) params.set('assignment', assignment)
    if (sort) {
      params.set('sort', sort.key)
      params.set('dir', sort.dir)
    }
    if (status) params.set('status', status)
    if (mechanicId) params.set('mechanicId', mechanicId)
    if (vehicleId) params.set('vehicleId', vehicleId)
    if (withBalance != null) params.set('withBalance', String(withBalance))
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    return api.get<Page<WorkOrderListDto>>(`/api/v1/work-orders?${params}`)
  },

  get: (id: string) => api.get<WorkOrderDto>(`/api/v1/work-orders/${id}`),

  create: (body: CreateWorkOrderRequest) =>
    api.post<WorkOrderDto>('/api/v1/work-orders', body, true),

  update: (id: string, body: UpdateWorkOrderRequest, version: number) =>
    api.put<WorkOrderDto>(`/api/v1/work-orders/${id}`, body, String(version)),

  changeStatus: (id: string, body: ChangeStatusRequest) =>
    api.post<WorkOrderDto>(`/api/v1/work-orders/${id}/status`, body, true),

  /** Dueño/administrativo indican el mecánico; el mecánico omite el id y se asigna a sí mismo. */
  assign: (id: string, mechanicId?: string) =>
    api.post<void>(`/api/v1/work-orders/${id}/assign`, mechanicId ? { mechanicId } : {}, true),

  /** Mecánicos activos a los que se puede asignar una orden (dueño y administrativo). */
  listMechanics: () => api.get<AssignableMechanic[]>('/api/v1/work-orders/mechanics'),

  updateMechanicNotes: (id: string, mechanicNotes: string | null) =>
    api.patchJson<void>(`/api/v1/work-orders/${id}/mechanic-notes`, { mechanicNotes }, true),

  getHistory: (id: string) =>
    api.get<StatusChangeDto[]>(`/api/v1/work-orders/${id}/history`),
}
