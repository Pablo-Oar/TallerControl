import { useAuth } from './AuthContext'

/** IVA por defecto del taller (Configuración → General): precarga renglones nuevos. 21 si todavía no hay sesión. */
export function useDefaultVat(): number {
  const { user } = useAuth()
  return user?.tenant.defaultVatRate ?? 21
}
