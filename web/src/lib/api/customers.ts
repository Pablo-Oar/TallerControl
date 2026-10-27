import { api } from './client'

export type VatCondition = 'consumidor_final' | 'responsable_inscripto' | 'monotributo' | 'exento' | 'no_responsable'
export type DocType = 'dni' | 'cuit' | 'cuil' | 'pasaporte' | 'otro'

export interface Page<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  total: number
}

export interface CustomerListDto {
  id: string
  name: string
  email: string | null
  phone: string | null
  docNumber: string | null
  balance: number
  isDeleted?: boolean
}

export interface VehicleBasicDto {
  id: string
  plate: string
  brand: string | null
  model: string | null
}

export interface CustomerDto {
  id: string
  name: string
  email: string | null
  phone: string | null
  docType: DocType | null
  docNumber: string | null
  vatCondition: VatCondition
  address: string | null
  doNotContact: boolean
  notes: string | null
  balance: number
  vehicles: VehicleBasicDto[]
  version: number
}

export interface CustomerUpsertRequest {
  name: string
  email?: string | null
  phone?: string | null
  docType?: DocType | null
  docNumber?: string | null
  vatCondition: VatCondition
  address?: string | null
  doNotContact: boolean
  notes?: string | null
}

export const customersApi = {
  search: (
    q?: string | null,
    page = 1,
    pageSize = 10,
    includeDeleted = false,
    /** Orden en el servidor (ver los nombres de columna del listado). */
    sort?: { key: string; dir: 'asc' | 'desc' } | null,
  ) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (q) params.set('q', q)
    if (includeDeleted) params.set('includeDeleted', 'true')
    if (sort) {
      params.set('sort', sort.key)
      params.set('dir', sort.dir)
    }
    return api.get<Page<CustomerListDto>>(`/api/v1/customers?${params}`)
  },
  get: (id: string) => api.get<CustomerDto>(`/api/v1/customers/${id}`),
  create: (body: CustomerUpsertRequest) => api.post<CustomerDto>('/api/v1/customers', body, true),
  update: (id: string, body: CustomerUpsertRequest, version: number) =>
    api.put<CustomerDto>(`/api/v1/customers/${id}`, body, String(version)),
  delete: (id: string, reason: string) =>
    api.delete<void>(`/api/v1/customers/${id}`, { reason }, true),
  restore: (id: string) => api.post<CustomerDto>(`/api/v1/customers/${id}/restore`, undefined, true),
}
