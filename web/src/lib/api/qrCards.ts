import { api } from './client'

export type QrCardStatus = 'free' | 'assigned' | 'lost' | 'revoked'
export type QrScanEventType = 'scan' | 'owner_challenge' | 'otp_sent' | 'otp_failed' | 'owner_session' | 'found_keys'

export interface QrCardListDto {
  id: string
  code: string
  status: QrCardStatus
  vehicleId: string | null
  vehiclePlate: string | null
  assignedAt: string | null
  revokedAt: string | null
}

export interface QrCardDto {
  id: string
  code: string
  status: QrCardStatus
  vehicleId: string | null
  vehiclePlate: string | null
  vehicleBrand: string | null
  vehicleModel: string | null
  assignedAt: string | null
  revokedAt: string | null
  revokeReason: string | null
}

export interface QrEventDto {
  id: string
  eventType: QrScanEventType
  outcome: string | null
  ipTruncated: string | null
  createdAt: string
}

export interface Page<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface GenerateBatchResult {
  batchId: string
  count: number
}

export interface ScanResultDto {
  workshop: { name: string; logoUrl: string | null }
  cardState: string
}

export interface OwnerChallengeResult {
  challengeId: string
  channel: string
  maskedDestination: string | null
}

export interface OwnerVehicleDto {
  plate: string
  brand: string | null
  model: string | null
  odometerKm: number
  workOrders: OwnerWorkOrderDto[]
  activeQuotes: OwnerQuoteDto[]
  pendingBalance: number
}

export interface OwnerWorkOrderDto {
  number: number
  status: string
  createdAt: string
  completedAt: string | null
  total: number | null
}

export interface OwnerQuoteDto {
  number: number
  status: string
  publicLink: string | null
  validUntil: string | null
}

// ── Endpoints privados (panel del taller) ─────────────────────────────────────

export const searchQrCards = (params: {
  status?: QrCardStatus
  q?: string
  page?: number
  pageSize?: number
}) => {
  const qs = new URLSearchParams()
  if (params.status) qs.set('status', params.status)
  if (params.q) qs.set('q', params.q)
  if (params.page) qs.set('page', String(params.page))
  if (params.pageSize) qs.set('pageSize', String(params.pageSize))
  return api.get<Page<QrCardListDto>>(`/api/v1/qr/cards?${qs}`)
}

export const getQrCard = (id: string) => api.get<QrCardDto>(`/api/v1/qr/cards/${id}`)

export const generateBatch = (quantity: number) =>
  api.post<GenerateBatchResult>('/api/v1/qr/cards/batch', { quantity }, true)

export const assignQrCard = (id: string, vehicleId: string) =>
  api.post<QrCardDto>(`/api/v1/qr/cards/${id}/assign`, { vehicleId }, true)

export const assignByCode = (code: string, vehicleId: string) =>
  api.post<QrCardDto>('/api/v1/qr/cards/assign-by-code', { code, vehicleId }, true)

export const unassignQrCard = (id: string) =>
  api.delete<void>(`/api/v1/qr/cards/${id}/assign`, undefined, true)

export const markLost = (id: string, lost: boolean) =>
  api.patchJson<void>(`/api/v1/qr/cards/${id}/lost`, { lost }, true)

export const revokeQrCard = (id: string, reason: string) =>
  api.post<void>(`/api/v1/qr/cards/${id}/revoke`, { reason }, true)

export const getQrCardPrintUrl = (id: string) => `/api/v1/qr/cards/${id}/print?format=card`

export const getQrCardEvents = (id: string, page = 1, pageSize = 10) =>
  api.get<Page<QrEventDto>>(
    `/api/v1/qr/cards/${id}/events?page=${page}&pageSize=${pageSize}`,
  )

export const getOwnerAccessCode = (customerId: string) =>
  api.post<{ code: string }>(`/api/v1/customers/${customerId}/owner-access-code`, {}, true)

// ── Endpoints públicos (portal del dueño) ─────────────────────────────────────

export const scanQrCode = (code: string) =>
  api.get<ScanResultDto>(`/api/public/v1/qr/${code}`)

export async function startOwnerChallenge(
  code: string,
  plate: string,
  customerCode: string | null,
  turnstileToken: string,
): Promise<OwnerChallengeResult> {
  const res = await fetch(`/api/public/v1/qr/${code}/owner-challenge`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ plate, customerCode, turnstileToken }),
  })
  if (!res.ok) throw new Error(await res.text())
  return res.json() as Promise<OwnerChallengeResult>
}

export async function validateOwnerOtp(
  code: string,
  challengeId: string,
  otp: string,
): Promise<void> {
  const res = await fetch(`/api/public/v1/qr/${code}/owner-session`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ challengeId, otp }),
  })
  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    const msg =
      (body as { errors?: { '': string[] } })?.errors?.['']?.[0] ??
      (body as { title?: string })?.title ??
      'Código incorrecto.'
    throw new Error(msg)
  }
}

export const getOwnerVehicle = (code: string) =>
  api.get<OwnerVehicleDto>(`/api/public/v1/qr/${code}/owner-vehicle`)

// ── Panel del taller: búsqueda rápida por código ──────────────────────────────

export interface QrVehicleSummaryDto {
  qrCardId: string
  code: string
  status: QrCardStatus
  vehicleId: string | null
  vehiclePlate: string | null
  vehicleBrand: string | null
  vehicleModel: string | null
  customerName: string | null
}

export const getVehicleSummaryByCode = (code: string) =>
  api.get<QrVehicleSummaryDto>(`/api/v1/qr/cards/scan/${encodeURIComponent(code)}`)

// ── Público: reportar llaves encontradas ─────────────────────────────────────

export async function reportFoundKeys(
  code: string,
  reporterName: string | null,
  reporterPhone: string | null,
): Promise<void> {
  const res = await fetch(`/api/public/v1/qr/${code}/found-keys`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reporterName, reporterPhone }),
  })
  if (!res.ok && res.status !== 429) {
    throw new Error('No se pudo enviar el aviso.')
  }
}
