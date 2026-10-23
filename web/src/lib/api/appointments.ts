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
  /** Solo el Dueño: agendar igual fuera de horario o encimado con otro turno. */
  force?: boolean
  customerId: string
  vehicleId: string
  mechanicMembershipId?: string | null
  scheduledAt: string
  notes?: string | null
  services: AppointmentServiceRequest[]
}

export interface UpdateAppointmentRequest {
  force?: boolean
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
    /** Rango de días del taller, inclusivo (calendario semanal). */
    from?: string
    to?: string
    mechanicMembershipId?: string
    status?: string
    page?: number
    pageSize?: number
  }) => {
    const q = new URLSearchParams()
    if (params.date) q.set('date', params.date)
    if (params.from) q.set('from', params.from)
    if (params.to) q.set('to', params.to)
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

  /** Emite un enlace nuevo para el cliente (el anterior deja de servir). Devuelve la dirección completa. */
  publicLink: async (id: string) => {
    const { token } = await api.post<{ token: string }>(`/api/v1/appointments/${id}/public-link`, {}, true)
    return `${window.location.origin}/t/${token}`
  },

  convertToWorkOrder: (id: string, request: ConvertToWorkOrderRequest) =>
    api.post<{ id: string; number: number }>(`/api/v1/appointments/${id}/to-work-order`, request, true),

  getAvailabilitySlots: (date: string, requiredMinutes: number) => {
    const q = new URLSearchParams({ date, requiredMinutes: String(requiredMinutes) })
    return api.get<AvailabilitySlotDto[]>(`/api/v1/availability?${q}`)
  },
}

// ── Horarios y excepciones (A-04) ────────────────────────────────────────────

export type WeekDay = 'monday' | 'tuesday' | 'wednesday' | 'thursday' | 'friday' | 'saturday' | 'sunday'

/** Orden de la semana para mostrar (lunes primero). */
export const WEEK_DAYS: { value: WeekDay; label: string; short: string }[] = [
  { value: 'monday', label: 'Lunes', short: 'Lun' },
  { value: 'tuesday', label: 'Martes', short: 'Mar' },
  { value: 'wednesday', label: 'Miércoles', short: 'Mié' },
  { value: 'thursday', label: 'Jueves', short: 'Jue' },
  { value: 'friday', label: 'Viernes', short: 'Vie' },
  { value: 'saturday', label: 'Sábado', short: 'Sáb' },
  { value: 'sunday', label: 'Domingo', short: 'Dom' },
]

export interface AvailabilityRuleDto {
  id: string
  mechanicMembershipId: string
  mechanicName: string
  dayOfWeek: WeekDay
  startTime: string
  endTime: string
}

export interface AvailabilityExceptionDto {
  id: string
  mechanicMembershipId: string | null
  mechanicName: string | null
  date: string
  isBlocked: boolean
  startTime: string | null
  endTime: string | null
  reason: string | null
}

export interface UpsertAvailabilityRuleRequest {
  mechanicMembershipId: string
  dayOfWeek: WeekDay
  startTime: string
  endTime: string
}

export interface CreateAvailabilityExceptionRequest {
  mechanicMembershipId?: string | null
  date: string
  isBlocked: boolean
  startTime?: string | null
  endTime?: string | null
  reason?: string | null
}

/** "09:00:00" → "09:00" */
export const hhmm = (t: string | null) => (t ? t.slice(0, 5) : '')

export const availabilityApi = {
  rules: (mechanicMembershipId?: string) =>
    api.get<AvailabilityRuleDto[]>(`/api/v1/availability/rules${mechanicMembershipId ? `?mechanicMembershipId=${mechanicMembershipId}` : ''}`),
  createRule: (request: UpsertAvailabilityRuleRequest) =>
    api.post<AvailabilityRuleDto>('/api/v1/availability/rules', request, true),
  deleteRule: (id: string) => api.delete<void>(`/api/v1/availability/rules/${id}`, undefined, true),
  exceptions: (from: string, to: string) =>
    api.get<AvailabilityExceptionDto[]>(`/api/v1/availability/exceptions?from=${from}&to=${to}`),
  createException: (request: CreateAvailabilityExceptionRequest) =>
    api.post<AvailabilityExceptionDto>('/api/v1/availability/exceptions', request, true),
  deleteException: (id: string) => api.delete<void>(`/api/v1/availability/exceptions/${id}`, undefined, true),
}

// ── Página pública del turno (A-03) ──────────────────────────────────────────

export interface PublicAppointmentDto {
  scheduledAt: string
  timeZone: string
  totalDurationMinutes: number
  status: AppointmentStatus
  vehicleLabel: string | null
  services: string[]
}

export const publicAppointmentApi = {
  get: (token: string) => api.get<PublicAppointmentDto>(`/api/public/v1/appointments/${token}`),
  confirm: (token: string) => api.post<{ message: string }>(`/api/public/v1/appointments/${token}/confirm`, {}, false),
  decline: (token: string) => api.post<{ message: string }>(`/api/public/v1/appointments/${token}/decline`, {}, false),
}
