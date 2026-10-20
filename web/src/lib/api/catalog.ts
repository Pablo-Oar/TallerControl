import { api } from './client'
import type { Page } from './customers'

export type CatalogItemKind = 'service' | 'product'

export interface CatalogItemListDto {
  id: string
  kind: CatalogItemKind
  sku: string | null
  name: string
  netPrice: number
  vatRate: number
  isActive: boolean
  trackStock: boolean
  stockQty: number
  isLowStock: boolean
}

export interface StockMovementDto {
  id: string
  quantity: number
  reason: string
  note: string | null
  createdAt: string
  branchId: string
}

export interface CatalogItemDto {
  id: string
  kind: CatalogItemKind
  sku: string | null
  name: string
  description: string | null
  netPrice: number
  vatRate: number
  isActive: boolean
  trackStock: boolean
  stockQty: number
  minStockQty: number | null
  maxStockQty: number | null
  /** El API devuelve el mínimo como `stockMin`. */
  stockMin?: number | null
  /** Duración y buffer en minutos (servicios): los usa la agenda al reservar. */
  durationMinutes?: number | null
  bufferMinutes?: number | null
  isLowStock: boolean
  version: number
}

export interface CreateCatalogItemRequest {
  kind: CatalogItemKind
  sku?: string | null
  name: string
  description?: string | null
  netPrice: number
  vatRate: number
  trackStock: boolean
  minStockQty?: number | null
  maxStockQty?: number | null
  stockMin?: number | null
  durationMinutes?: number | null
  bufferMinutes?: number | null
  isActive?: boolean
}

export interface UpdateCatalogItemRequest {
  name: string
  description?: string | null
  netPrice: number
  vatRate: number
  trackStock: boolean
  minStockQty?: number | null
  maxStockQty?: number | null
  stockMin?: number | null
  durationMinutes?: number | null
  bufferMinutes?: number | null
  isActive: boolean
}

export interface ManualStockAdjustmentRequest {
  quantity: number
  note: string
}

export const catalogApi = {
  search: (q?: string | null, kind?: CatalogItemKind | null, activeOnly?: boolean | null, page = 1, pageSize = 25) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (q) params.set('q', q)
    if (kind) params.set('kind', kind)
    if (activeOnly != null) params.set('activeOnly', String(activeOnly))
    return api.get<Page<CatalogItemListDto>>(`/api/v1/catalog?${params}`)
  },
  get: (id: string) => api.get<CatalogItemDto>(`/api/v1/catalog/${id}`),
  create: (body: CreateCatalogItemRequest) => api.post<CatalogItemDto>('/api/v1/catalog', body, true),
  update: (id: string, body: UpdateCatalogItemRequest, version: number) =>
    api.put<CatalogItemDto>(`/api/v1/catalog/${id}`, body, String(version)),
  delete: (id: string, reason: string) =>
    api.delete<void>(`/api/v1/catalog/${id}`, { reason }, true),
  restore: (id: string) => api.post<CatalogItemDto>(`/api/v1/catalog/${id}/restore`, undefined, true),
  getMovements: (id: string, page = 1, pageSize = 25) =>
    api.get<StockMovementDto[]>(`/api/v1/catalog/${id}/stock-movements?page=${page}&pageSize=${pageSize}`),
  stockAdjust: (id: string, body: ManualStockAdjustmentRequest) =>
    api.post<CatalogItemDto>(`/api/v1/catalog/${id}/stock-adjustments`, body, true),
}
