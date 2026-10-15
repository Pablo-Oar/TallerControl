import { api } from './client'
import type { Page } from './customers'

export type PurchaseStatus = 'draft' | 'confirmed' | 'cancelled'

export interface PurchaseItemDto {
  id: string
  catalogItemId: string | null
  description: string
  quantity: number
  unitCost: number
  vatRate: number
  total: number
}

export interface PurchaseDto {
  id: string
  branchId: string
  supplierId: string
  documentNumber: number
  invoiceDate: string
  pointOfSale: number | null
  invoiceNumber: number | null
  subtotalNet: number
  vatAmount: number
  perceptions: number
  retentions: number
  total: number
  isExcludedFromCiti: boolean
  status: PurchaseStatus
  accountId: string | null
  items: PurchaseItemDto[]
  version: number
}

export interface CreatePurchaseItemRequest {
  catalogItemId?: string | null
  description: string
  quantity: number
  unitCost: number
  vatRate: number
}

export interface CreatePurchaseRequest {
  supplierId: string
  invoiceDate: string
  pointOfSale?: number | null
  invoiceNumber?: number | null
  perceptions?: number
  retentions?: number
  items: CreatePurchaseItemRequest[]
}

export interface ConfirmPurchaseRequest {
  accountId?: string | null
  version: number
}

export const purchasesApi = {
  search: (q?: string | null, page = 1, pageSize = 25) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (q) params.set('q', q)
    return api.get<Page<PurchaseDto>>(`/api/v1/purchases?${params}`)
  },
  get: (id: string) => api.get<PurchaseDto>(`/api/v1/purchases/${id}`),
  create: (body: CreatePurchaseRequest) => api.post<PurchaseDto>('/api/v1/purchases', body, true),
  confirm: (id: string, body: ConfirmPurchaseRequest) =>
    api.post<PurchaseDto>(`/api/v1/purchases/${id}/confirm`, body, true),
  cancel: (id: string) => api.post<PurchaseDto>(`/api/v1/purchases/${id}/cancel`, undefined, true),
  delete: (id: string, reason: string) =>
    api.delete<void>(`/api/v1/purchases/${id}`, { reason }, true),
}
