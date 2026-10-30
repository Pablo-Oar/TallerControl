import { api } from './client'

/** Alícuotas de IVA permitidas (doc 04 §3.1, CHECK de tenant_settings). */
export const ALLOWED_VAT_RATES = [0, 2.5, 5, 10.5, 21, 27] as const

export const ALLOWED_LOGO_CONTENT_TYPES = ['image/png', 'image/jpeg', 'image/webp'] as const
export const MAX_LOGO_SIZE_BYTES = 5 * 1024 * 1024

export interface SettingsDto {
  name: string
  legalName: string | null
  taxId: string | null
  address: string | null
  phone: string | null
  timeZone: string
  defaultVatRate: number
  quoteValidityDays: number
  logoUrl: string | null
  version: string
  /** Umbrales del Inicio (D-42). */
  hotQuoteHours: number
  stuckOrderDays: number
  agedReceivableDays: number
}

export interface UpdateSettingsRequest {
  name: string
  legalName: string | null
  taxId: string | null
  address: string | null
  phone: string | null
  timeZone: string
  defaultVatRate: number
  quoteValidityDays: number
  hotQuoteHours: number
  stuckOrderDays: number
  agedReceivableDays: number
}

export interface LogoUploadUrlResponse {
  uploadUrl: string
  fileKey: string
}

export const settingsApi = {
  get: () => api.get<SettingsDto>('/api/v1/settings'),
  update: (body: UpdateSettingsRequest, version: string) =>
    api.put<SettingsDto>('/api/v1/settings', body, version),
  requestLogoUploadUrl: (contentType: string, size: number) =>
    api.post<LogoUploadUrlResponse>('/api/v1/settings/logo/upload-url', { contentType, size }, true),
  confirmLogo: () => api.post<SettingsDto>('/api/v1/settings/logo/confirm', undefined, true),
}
