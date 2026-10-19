import { Navigate, useLocation } from 'react-router'
import type { ReactNode } from 'react'
import { useAuth } from './AuthContext'
import type { MeResponse } from '../api/auth'

interface RequireAuthProps {
  children: ReactNode
  /** Si se especifica, el rol del usuario debe coincidir. */
  role?: MeResponse['role']
  /**
   * Área que protege el guard. En `app` (panel del taller) un usuario cuyo `home` es otra área
   * (p. ej. el mecánico, `/m`) se redirige a su propia área (E2E-08, T-AUTH-09).
   */
  area?: 'app'
}

/**
 * Guard de ruta: redirige a /login si no hay sesión activa.
 * Si se pasa `role`, redirige a /app si el rol no coincide (403 silencioso).
 * Si se pasa `area="app"`, redirige al `home` del usuario cuando no es el panel del taller.
 */
export function RequireAuth({ children, role, area }: RequireAuthProps) {
  const { user, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="h-6 w-6 animate-spin border-2 border-tc-purple border-t-transparent" />
      </div>
    )
  }

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  if (area === 'app' && user.home && user.home !== '/app') {
    return <Navigate to={user.home} replace />
  }

  if (role && user.role !== role) {
    return <Navigate to="/app" replace />
  }

  return <>{children}</>
}
