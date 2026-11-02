import { useAuth } from './AuthContext'

/** Días de vigencia por defecto de un presupuesto (Configuración → General). 15 si todavía no hay sesión. */
export function useQuoteValidityDays(): number {
  const { user } = useAuth()
  return user?.tenant.quoteValidityDays ?? 15
}

/** Minutos con los que se reserva un turno si el servicio del catálogo no tiene duración (Configuración → General). 30 por defecto. */
export function useDefaultServiceMinutes(): number {
  const { user } = useAuth()
  return user?.tenant.defaultServiceMinutes ?? 30
}

/** IVA por defecto del taller (Configuración → General): precarga renglones nuevos. 21 si todavía no hay sesión. */
export function useDefaultVat(): number {
  const { user } = useAuth()
  return user?.tenant.defaultVatRate ?? 21
}
