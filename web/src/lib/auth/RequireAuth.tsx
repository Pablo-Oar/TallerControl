import { Navigate, useLocation } from 'react-router'
import type { ReactNode } from 'react'
import { useAuth } from './AuthContext'
import type { MeResponse } from '../api/auth'

interface RequireAuthProps {
  children: ReactNode
  /** Si se especifica, el rol del usuario debe coincidir. */
  role?: MeResponse['role']
}

/**
 * Guard de ruta: redirige a /login si no hay sesiÃ³n activa.
 * Si se pasa `role`, redirige a /app si el rol no coincide (403 silencioso).
 */
export function RequireAuth({ children, role }: RequireAuthProps) {
  const { user, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
      </div>
    )
  }

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  if (role && user.role !== role) {
    return <Navigate to="/app" replace />
  }

  return <>{children}</>
}

