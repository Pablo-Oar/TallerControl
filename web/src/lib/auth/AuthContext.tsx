import { createContext, useContext, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { authApi, type MeResponse } from '../api/auth'
import { clearCsrfToken } from '../api/client'

interface AuthContextValue {
  user: MeResponse | null
  isLoading: boolean
  logout: () => Promise<void>
  invalidate: () => void
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

  function invalidate() {
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
