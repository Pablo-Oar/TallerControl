import { useState, useEffect } from 'react'
import { Button } from '../../components/ui/Button'
import { api } from '../../lib/api/client'

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

const DISMISS_KEY = 'tc-pwa-banner-dismissed'
const DISMISS_DAYS = 7

/** base64url → bytes, el formato que pide `applicationServerKey`. */
function urlBase64ToBytes(value: string) {
  const base64 = (value + '='.repeat((4 - (value.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/')
  return Uint8Array.from(atob(base64), c => c.charCodeAt(0))
}

/** bytes → base64url sin relleno, como lo espera el servidor. */
function bytesToUrlBase64(buffer: ArrayBuffer | null) {
  if (!buffer) return ''
  return btoa(String.fromCharCode(...new Uint8Array(buffer))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

/**
 * Deja una suscripción push vigente y registrada en el servidor. Sirve para activar los avisos y para repararlos si
 * el permiso ya estaba dado pero la suscripción no existe, es de otra clave o el servidor la descartó.
 * Devuelve false si el servidor no tiene push configurado.
 */
async function syncPushSubscription(): Promise<boolean> {
  const key = await api.get<{ publicKey: string }>('/api/v1/push/public-key').catch(() => null)
  if (!key) return false

  // El service worker únicamente se registra en producción: sin registrarlo acá, `ready` no resuelve nunca en un
  // navegador que no lo tenía (p. ej. Samsung Internet) y la suscripción queda colgada.
  await navigator.serviceWorker.register('/sw.js').catch(() => null)
  const reg = await navigator.serviceWorker.ready
  const serverKey = urlBase64ToBytes(key.publicKey)
  const subscribe = () => reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: serverKey })

  // Una suscripción hecha con otra clave VAPID (o sin clave) no sirve: se reemplaza.
  const sub = await subscribe().catch(async () => {
    await (await reg.pushManager.getSubscription())?.unsubscribe()
    return subscribe().catch(() => null)
  })
  if (!sub) return true

  await api.post('/api/v1/push-subscriptions', {
    endpoint: sub.endpoint,
    p256Dh: bytesToUrlBase64(sub.getKey('p256dh')),
    auth: bytesToUrlBase64(sub.getKey('auth')),
  }, true).catch(() => null)
  return true
}

function dismissedRecently() {
  try {
    const at = Number(localStorage.getItem(DISMISS_KEY) ?? 0)
    return Date.now() - at < DISMISS_DAYS * 86_400_000
  } catch {
    return false
  }
}

export function PwaInstallBanner() {
  const [dismissed, setDismissed] = useState(dismissedRecently)
  const [installPrompt, setInstallPrompt] = useState<BeforeInstallPromptEvent | null>(null)
  const [showPush, setShowPush] = useState(false)
  const [pushGranted, setPushGranted] = useState(false)

  useEffect(() => {
    const handler = (e: Event) => {
      e.preventDefault()
      setInstallPrompt(e as BeforeInstallPromptEvent)
    }
    window.addEventListener('beforeinstallprompt', handler)

    // Verificar si push está disponible y si ya hay permiso
    if ('Notification' in window) {
      setShowPush(Notification.permission !== 'granted')
      setPushGranted(Notification.permission === 'granted')
      // Con el permiso ya dado se renueva la suscripción en silencio: no depende de ningún botón.
      if (Notification.permission === 'granted' && 'serviceWorker' in navigator) {
        void syncPushSubscription().catch(() => null)
      }
    }

    return () => window.removeEventListener('beforeinstallprompt', handler)
  }, [])

  async function handleInstall() {
    if (!installPrompt) return
    await installPrompt.prompt()
    const { outcome } = await installPrompt.userChoice
    if (outcome === 'accepted') setInstallPrompt(null)
  }

  async function handleEnablePush() {
    if (!('Notification' in window) || !('serviceWorker' in navigator)) return
    const permission = await Notification.requestPermission()
    if (permission !== 'granted') return

    // Sin claves VAPID en el servidor el push no está configurado.
    if (!(await syncPushSubscription())) {
      setShowPush(false)
      return
    }

    setPushGranted(true)
    setShowPush(false)
  }

  function dismiss() {
    try {
      localStorage.setItem(DISMISS_KEY, String(Date.now()))
    } catch {
      /* sin almacenamiento: se oculta hasta recargar */
    }
    setDismissed(true)
  }

  if (dismissed || (!installPrompt && !(showPush && !pushGranted))) return null

  return (
    <div className="mx-4 my-1 flex items-center gap-2 border border-tc-border bg-white py-1 pl-3 pr-0 text-xs text-tc-text">
      <div className="min-w-0 flex-1 space-y-1">
        {installPrompt && <p>Instalá la app para abrirla rápido, incluso sin señal.</p>}
        {showPush && !pushGranted && <p>Activá los avisos de nuevas órdenes asignadas.</p>}
        <div className="flex flex-wrap gap-2">
          {installPrompt && (
            <Button size="sm" className="min-h-[44px]" onClick={handleInstall}>
              Instalar
            </Button>
          )}
          {showPush && !pushGranted && (
            <Button size="sm" variant="secondary" className="min-h-[44px]" onClick={handleEnablePush}>
              Activar avisos
            </Button>
          )}
        </div>
      </div>
      <button
        type="button"
        onClick={dismiss}
        aria-label="Ocultar este aviso por una semana"
        className="flex h-11 w-11 shrink-0 items-center justify-center text-lg text-tc-text-muted hover:text-tc-text"
      >
        ×
      </button>
    </div>
  )
}
