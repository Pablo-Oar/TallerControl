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
  stockMin: number | null
  stockMax: number | null
  /** Costo unitario: solo viaja para quien tiene el permiso de costos (`costs.view`). */
  cost?: number | null
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
  cost?: number | null
  stockMin?: number | null
  stockMax?: number | null
  durationMinutes?: number | null
  bufferMinutes?: number | null
  /** Solo productos con control de stock: existencias de partida (quedan como un movimiento). */
  initialStockQty?: number | null
}

export interface UpdateCatalogItemRequest {
  name: string
  /** Se reenvía el actual: el servidor reemplaza el valor, así que omitirlo lo borraría. */
  sku?: string | null
  /** Sin permiso de costos el servidor ignora este campo y conserva el actual. */
  cost?: number | null
  description?: string | null
  netPrice: number
  vatRate: number
  /** Solo productos: activa o desactiva el control de stock. */
  trackStock?: boolean
  /** Al activar el control de stock: existencias de partida (queda como un movimiento). */
  initialStockQty?: number | null
  stockMin?: number | null
  stockMax?: number | null
  durationMinutes?: number | null
  bufferMinutes?: number | null
  isActive: boolean
}

export interface ManualStockAdjustmentRequest {
  quantity: number
  note: string
}

export const catalogApi = {
  search: (
    q?: string | null,
    kind?: CatalogItemKind | null,
    activeOnly?: boolean | null,
    page = 1,
    pageSize = 10,
    /** Orden en el servidor (ver los nombres de columna del listado). */
    sort?: { key: string; dir: 'asc' | 'desc' } | null,
    /** true = solo los dados de baja («Ver eliminados»). */
    deleted?: boolean,
  ) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (q) params.set('q', q)
    if (kind) params.set('kind', kind)
    if (activeOnly != null) params.set('activeOnly', String(activeOnly))
    if (deleted) params.set('deleted', 'true')
    if (sort) {
      params.set('sort', sort.key)
      params.set('dir', sort.dir)
    }
    return api.get<Page<CatalogItemListDto>>(`/api/v1/catalog?${params}`)
  },
  get: (id: string) => api.get<CatalogItemDto>(`/api/v1/catalog/${id}`),
  create: (body: CreateCatalogItemRequest) => api.post<CatalogItemDto>('/api/v1/catalog', body, true),
  update: (id: string, body: UpdateCatalogItemRequest, version: number) =>
    api.put<CatalogItemDto>(`/api/v1/catalog/${id}`, body, String(version)),
  delete: (id: string, reason: string) =>
    api.delete<void>(`/api/v1/catalog/${id}`, { reason }, true),
  restore: (id: string) => api.post<CatalogItemDto>(`/api/v1/catalog/${id}/restore`, undefined, true),
  getMovements: (id: string, page = 1, pageSize = 10) =>
    api.get<StockMovementDto[]>(`/api/v1/catalog/${id}/stock-movements?page=${page}&pageSize=${pageSize}`),
  stockAdjust: (id: string, body: ManualStockAdjustmentRequest) =>
    api.post<CatalogItemDto>(`/api/v1/catalog/${id}/stock-adjustments`, body, true),
}

export const STOCK_REASON_LABELS: Record<string, string> = {
  work_order: 'Orden de trabajo',
  work_order_cancel: 'Orden anulada (repone)',
  item_removed: 'Renglón quitado de la orden',
  manual_adjust: 'Ajuste manual',
  purchase: 'Compra',
  purchase_cancel: 'Compra anulada',
  counter_sale: 'Venta de mostrador',
  counter_sale_cancel: 'Venta anulada',
}
