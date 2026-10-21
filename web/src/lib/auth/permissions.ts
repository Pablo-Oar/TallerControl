import { useAuth } from './AuthContext'

/** ¿La sesión tiene todos los permisos pedidos? Los permisos vienen de `/me` (ya incluyen los del rol y los extra). */
export function hasPermissions(user: { permissions?: string[] } | null | undefined, ...perms: string[]): boolean {
  const granted = user?.permissions
  if (!granted) return false
  return perms.every(p => granted.includes(p))
}

/**
 * Para ocultar o deshabilitar acciones que el servidor rechazaría. Es solo comodidad de la interfaz:
 * la barrera real sigue siendo el servidor.
 */
export function useCan() {
  const { user } = useAuth()
  return (...perms: string[]) => hasPermissions(user, ...perms)
}
