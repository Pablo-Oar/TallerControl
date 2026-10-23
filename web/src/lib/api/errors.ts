import { ApiError } from './client'

/**
 * Mensaje para mostrar cuando una acción falla. Sin conexión lo dice claro (en el celular del mecánico es lo más
 * común); si el servidor explicó el problema, se muestra su texto; si no, el texto de respaldo.
 */
export function actionErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) return error.detail
  if (typeof navigator !== 'undefined' && navigator.onLine === false) {
    return 'Sin conexión: no se pudo completar. Probá de nuevo cuando vuelva la señal.'
  }
  return fallback
}
