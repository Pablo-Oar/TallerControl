import { api } from './client'

export type PaymentMethod = 'cash' | 'transfer' | 'debit' | 'credit'
export type PaymentStatus = 'valid' | 'voided'

export interface PaymentDto {
  id: string
  number: number
  method: PaymentMethod
  amount: number
  paidAt: string
  reference: string | null
  status: PaymentStatus
  voidReason: string | null
  voidedAt: string | null
  publicToken: string
}

export interface WorkOrderPaymentsDto {
  payments: PaymentDto[]
  total: number
  paid: number
  balance: number
}

export interface PublicPaymentDto {
  number: number
  method: PaymentMethod
  amount: number
  paidAt: string
  reference: string | null
  status: PaymentStatus
}

export interface CreatePaymentRequest {
  method: PaymentMethod
  amount: number
  paidAt?: string
  reference?: string
}

export interface CreatePaymentResult {
  payment: PaymentDto
  balance: number
}

export const METHOD_LABELS: Record<PaymentMethod, string> = {
  cash: 'Efectivo',
  transfer: 'Transferencia',
  debit: 'Débito',
  credit: 'Crédito',
}

export const STATUS_LABELS: Record<PaymentStatus, string> = {
  valid: 'Válido',
  voided: 'Anulado',
}

export const getPayments = (workOrderId: string) =>
  api.get<WorkOrderPaymentsDto>(`/api/v1/work-orders/${workOrderId}/payments`)

export async function createPayment(
  workOrderId: string,
  request: CreatePaymentRequest,
  idempotencyKey?: string,
): Promise<CreatePaymentResult> {
  // El cliente no soporta cabeceras extras; si hay clave de idempotencia usamos fetch directo.
  if (!idempotencyKey) {
    return api.post<CreatePaymentResult>(
      `/api/v1/work-orders/${workOrderId}/payments`,
      request,
      true,
    )
  }

  const csrfRes = await fetch('/api/v1/auth/antiforgery-token', { credentials: 'include' })
  const { token } = await csrfRes.json()
  const res = await fetch(`/api/v1/work-orders/${workOrderId}/payments`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-XSRF-TOKEN': token as string,
      'Idempotency-Key': idempotencyKey,
    },
    body: JSON.stringify(request),
  })
  if (!res.ok) throw new Error(await res.text())
  return res.json() as Promise<CreatePaymentResult>
}

export const voidPayment = (workOrderId: string, paymentId: string, reason: string) =>
  api.post<WorkOrderPaymentsDto>(
    `/api/v1/work-orders/${workOrderId}/payments/${paymentId}/void`,
    { reason },
    true,
  )

export const getPaymentPdfUrl = (workOrderId: string, paymentId: string) =>
  `/api/v1/work-orders/${workOrderId}/payments/${paymentId}/pdf`

export const getPublicPayment = (token: string) =>
  api.get<PublicPaymentDto>(`/api/public/v1/payments/${token}`)
