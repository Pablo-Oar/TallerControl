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
  /** Cuenta de caja donde entra el cobro (opcional). */
  accountId?: string | null
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

export function createPayment(
  workOrderId: string,
  request: CreatePaymentRequest,
  idempotencyKey?: string,
): Promise<CreatePaymentResult> {
  // Con clave de idempotencia, repetir el envío (doble toque, reintento) devuelve el mismo cobro.
  return api.post<CreatePaymentResult>(
    `/api/v1/work-orders/${workOrderId}/payments`,
    request,
    true,
    idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined,
  )
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

/** Emite un enlace nuevo del comprobante (el anterior deja de servir). Devuelve la dirección completa. */
export async function createPaymentPublicLink(workOrderId: string, paymentId: string): Promise<string> {
  const { publicToken } = await api.post<{ publicToken: string }>(
    `/api/v1/work-orders/${workOrderId}/payments/${paymentId}/public-link`,
    undefined,
    true,
  )
  return `${window.location.origin}/c/${publicToken}`
}
