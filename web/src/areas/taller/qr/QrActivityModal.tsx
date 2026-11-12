import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getQrCardEvents, type QrEventDto, type QrScanEventType } from '../../../lib/api/qrCards'
import { Modal } from '../../../components/ui/Modal'
import { Button } from '../../../components/ui/Button'
import { Timeline } from '../../../components/ui/Timeline'
import { LoadError } from '../../../components/LoadError'
import { formatDate } from '../../../lib/dates'

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
        {data && <DayGroups events={data.items} />}
        <div className="flex justify-end">
          <Button size="sm" variant="secondary" onClick={onClose}>Cerrar</Button>
        </div>
      </div>
    </Modal>
  )
}

const dayKey = (iso: string) => {
  const d = new Date(iso)
  return `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`
}

/** Eventos agrupados por día: cada día es una tarjeta que se abre y se cierra con un clic. */
function DayGroups({ events }: { events: QrEventDto[] }) {
  const [open, setOpen] = useState<string | null>(null)
  if (events.length === 0) return <p className="text-sm text-tc-text-muted">Esta tarjeta todavía no tuvo actividad.</p>

  const groups: { key: string; date: string; items: QrEventDto[] }[] = []
  for (const e of events) {
    const k = dayKey(e.createdAt)
    const g = groups.find(x => x.key === k)
    if (g) g.items.push(e)
    else groups.push({ key: k, date: e.createdAt, items: [e] })
  }

  return (
    <ul className="space-y-2" aria-label="Actividad de la tarjeta por día">
      {groups.map(g => {
        const isOpen = open === g.key
        const scans = g.items.filter(e => e.eventType === 'scan').length
        const entries = g.items.filter(e => e.eventType === 'owner_session').length
        const alerts = g.items.filter(e => e.eventType === 'found_keys').length
        return (
          <li key={g.key} className="rounded-xl border border-tc-border bg-white">
            <button
              type="button"
              aria-expanded={isOpen}
              onClick={() => setOpen(isOpen ? null : g.key)}
              className="flex min-h-[44px] w-full items-center justify-between gap-3 px-3 py-2 text-left"
            >
              <span>
                <span className="block text-sm font-semibold text-tc-text">{formatDate(g.date)}</span>
                <span className="block text-xs text-tc-text-secondary">
                  {g.items.length} {g.items.length === 1 ? 'evento' : 'eventos'}
                  {scans > 0 && ` · ${scans} ${scans === 1 ? 'escaneo' : 'escaneos'}`}
                  {entries > 0 && ` · ${entries} ${entries === 1 ? 'ingreso al portal' : 'ingresos al portal'}`}
                  {alerts > 0 && ` · ${alerts} ${alerts === 1 ? 'aviso de llaves' : 'avisos de llaves'}`}
                </span>
              </span>
              <span aria-hidden className="text-tc-text-muted">{isOpen ? '▲' : '▼'}</span>
            </button>
            {isOpen && (
              <div className="border-t border-tc-border px-3 py-3">
                <Timeline
                  label={`Actividad del ${formatDate(g.date)}`}
                  items={g.items.map(e => ({
                    id: e.id,
                    date: e.createdAt,
                    kind: e.eventType === 'found_keys' ? ('status' as const) : ('info' as const),
                    title: EVENT_LABELS[e.eventType] ?? e.eventType,
                    detail: e.outcome,
                    emphasis: e.eventType === 'found_keys',
                  }))}
                />
              </div>
            )}
          </li>
        )
      })}
    </ul>
  )
}
