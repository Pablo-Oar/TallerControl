import type { InvoiceStatus } from '../../../lib/api/invoicing'

export const INVOICE_STATUS_LABELS: Record<InvoiceStatus, string> = {
  pending: 'Pendiente',
  authorized: 'Autorizada',
  rejected: 'Rechazada',
}

export const INVOICE_STATUS_HINTS: Record<InvoiceStatus, string> = {
  pending: 'Enviada a ARCA, todavía sin respuesta',
  authorized: 'ARCA la autorizó y tiene CAE',
  rejected: 'ARCA no la autorizó',
}
