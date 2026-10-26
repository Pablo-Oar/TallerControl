import { useState } from 'react'
import { getOwnerAccessCode } from '../../../lib/api/qrCards'
import { ApiError } from '../../../lib/api/client'
import { Button } from '../../../components/ui/Button'
import { ShareLink } from '../../../components/ShareLink'
import { confirmDialog } from '../../../components/ui/dialogs'

/**
 * Código de acceso del cliente para el portal del dueño del vehículo (QR). El sistema solo guarda su hash: se ve
 * una única vez, al generarlo. Generar otro invalida el anterior.
 */
export function OwnerAccessCode({ customerId, customerName }: { customerId: string; customerName: string }) {
  const [code, setCode] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function generate() {
    const ok = await confirmDialog(
      'Se genera un código nuevo y el anterior deja de servir. ¿Generar un código de acceso para este cliente?',
      { confirmLabel: 'Generar código' },
    )
    if (!ok) return
    setBusy(true)
    setError(null)
    try {
      setCode((await getOwnerAccessCode(customerId)).code)
    } catch (err) {
      setError(err instanceof ApiError ? err.detail : 'No se pudo generar el código')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="space-y-2 rounded-xl border border-tc-border bg-tc-surface p-3">
      <p className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Código de acceso al portal QR</p>
      <p className="text-xs text-tc-text-secondary">
        Es el código que {customerName} usa, junto con la patente, para ver el historial de su vehículo al escanear el
        llavero. Entregáselo en persona o por mensaje.
      </p>
      {error && <p role="alert" className="text-xs text-tc-stamp-red">{error}</p>}
      {code ? (
        <div className="space-y-2">
          <p className="text-2xl font-bold tracking-[0.3em] text-tc-text tabular-nums" aria-label="Código de acceso">{code}</p>
          <p className="text-xs text-tc-text-muted">Anotalo ahora: no se vuelve a mostrar.</p>
          <ShareLink label="Código" url={code} whatsappText="Tu código de acceso para ver el historial de tu vehículo:" />
        </div>
      ) : (
        <Button type="button" size="sm" variant="secondary" loading={busy} onClick={generate}>
          Generar código de acceso
        </Button>
      )}
    </div>
  )
}
