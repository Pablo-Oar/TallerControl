import { api } from './client'
import type { Page } from './customers'

export type CounterSaleStatus = 'draft' | 'confirmed' | 'cancelled'

export interface CounterSaleItemDto {
  id: string
  catalogItemId: string | null
  description: string
  quantity: number
  unitPrice: number
  vatRate: number
  netAmount: number
  vatAmount: number
  total: number
}

export interface CounterSaleDto {
  id: string
  branchId: string
  customerId: string | null
  documentNumber: number
  date: string
  subtotalNet: number
  vatAmount: number
  total: number
  status: CounterSaleStatus
  accountId: string | null
  items: CounterSaleItemDto[]
  version: number
}

export interface CreateCounterSaleItemRequest {
  catalogItemId?: string | null
  description: string
  quantity: number
  unitPrice: number
  vatRate: number
}

export interface CreateCounterSaleRequest {
  customerId?: string | null
  items: CreateCounterSaleItemRequest[]
}

export interface ConfirmCounterSaleRequest {
  accountId?: string | null
  version: number
}

export const counterSalesApi = {
  search: (q?: string | null, page = 1, pageSize = 25) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (q) params.set('q', q)
    return api.get<Page<CounterSaleDto>>(`/api/v1/counter-sales?${params}`)
  },
  get: (id: string) => api.get<CounterSaleDto>(`/api/v1/counter-sales/${id}`),
  create: (body: CreateCounterSaleRequest) =>
    api.post<CounterSaleDto>('/api/v1/counter-sales', body, true),
  confirm: (id: string, body: ConfirmCounterSaleRequest) =>
    api.post<CounterSaleDto>(`/api/v1/counter-sales/${id}/confirm`, body, true),
  cancel: (id: string) =>
    api.post<CounterSaleDto>(`/api/v1/counter-sales/${id}/cancel`, undefined, true),
  delete: (id: string, reason: string) =>
    api.delete<void>(`/api/v1/counter-sales/${id}`, { reason }, true),
}
