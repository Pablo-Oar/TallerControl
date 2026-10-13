import { api } from './client'
import type { Page } from './customers'

export type FuelType = 'gasoline' | 'diesel' | 'electric' | 'hybrid' | 'gas' | 'other'

export interface VehicleListDto {
  id: string
  plate: string
  brand: string | null
  model: string | null
  year: number | null
  customerName: string
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
}

export const vehiclesApi = {
  search: (q?: string | null, page = 1, pageSize = 25) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (q) params.set('q', q)
    return api.get<Page<VehicleListDto>>(`/api/v1/vehicles?${params}`)
  },
  get: (id: string) => api.get<VehicleDto>(`/api/v1/vehicles/${id}`),
  create: (body: VehicleUpsertRequest) => api.post<VehicleDto>('/api/v1/vehicles', body, true),
  update: (id: string, body: VehicleUpsertRequest, version: number) =>
    api.put<VehicleDto>(`/api/v1/vehicles/${id}`, body, String(version)),
  delete: (id: string, reason: string) =>
    api.delete<void>(`/api/v1/vehicles/${id}`, { reason }, true),
}
