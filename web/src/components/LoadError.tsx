import { Button } from './ui/Button'

/**
 * Error de carga con salida: distingue "sin conexión" (el celular del mecánico en un sótano o en modo avión) de un
 * error del servidor, y deja reintentar sin recargar la app. La app abre sin red (service worker) pero los datos no
 * se guardan en el dispositivo: hay que decirlo claro.
 */
export function LoadError({ what, onRetry }: { what: string; onRetry: () => void }) {
  const offline = typeof navigator !== 'undefined' && navigator.onLine === false
  return (
    <div role="alert" className="space-y-3 border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
      <p>
        {offline
          ? `Sin conexión. No se pudo cargar ${what}: los datos necesitan internet.`
          : `No se pudo cargar ${what}.`}
      </p>
      <Button size="sm" variant="secondary" onClick={onRetry}>
        Reintentar
      </Button>
    </div>
  )
}
