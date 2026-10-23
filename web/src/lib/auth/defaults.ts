import { useAuth } from './AuthContext'

/** Días de vigencia por defecto de un presupuesto (Configuración → General). 15 si todavía no hay sesión. */
export function useQuoteValidityDays(): number {
  const { user } = useAuth()
  return user?.tenant.quoteValidityDays ?? 15
}

/** IVA por defecto del taller (Configuración → General): precarga renglones nuevos. 21 si todavía no hay sesión. */
export function useDefaultVat(): number {
  const { user } = useAuth()
  return user?.tenant.defaultVatRate ?? 21
}
