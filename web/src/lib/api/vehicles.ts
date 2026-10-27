import { api } from './client'
import type { Page } from './customers'

export type FuelType = 'gasoline' | 'diesel' | 'electric' | 'hybrid' | 'gas' | 'other'
export type VehicleUsageProfile = 'regular' | 'low_usage' | 'commercial'

export interface VehicleListDto {
  id: string
  plate: string
  brand: string | null
  model: string | null
  year: number | null
  customerName: string
  isDeleted?: boolean
}

export interface VehicleHistoryEntryDto {
  type: 'work_order' | 'quote'
  id: string
  /** "OT-0003" / "P-0001". */
  number: string
  date: string
  odometerKm: number | null
  status: string
  total: number | null
}

export interface VehicleDto {
  id: string
  plate: string
  customerId: string
  customerName: string
  brand: string | null
  model: string | null
  versionName: string | null
  color: string | null
  year: number | null
  vin: string | null
  fuel: FuelType | null
  odometerKm: number
  usageProfile: VehicleUsageProfile | null
  engineNumber: string | null
  activeQrCard: null
  version: number
}

export interface VehicleUpsertRequest {
  plate: string
  customerId: string
  brand?: string | null
  model?: string | null
  version?: string | null
  color?: string | null
  year?: number | null
  vin?: string | null
  fuel?: FuelType | null
  odometerKm: number
  usageProfile?: VehicleUsageProfile | null
  engineNumber?: string | null
}

export const vehiclesApi = {
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
    return api.get<Page<VehicleListDto>>(`/api/v1/vehicles?${params}`)
  },
  get: (id: string) => api.get<VehicleDto>(`/api/v1/vehicles/${id}`),
  /** Órdenes y presupuestos del vehículo por fecha descendente (REQ-07). El total es null sin prices.view. */
  history: (id: string, page = 1, pageSize = 10) =>
    api.get<Page<VehicleHistoryEntryDto>>(`/api/v1/vehicles/${id}/history?page=${page}&pageSize=${pageSize}`),
  create: (body: VehicleUpsertRequest) => api.post<VehicleDto>('/api/v1/vehicles', body, true),
  update: (id: string, body: VehicleUpsertRequest, version: number) =>
    api.put<VehicleDto>(`/api/v1/vehicles/${id}`, body, String(version)),
  delete: (id: string, reason: string) =>
    api.delete<void>(`/api/v1/vehicles/${id}`, { reason }, true),
  restore: (id: string) => api.post<VehicleDto>(`/api/v1/vehicles/${id}/restore`, undefined, true),
}
