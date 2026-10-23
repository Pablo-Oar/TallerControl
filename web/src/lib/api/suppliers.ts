import { api } from './client'
import type { Page } from './customers'

export type VatCondition = 'responsable_inscripto' | 'monotributo' | 'exento' | 'consumidor_final' | 'no_responsable'

export interface SupplierDto {
  id: string
  name: string
  cuit: string | null
  vatCondition: VatCondition
  address: string | null
  phone: string | null
  email: string | null
  notes: string | null
  version: number
  /** Compras vigentes (borrador o confirmadas): el detalle lo informa para avisar antes de dar de baja. */
  purchaseCount?: number
}

export interface CreateSupplierRequest {
  name: string
  cuit?: string | null
  vatCondition: VatCondition
  address?: string | null
  phone?: string | null
  email?: string | null
  notes?: string | null
}

export interface UpdateSupplierRequest {
  name: string
  cuit?: string | null
  vatCondition: VatCondition
  address?: string | null
  phone?: string | null
  email?: string | null
  notes?: string | null
}

export const suppliersApi = {
  search: (q?: string | null, page = 1, pageSize = 25, deleted = false) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (q) params.set('q', q)
    if (deleted) params.set('deleted', 'true')
    return api.get<Page<SupplierDto>>(`/api/v1/suppliers?${params}`)
  },
  get: (id: string) => api.get<SupplierDto>(`/api/v1/suppliers/${id}`),
  create: (body: CreateSupplierRequest) => api.post<SupplierDto>('/api/v1/suppliers', body, true),
  update: (id: string, body: UpdateSupplierRequest, version: number) =>
    api.put<SupplierDto>(`/api/v1/suppliers/${id}`, body, String(version)),
  delete: (id: string, reason: string) =>
    api.delete<void>(`/api/v1/suppliers/${id}`, { reason }, true),
  restore: (id: string) => api.post<SupplierDto>(`/api/v1/suppliers/${id}/restore`, undefined, true),
}
