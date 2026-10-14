import { api } from './client'
import type { Page } from './customers'

export type QuoteStatus =
  | 'draft'
  | 'sent'
  | 'approved'
  | 'partially_approved'
  | 'rejected'
  | 'expired'
  | 'converted'
  | 'cancelled'

export type QuoteItemKind = 'service' | 'product' | 'manual'

export const QUOTE_STATUS_LABELS: Record<QuoteStatus, string> = {
  draft: 'Borrador',
  sent: 'Enviado',
  approved: 'Aprobado',
  partially_approved: 'Aprobado parcialmente',
  rejected: 'Rechazado',
  expired: 'Vencido',
  converted: 'Convertido',
  cancelled: 'Cancelado',
}

export const QUOTE_STATUS_COLORS: Record<QuoteStatus, string> = {
  draft: 'bg-slate-100 text-slate-600',
  sent: 'bg-blue-100 text-blue-700',
  approved: 'bg-green-100 text-green-700',
  partially_approved: 'bg-teal-100 text-teal-700',
  rejected: 'bg-red-100 text-red-600',
  expired: 'bg-orange-100 text-orange-700',
  converted: 'bg-purple-100 text-purple-700',
  cancelled: 'bg-slate-100 text-slate-500',
}

export interface QuoteItemDto {
  id: string
  kind: QuoteItemKind
  description: string
  catalogItemId: string | null
  quantity: number
  unitNetPrice: number
  vatRate: number
  lineNet: number
  lineVat: number
  lineTotal: number
  position: number
  requiredForApproval: boolean
}

export interface QuoteListDto {
  id: string
  number: number
  status: QuoteStatus
  customerId: string
  customerName: string
  vehicleId: string
  vehiclePlate: string
  total: number
  globalDiscountPct: number
  validUntil: string
  createdAt: string
}

export interface QuoteDto {
  id: string
  number: number
  status: QuoteStatus
  customerId: string
  customerName: string
  vehicleId: string
  vehiclePlate: string
  vehicleBrand: string | null
  vehicleModel: string | null
  globalDiscountPct: number
  subtotalNet: number
  discountAmount: number
  vatAmount: number
  total: number
  approvedTotal: number | null
  validUntil: string
  customerNotes: string | null
  internalNotes: string | null
  workOrderId: string | null
  sentAt: string | null
  convertedAt: string | null
  cancelReason: string | null
  items: QuoteItemDto[]
  version: number
  createdAt: string
}

export interface QuoteResponseItemDto {
  quoteItemId: string
  approved: boolean
}

export interface QuoteResponseDto {
  id: string
  decision: string
  signerName: string
  approvedTotal: number
  comment: string | null
  isCurrent: boolean
  createdAt: string
  items: QuoteResponseItemDto[]
}

export interface QuoteItemRequest {
  kind: QuoteItemKind
  description: string
  catalogItemId: string | null
  quantity: number
  unitNetPrice: number
  vatRate: number
  requiredForApproval?: boolean
}

export interface CreateQuoteRequest {
  customerId: string
  vehicleId: string
  validUntil?: string | null
  globalDiscountPct?: number
  customerNotes?: string | null
  internalNotes?: string | null
  items?: QuoteItemRequest[]
}

export interface UpdateQuoteRequest {
  customerId: string
  vehicleId: string
  validUntil: string
  globalDiscountPct: number
  customerNotes: string | null
  internalNotes: string | null
  items: QuoteItemRequest[]
}

export interface ConvertQuoteRequest {
  odometerKm: number
  fuelLevel?: number | null
  reportedIssue?: string | null
}

// ── Público ──────────────────────────────────────────────────────────────────

export interface PublicQuoteItemDto {
  id: string
  kind: QuoteItemKind
  description: string
  quantity: number
  unitNetPrice: number
  vatRate: number
  lineNet: number
  lineVat: number
  lineTotal: number
  position: number
  requiredForApproval: boolean
}

export interface PublicQuoteDto {
  id: string
  number: number
  status: QuoteStatus
  customerName: string
  vehiclePlate: string
  vehicleBrand: string | null
  vehicleModel: string | null
  globalDiscountPct: number
  subtotalNet: number
  discountAmount: number
  vatAmount: number
  total: number
  validUntil: string
  customerNotes: string | null
  items: PublicQuoteItemDto[]
  contentHash: string
  createdAt: string
}

export interface SubmitPublicResponseRequest {
  decision: 'approved' | 'rejected'
  signerName: string
  contentHash: string
  comment?: string | null
  items: { quoteItemId: string; approved: boolean }[]
}

export interface PublicWorkOrderDto {
  id: string
  number: number
  status: string
  customerName: string
  vehiclePlate: string
  vehicleBrand: string | null
  vehicleModel: string | null
  assignedMechanicName: string | null
  reportedIssue: string | null
  mechanicNotes: string | null
  createdAt: string
}

export const quotesApi = {
  search: (
    status?: QuoteStatus | null,
    customerId?: string | null,
    vehicleId?: string | null,
    from?: string | null,
    to?: string | null,
    page = 1,
    pageSize = 25,
  ) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (status) params.set('status', status)
    if (customerId) params.set('customerId', customerId)
    if (vehicleId) params.set('vehicleId', vehicleId)
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    return api.get<Page<QuoteListDto>>(`/api/v1/quotes?${params}`)
  },

  get: (id: string) => api.get<QuoteDto>(`/api/v1/quotes/${id}`),

  create: (body: CreateQuoteRequest) => api.post<QuoteDto>('/api/v1/quotes', body, true),

  update: (id: string, body: UpdateQuoteRequest, version: number) =>
    api.put<QuoteDto>(`/api/v1/quotes/${id}`, body, String(version)),

  send: (id: string) => api.post<QuoteDto>(`/api/v1/quotes/${id}/send`, {}, true),

  reopen: (id: string, reason?: string | null) =>
    api.post<QuoteDto>(`/api/v1/quotes/${id}/reopen`, { reason }, true),

  convert: (id: string, body: ConvertQuoteRequest) =>
    api.post<unknown>(`/api/v1/quotes/${id}/convert`, body, true),

  cancel: (id: string, reason: string) =>
    api.post<QuoteDto>(`/api/v1/quotes/${id}/cancel`, { reason }, true),

  delete: (id: string, reason?: string | null) =>
    api.delete(`/api/v1/quotes/${id}`, { reason: reason ?? null }, true),

  getResponses: (id: string) =>
    api.get<QuoteResponseDto[]>(`/api/v1/quotes/${id}/responses`),

  getPdfUrl: (id: string) => `/api/v1/quotes/${id}/pdf`,

  getPublicLink: (workOrderId: string) =>
    api.get<{ tokenBase64Url: string; expiresAt: string }>(
      `/api/v1/work-orders/${workOrderId}/public-link`,
    ),
}

// ── Endpoints públicos (anónimos) ─────────────────────────────────────────────

export const publicApi = {
  getQuote: (token: string) =>
    api.get<PublicQuoteDto>(`/api/public/v1/quotes/${token}`),

  submitResponse: (token: string, body: SubmitPublicResponseRequest) =>
    api.post<unknown>(`/api/public/v1/quotes/${token}/responses`, body, false),

  getWorkOrder: (token: string) =>
    api.get<PublicWorkOrderDto>(`/api/public/v1/work-orders/${token}`),
}
