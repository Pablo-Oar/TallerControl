import { useState, useEffect } from 'react'
import { Button } from '../../components/ui/Button'
import { api } from '../../lib/api/client'

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

const DISMISS_KEY = 'tc-pwa-banner-dismissed'
const DISMISS_DAYS = 7

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

    const reg = await navigator.serviceWorker.ready
    const sub = await reg.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: undefined, // VAPID key se configura en producción
    }).catch(() => null)

    if (sub) {
      const json = sub.toJSON()
      await api.post('/api/v1/push-subscriptions', {
        endpoint: json.endpoint,
        p256Dh: btoa(String.fromCharCode(...new Uint8Array(sub.getKey('p256dh') ?? new ArrayBuffer(0)))),
        auth: btoa(String.fromCharCode(...new Uint8Array(sub.getKey('auth') ?? new ArrayBuffer(0)))),
      }, true).catch(() => null)
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
    <div className="mx-4 my-2 flex items-start gap-3 border border-tc-border bg-white p-3 text-sm text-tc-text">
      <div className="min-w-0 flex-1 space-y-2">
        {installPrompt && <p>Instalá la app para abrirla rápido, incluso sin señal.</p>}
        {showPush && !pushGranted && <p>Activá los avisos de nuevas órdenes asignadas.</p>}
        <div className="flex flex-wrap gap-2">
          {installPrompt && (
            <Button size="sm" onClick={handleInstall}>
              Instalar
            </Button>
          )}
          {showPush && !pushGranted && (
            <Button size="sm" variant="secondary" onClick={handleEnablePush}>
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
