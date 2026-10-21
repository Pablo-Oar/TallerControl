import { api } from './client'

export type CashAccountKind = 'cash' | 'bank' | 'digital'
export type CashSessionStatus = 'open' | 'closed'
export type CashMovementDirection = 'in' | 'out'
export type CashMovementReason = 'payment' | 'purchase_payment' | 'counter_sale_income' | 'manual_income' | 'manual_expense'

export interface CashAccountDto {
  id: string
  branchId: string
  name: string
  kind: CashAccountKind
  isActive: boolean
  version: number
  /** Suma de los movimientos (ingresos − egresos). */
  balance: number
  /** Desde cuándo está abierta la sesión; null si no hay ninguna abierta. */
  sessionOpenedAt: string | null
}

export interface CashSessionDto {
  id: string
  accountId: string
  status: CashSessionStatus
  openedAt: string
  openedByMembershipId: string
  openingNote: string | null
  closedAt: string | null
  closedByMembershipId: string | null
  closingNote: string | null
}

export interface CashMovementDto {
  id: string
  accountId: string
  amount: number
  direction: CashMovementDirection
  reason: CashMovementReason
  referenceType: string | null
  referenceId: string | null
  note: string | null
  createdAt: string
}

export interface CreateCashAccountRequest {
  name: string
  kind: CashAccountKind
}

export interface UpdateCashAccountRequest {
  name: string
  kind: CashAccountKind
  isActive: boolean
  version: number
}

export interface OpenCashSessionRequest {
  openingNote?: string | null
}

export interface CloseCashSessionRequest {
  closingNote?: string | null
  /** Efectivo contado: el sistema registra lo esperado y la diferencia en la nota de cierre. */
  countedAmount?: number | null
}

export interface ManualCashMovementRequest {
  amount: number
  direction: CashMovementDirection
  note: string
}

export const cashApi = {
  listAccounts: () => api.get<CashAccountDto[]>('/api/v1/cash/accounts'),
  getAccount: (id: string) => api.get<CashAccountDto>(`/api/v1/cash/accounts/${id}`),
  createAccount: (body: CreateCashAccountRequest) =>
    api.post<CashAccountDto>('/api/v1/cash/accounts', body, true),
  updateAccount: (id: string, body: UpdateCashAccountRequest) =>
    api.put<CashAccountDto>(`/api/v1/cash/accounts/${id}`, body, String(body.version)),
  deleteAccount: (id: string) => api.delete<void>(`/api/v1/cash/accounts/${id}`, undefined, true),
  openSession: (accountId: string, body: OpenCashSessionRequest) =>
    api.post<CashSessionDto>(`/api/v1/cash/accounts/${accountId}/open`, body, true),
  closeSession: (accountId: string, body: CloseCashSessionRequest) =>
    api.post<CashSessionDto>(`/api/v1/cash/accounts/${accountId}/close`, body, true),
  getMovements: (accountId: string, from?: string | null, to?: string | null) => {
    const params = new URLSearchParams()
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    const qs = params.toString() ? `?${params}` : ''
    return api.get<CashMovementDto[]>(`/api/v1/cash/accounts/${accountId}/movements${qs}`)
  },
  addMovement: (accountId: string, body: ManualCashMovementRequest) =>
    api.post<CashMovementDto>(`/api/v1/cash/accounts/${accountId}/movements`, body, true),
}
