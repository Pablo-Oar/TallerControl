import { api } from './api/client'

/**
 * Da de baja en el servidor la suscripción push de este navegador. Se usa al cerrar sesión: la suscripción es del
 * navegador, no de la persona, y si queda registrada los avisos de ese usuario le seguirían llegando a quien use el
 * navegador después. La suscripción del navegador se conserva: al volver a entrar a /m se registra de nuevo.
 */
export async function unregisterPush(): Promise<void> {
  try {
    if (!('serviceWorker' in navigator)) return
    const reg = await navigator.serviceWorker.getRegistration()
    const sub = await reg?.pushManager.getSubscription()
    if (!sub) return
    await api.delete<void>(`/api/v1/push-subscriptions?endpoint=${encodeURIComponent(sub.endpoint)}`, undefined, true)
  } catch {
    // Mejor esfuerzo: cerrar sesión no debe fallar por esto.
  }
}
