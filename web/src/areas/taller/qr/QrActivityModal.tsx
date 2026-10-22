import { useQuery } from '@tanstack/react-query'
import { getQrCardEvents, type QrScanEventType } from '../../../lib/api/qrCards'
import { Modal } from '../../../components/ui/Modal'
import { Button } from '../../../components/ui/Button'
import { Timeline } from '../../../components/ui/Timeline'
import { LoadError } from '../../../components/LoadError'

const EVENT_LABELS: Record<QrScanEventType, string> = {
  scan: 'Escaneo del código',
  owner_challenge: 'Pidieron verificar la identidad',
  otp_sent: 'Se envió un código de verificación',
  otp_failed: 'Código de verificación incorrecto',
  owner_session: 'El dueño ingresó al portal',
  found_keys: 'Avisaron que encontraron las llaves',
}

/** Actividad registrada de una tarjeta: escaneos, verificaciones y avisos de llaves encontradas. */
export function QrActivityModal({ cardId, code, onClose }: { cardId: string; code: string; onClose: () => void }) {
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['qr-events', cardId],
    queryFn: () => getQrCardEvents(cardId, 1, 50),
  })

  return (
    <Modal title={`Actividad · ${code}`} onClose={onClose} className="max-w-lg">
      <div className="space-y-4">
        {isLoading && <p className="text-sm text-tc-text-muted">Cargando actividad…</p>}
        {error && <LoadError what="la actividad de la tarjeta" onRetry={() => refetch()} />}
        {data && (
          <Timeline
            label="Actividad de la tarjeta"
            empty="Esta tarjeta todavía no tuvo actividad."
            items={data.items.map(e => ({
              id: e.id,
              date: e.createdAt,
              kind: e.eventType === 'found_keys' ? ('status' as const) : ('info' as const),
              title: EVENT_LABELS[e.eventType] ?? e.eventType,
              detail: e.outcome,
              emphasis: e.eventType === 'found_keys',
            }))}
          />
        )}
        <div className="flex justify-end">
          <Button size="sm" variant="secondary" onClick={onClose}>Cerrar</Button>
        </div>
      </div>
    </Modal>
  )
}
