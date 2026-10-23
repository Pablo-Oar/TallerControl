import { useState } from 'react'
import { Button } from '../../components/ui/Button'
import { api, ApiError } from '../../lib/api/client'

interface PushTestResult {
  configured: boolean
  subscriptions: number
  delivered: number
  failures: string[]
  statuses: number[]
}

/**
 * Manda un aviso de prueba a los dispositivos del propio usuario y muestra qué respondió el servicio push.
 * Sirve para saber si los avisos funcionan sin esperar a que le asignen una orden.
 */
export function PushTestButton() {
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  async function run() {
    setBusy(true)
    setMessage(null)
    try {
      const r = await api.post<PushTestResult>('/api/v1/push/test', {}, true)
      if (!r.configured) setMessage('El servidor no tiene configurado el envío de avisos.')
      else if (r.subscriptions === 0) setMessage('Este usuario no tiene ningún celular registrado. Tocá «Activar avisos» y volvé a probar.')
      else if (r.delivered === 0) setMessage(`Google rechazó el aviso: ${r.failures.join(' | ') || 'sin detalle'}`)
      else setMessage(`Aviso enviado a ${r.delivered} de ${r.subscriptions} dispositivo(s) (respuesta ${r.statuses.join(', ')}). Si no aparece en unos segundos, el problema está en el celular.`)
    } catch (err) {
      setMessage(err instanceof ApiError
        ? `El servidor respondió ${err.status}: ${err.detail}`
        : 'No se pudo conectar con el servidor. Revisá tu conexión.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="mt-3 space-y-2">
      <Button variant="secondary" size="sm" loading={busy} onClick={run}>
        Enviar aviso de prueba
      </Button>
      {message && <p role="status" className="text-xs text-tc-text-secondary">{message}</p>}
    </div>
  )
}
