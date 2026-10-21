import type { InvoiceStatus } from '../../../lib/api/invoicing'

export const INVOICE_STATUS_LABELS: Record<InvoiceStatus, string> = {
  pending: 'Pendiente',
  authorized: 'Autorizada',
  rejected: 'Rechazada',
}
