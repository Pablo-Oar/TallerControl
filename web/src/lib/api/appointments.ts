import { api } from './client'

export type AppointmentStatus = 'pending' | 'confirmed' | 'completed' | 'cancelled'

export interface AppointmentServiceDto {
  catalogItemId: string
  catalogItemName: string
  durationMinutes: number
  bufferMinutes: number
}

export interface AppointmentListDto {
  id: string
  customerId: string
  customerName: string
  vehicleId: string
  vehiclePlate: string
  mechanicMembershipId: string | null
  mechanicName: string | null
  scheduledAt: string
  totalDurationMinutes: number
  status: AppointmentStatus
  notes: string | null
}

export interface AppointmentDto extends AppointmentListDto {
  cancellationReason: string | null
  services: AppointmentServiceDto[]
  version: number
}

export interface CreateAppointmentResponse {
  appointment: AppointmentDto
  confirmationToken: string
}

export interface AppointmentServiceRequest {
  catalogItemId: string
  durationMinutes: number
  bufferMinutes: number
}

export interface CreateAppointmentRequest {
  customerId: string
  vehicleId: string
  mechanicMembershipId?: string | null
  scheduledAt: string
  notes?: string | null
  services: AppointmentServiceRequest[]
}

export interface UpdateAppointmentRequest {
  scheduledAt: string
  mechanicMembershipId?: string | null
  notes?: string | null
}

export interface ConvertToWorkOrderRequest {
  odometerKm: number
}

export interface AppointmentsPage {
  items: AppointmentListDto[]
  page: number
  pageSize: number
  total: number
}

export interface AvailabilitySlotDto {
  mechanicMembershipId: string
  mechanicName: string | null
  start: string
  end: string
}

const STATUS_LABELS: Record<AppointmentStatus, string> = {
  pending: 'Pendiente',
  confirmed: 'Confirmado',
  completed: 'Completado',
  cancelled: 'Cancelado',
}

export function getAppointmentStatusLabel(status: AppointmentStatus): string {
  return STATUS_LABELS[status] ?? status
}

export const appointmentsApi = {
  search: (params: {
    date?: string
    mechanicMembershipId?: string
    status?: string
    page?: number
    pageSize?: number
  }) => {
    const q = new URLSearchParams()
    if (params.date) q.set('date', params.date)
    if (params.mechanicMembershipId) q.set('mechanicMembershipId', params.mechanicMembershipId)
    if (params.status) q.set('status', params.status)
    if (params.page) q.set('page', String(params.page))
    if (params.pageSize) q.set('pageSize', String(params.pageSize))
    return api.get<AppointmentsPage>(`/api/v1/appointments?${q}`)
  },

  get: (id: string) => api.get<AppointmentDto>(`/api/v1/appointments/${id}`),

  create: (request: CreateAppointmentRequest) =>
    api.post<CreateAppointmentResponse>('/api/v1/appointments', request, true),

  update: (id: string, version: number, request: UpdateAppointmentRequest) =>
    api.put<AppointmentDto>(`/api/v1/appointments/${id}`, request, String(version)),

  cancel: (id: string, reason: string) =>
    api.delete<void>(`/api/v1/appointments/${id}`, { reason }, true),

  convertToWorkOrder: (id: string, request: ConvertToWorkOrderRequest) =>
    api.post<unknown>(`/api/v1/appointments/${id}/to-work-order`, request, true),

  getAvailabilitySlots: (date: string, requiredMinutes: number) => {
    const q = new URLSearchParams({ date, requiredMinutes: String(requiredMinutes) })
    return api.get<AvailabilitySlotDto[]>(`/api/v1/availability?${q}`)
  },
}
