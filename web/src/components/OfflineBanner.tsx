import { useSyncExternalStore } from 'react'

function subscribe(onChange: () => void) {
  window.addEventListener('online', onChange)
  window.addEventListener('offline', onChange)
  return () => {
    window.removeEventListener('online', onChange)
    window.removeEventListener('offline', onChange)
  }
}

/** ¿El navegador dice que no hay red? */
export function useOffline(): boolean {
  return useSyncExternalStore(subscribe, () => navigator.onLine === false, () => false)
}

/**
 * P-02: aviso del portal del mecánico sin conexión. Las órdenes que ya abrió se ven (las guarda el service worker),
 * pero cambiar estados, notas o fotos necesita internet: se dice claro para que nadie crea que guardó algo.
 */
export function OfflineBanner() {
  const offline = useOffline()
  if (!offline) return null
  return (
    <div role="status" className="border-b border-amber-300 bg-amber-50 px-4 py-2 text-center text-xs text-amber-900">
      Sin conexión: ves lo último que cargaste. Para cambiar estados, notas o fotos necesitás internet.
    </div>
  )
}
