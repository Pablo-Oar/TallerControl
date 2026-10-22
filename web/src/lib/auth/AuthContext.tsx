import { createContext, useContext, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { authApi, type MeResponse } from '../api/auth'
import { clearCsrfToken } from '../api/client'

interface AuthContextValue {
  user: MeResponse | null
  isLoading: boolean
  logout: () => Promise<void>
  /** Pasar el usuario recién obtenido (`me`) evita que el guard vea al usuario anterior mientras se refresca. */
  invalidate: (me?: MeResponse) => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['me'],
    queryFn: () => authApi.me(),
    retry: false,
    staleTime: 5 * 60 * 1000,
  })

  async function logout() {
    try {
      await authApi.logout()
    } finally {
      clearCsrfToken()
      queryClient.removeQueries({ queryKey: ['me'] })
    }
  }

  /**
   * La sesión cambió (login, aceptar invitación): el token antiforgery está ligado a la identidad, así que el
   * obtenido antes de autenticarse deja de valer. Se descarta para pedir uno nuevo en la primera mutación;
   * sin esto la primera acción tras iniciar sesión (sin recargar) fallaba con "cabecera antiforgery inválida".
   */
  function invalidate(me?: MeResponse) {
    clearCsrfToken()
    // Sin esto, entre el login y el refresco de `['me']` el panel seguía con el usuario anterior en memoria
    // (p. ej. un mecánico) y el guard mandaba al dueño a /m.
    if (me) queryClient.setQueryData(['me'], me)
    queryClient.invalidateQueries({ queryKey: ['me'] })
  }

  return (
    <AuthContext.Provider
      value={{ user: data ?? null, isLoading, logout, invalidate }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider')
  return ctx
}
