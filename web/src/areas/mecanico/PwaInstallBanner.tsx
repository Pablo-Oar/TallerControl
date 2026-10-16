import { useState, useEffect } from 'react'
import { api } from '../../lib/api/client'

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

export function PwaInstallBanner() {
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

  if (!installPrompt && !showPush) return null

  return (
    <div className="mx-4 my-2 rounded-xl border border-blue-200 bg-blue-50 p-3 flex flex-col gap-2">
      {installPrompt && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-blue-800">Instalá la app para acceso rápido offline.</p>
          <button
            onClick={handleInstall}
            className="ml-2 rounded-md bg-blue-700 px-3 py-1 text-xs font-medium text-white hover:bg-blue-800"
          >
            Instalar
          </button>
        </div>
      )}
      {showPush && !pushGranted && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-blue-800">Activá notificaciones para nuevas órdenes asignadas.</p>
          <button
            onClick={handleEnablePush}
            className="ml-2 rounded-md bg-blue-700 px-3 py-1 text-xs font-medium text-white hover:bg-blue-800"
          >
            Activar
          </button>
        </div>
      )}
    </div>
  )
}
