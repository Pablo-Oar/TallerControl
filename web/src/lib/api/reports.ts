import { api } from './client'

export interface RevenueReportRow {
  date: string
  amount: number
  paymentCount: number
}
export interface RevenueReportDto {
  rows: RevenueReportRow[]
  total: number
  count: number
}

export interface ReceivablesReportRow {
  workOrderId: string
  workOrderNumber: number
  customerName: string
  vehiclePlate: string
  balance: number
  createdAt: string
}
export interface ReceivablesReportDto {
  rows: ReceivablesReportRow[]
  total: number
}

export interface StockReportRow {
  catalogItemId: string
  name: string
  sku: string | null
  kind: string
  stockQty: number
  stockMin: number | null
  unitCost: number
  isLowStock: boolean
}
export interface StockReportDto {
  rows: StockReportRow[]
  lowStockCount: number
}

export interface PurchasesReportRow {
  purchaseId: string
  purchaseNumber: number
  supplierName: string | null
  date: string
  total: number
  status: string
}
export interface PurchasesReportDto {
  rows: PurchasesReportRow[]
  total: number
  count: number
}

function buildDateParams(from: string, to: string) {
  return new URLSearchParams({ from, to })
}

export const reportsApi = {
  getRevenue: (from: string, to: string) =>
    api.get<RevenueReportDto>(`/api/v1/reports/revenue?${buildDateParams(from, to)}`),

  getReceivables: () =>
    api.get<ReceivablesReportDto>('/api/v1/reports/receivables'),

  getStock: () =>
    api.get<StockReportDto>('/api/v1/reports/stock'),

  getPurchases: (from: string, to: string) =>
    api.get<PurchasesReportDto>(`/api/v1/reports/purchases?${buildDateParams(from, to)}`),

  exportUrl: (report: 'revenue' | 'receivables' | 'stock' | 'purchases', from?: string, to?: string) => {
    const params = new URLSearchParams({ format: 'xlsx' })
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    return `/api/v1/reports/${report}?${params}`
  },
}
