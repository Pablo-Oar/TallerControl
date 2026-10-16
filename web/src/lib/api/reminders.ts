import { api } from './client'

export type ReminderKind = 'service_due' | 'inspection_due' | 'insurance_due' | 'other_due'

const KIND_LABELS: Record<ReminderKind, string> = {
  service_due: 'Service / Mantenimiento',
  inspection_due: 'VTV / Inspección',
  insurance_due: 'Seguro',
  other_due: 'Otro vencimiento',
}

export function getReminderKindLabel(kind: ReminderKind) {
  return KIND_LABELS[kind] ?? kind
}

export interface ReminderListDto {
  id: string
  vehicleId: string
  vehiclePlate: string
  kind: ReminderKind
  title: string
  dueDate: string | null
  dueKm: number | null
  lastNotifiedAt: string | null
}

export interface ReminderDto extends ReminderListDto {
  notes: string | null
  version: number
}

export interface UpsertReminderRequest {
  vehicleId: string
  kind: ReminderKind
  title: string
  dueDate?: string | null
  dueKm?: number | null
  notes?: string | null
}

export interface RemindersPage {
  items: ReminderListDto[]
  page: number
  pageSize: number
  total: number
}

export const remindersApi = {
  search: (params: { vehicleId?: string; page?: number; pageSize?: number }) => {
    const q = new URLSearchParams()
    if (params.vehicleId) q.set('vehicleId', params.vehicleId)
    if (params.page) q.set('page', String(params.page))
    if (params.pageSize) q.set('pageSize', String(params.pageSize))
    return api.get<RemindersPage>(`/api/v1/reminders?${q}`)
  },

  get: (id: string) => api.get<ReminderDto>(`/api/v1/reminders/${id}`),

  create: (request: UpsertReminderRequest) =>
    api.post<ReminderDto>('/api/v1/reminders', request, true),

  update: (id: string, version: number, request: UpsertReminderRequest) =>
    api.put<ReminderDto>(`/api/v1/reminders/${id}`, request, String(version)),

  delete: (id: string) => api.delete<void>(`/api/v1/reminders/${id}`, undefined, true),

  evaluate: () => api.post<{ notificationsCreated: number }>('/api/v1/reminders/evaluate', {}, true),
}
