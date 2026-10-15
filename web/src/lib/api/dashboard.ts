import { api } from './client'

export interface DashboardDto {
  totalReceivable: number
  openWorkOrders: number
  pendingQuotes: number
  lowStockItems: number
}

export function getDashboard(): Promise<DashboardDto> {
  return api.get<DashboardDto>('/api/v1/dashboard')
}
