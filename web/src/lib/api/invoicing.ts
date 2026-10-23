import { api } from './client'

// ── Enums ────────────────────────────────────────────────────────────────────

export type FiscalEnvironment = 'production' | 'homologation'
export type FiscalCertificateStatus = 'pending' | 'active' | 'expired' | 'revoked'
export type InvoiceType = 'a' | 'b' | 'c' | 'm'
export type InvoiceStatus = 'pending' | 'authorized' | 'rejected'
export type InvoiceSourceType = 'work_order' | 'counter_sale'

// ── Certificados ─────────────────────────────────────────────────────────────

export interface FiscalCertificateDto {
  id: string
  cuit: string
  name: string
  isPrimary: boolean
  status: FiscalCertificateStatus
  environment: FiscalEnvironment
  csrPem: string
  expiresAt: string | null
  isActive: boolean
  version: number
  createdAt: string
}

export interface CreateFiscalCertificateRequest {
  cuit: string
  name: string
  isPrimary: boolean
  environment: FiscalEnvironment
}

export interface UploadFiscalCertificateRequest {
  certificatePem: string
  /** Opcional: el servidor lee el vencimiento del certificado. */
  expiresAt?: string
}

export interface TestFiscalConnectionResponse {
  success: boolean
  lastInvoiceNumber: number | null
  errorMessage: string | null
  testedAt: string
}

// ── Puntos de Venta ───────────────────────────────────────────────────────────

export interface FiscalPointOfSaleDto {
  id: string
  fiscalCertificateId: string
  number: number
  name: string
  isActive: boolean
  version: number
}

export interface CreateFiscalPointOfSaleRequest {
  fiscalCertificateId: string
  number: number
  name: string
}

export interface UpdateFiscalPointOfSaleRequest {
  name: string
  isActive: boolean
  version: number
}

// ── Facturas ─────────────────────────────────────────────────────────────────

export interface InvoiceListDto {
  id: string
  number: number | null
  invoiceType: InvoiceType
  status: InvoiceStatus
  isCredit: boolean
  totalAmount: number
  issuedAt: string
  sourceType: InvoiceSourceType
  sourceId: string
  /** Emitida contra homologación: de prueba, sin validez fiscal. */
  isHomologation: boolean
}

export interface InvoiceDto extends InvoiceListDto {
  fiscalPointOfSaleId: string
  netAmount: number
  vatAmount: number
  cae: string | null
  caeExpiresAt: string | null
  originalInvoiceId: string | null
  rejectionReason: string | null
}

export interface EmitInvoiceRequest {
  fiscalPointOfSaleId: string
  concept: number
}

// ── Libro IVA ─────────────────────────────────────────────────────────────────

export interface IvaBookEntryDto {
  date: string
  /** «0001-00000012»; null si falta el punto de venta o el número. */
  invoiceNumber: string | null
  counterpartyCuit: string | null
  /** Cliente (ventas) o proveedor (compras). */
  counterpartyName: string
  netAmount: number
  vatAmount: number
  totalAmount: number
  /** «Nota de crédito» o «Excluido CITI» cuando corresponde. */
  observations: string | null
  /** Tipo de factura (A, B…): solo ventas. */
  invoiceType?: string | null
  cae?: string | null
}

export interface IvaBookSalesDto {
  count: number
  totalNet: number
  totalVat: number
  totalAmount: number
  entries: IvaBookEntryDto[]
}

export interface IvaBookPurchasesDto {
  count: number
  totalNet: number
  totalVat: number
  totalAmount: number
  entries: IvaBookEntryDto[]
}

export interface IvaBookDto {
  year: number
  month: number
  sales: IvaBookSalesDto
  purchases: IvaBookPurchasesDto
}

// ── Paginación ────────────────────────────────────────────────────────────────

export interface Page<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

// ── API calls ─────────────────────────────────────────────────────────────────

// Certificados
export const listCertificates = (): Promise<FiscalCertificateDto[]> =>
  api.get<FiscalCertificateDto[]>('/api/v1/invoicing/certificates')

export const createCertificate = (req: CreateFiscalCertificateRequest): Promise<FiscalCertificateDto> =>
  api.post<FiscalCertificateDto>('/api/v1/invoicing/certificates', req, true)

export const uploadCertificate = (id: string, req: UploadFiscalCertificateRequest): Promise<FiscalCertificateDto> =>
  api.post<FiscalCertificateDto>(`/api/v1/invoicing/certificates/${id}/upload`, req, true)

export const revokeCertificate = (id: string): Promise<FiscalCertificateDto> =>
  api.post<FiscalCertificateDto>(`/api/v1/invoicing/certificates/${id}/revoke`, {}, true)

/** Genera una clave y un CSR nuevos para el mismo CUIT; el certificado queda Pendiente hasta cargar el de ARCA. */
export const renewCertificate = (id: string): Promise<FiscalCertificateDto> =>
  api.post<FiscalCertificateDto>(`/api/v1/invoicing/certificates/${id}/renew`, {}, true)

/** Días que faltan para el vencimiento (negativo si ya venció); null si no hay fecha. */
export function daysToExpire(cert: { expiresAt: string | null }, now = Date.now()): number | null {
  if (!cert.expiresAt) return null
  return Math.ceil((new Date(cert.expiresAt).getTime() - now) / 86_400_000)
}

export const EXPIRY_WARNING_DAYS = 30

export const testConnection = (certId: string, posId: string): Promise<TestFiscalConnectionResponse> =>
  api.post<TestFiscalConnectionResponse>(`/api/v1/invoicing/certificates/${certId}/test?pointOfSaleId=${posId}`, undefined, true)

// Puntos de venta
export const listPointsOfSale = (): Promise<FiscalPointOfSaleDto[]> =>
  api.get<FiscalPointOfSaleDto[]>('/api/v1/invoicing/points-of-sale')

export const createPointOfSale = (req: CreateFiscalPointOfSaleRequest): Promise<FiscalPointOfSaleDto> =>
  api.post<FiscalPointOfSaleDto>('/api/v1/invoicing/points-of-sale', req, true)

export const updatePointOfSale = (id: string, req: UpdateFiscalPointOfSaleRequest): Promise<FiscalPointOfSaleDto> =>
  api.put<FiscalPointOfSaleDto>(`/api/v1/invoicing/points-of-sale/${id}`, req, String(req.version))

// Facturas
export const searchInvoices = (params: { status?: string; sourceType?: string; sourceId?: string; page?: number; pageSize?: number }): Promise<Page<InvoiceListDto>> => {
  const sp = new URLSearchParams()
  if (params.status) sp.set('status', params.status)
  if (params.sourceType) sp.set('sourceType', params.sourceType)
  if (params.sourceId) sp.set('sourceId', params.sourceId)
  if (params.page) sp.set('page', String(params.page))
  if (params.pageSize) sp.set('pageSize', String(params.pageSize))
  return api.get<Page<InvoiceListDto>>(`/api/v1/invoicing/invoices?${sp}`)
}

export const getInvoice = (id: string): Promise<InvoiceDto> =>
  api.get<InvoiceDto>(`/api/v1/invoicing/invoices/${id}`)

export const emitInvoiceFromWorkOrder = (workOrderId: string, req: EmitInvoiceRequest): Promise<InvoiceDto> =>
  api.post<InvoiceDto>(`/api/v1/work-orders/${workOrderId}/invoice`, req, true)

export const emitInvoiceFromCounterSale = (counterSaleId: string, req: EmitInvoiceRequest): Promise<InvoiceDto> =>
  api.post<InvoiceDto>(`/api/v1/counter-sales/${counterSaleId}/invoice`, req, true)

export const voidInvoice = (id: string): Promise<InvoiceDto> =>
  api.post<InvoiceDto>(`/api/v1/invoicing/invoices/${id}/void`, undefined, true)

// Libro IVA
export const getIvaBook = (year: number, month: number): Promise<IvaBookDto> =>
  api.get<IvaBookDto>(`/api/v1/invoicing/iva-book?year=${year}&month=${month}`)

// CITI — usa fetch directo porque necesita el blob, no JSON
export const downloadCitiExport = async (year: number, month: number): Promise<void> => {
  const res = await fetch(`/api/v1/invoicing/citi-export?year=${year}&month=${month}`, {
    credentials: 'include',
  })
  if (!res.ok) throw new Error('Error al descargar CITI')
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `CITI_${year}_${String(month).padStart(2, '0')}.zip`
  a.click()
  URL.revokeObjectURL(url)
}
