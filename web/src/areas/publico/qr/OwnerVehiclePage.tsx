import { useState } from 'react'
import { useParams, useNavigate } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { getOwnerVehicle, getOwnerWorkOrder, type OwnerWorkOrderDto } from '../../../lib/api/qrCards'
import { Modal } from '../../../components/ui/Modal'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { Timeline, WORK_ORDER_MARKER, type TimelineItem } from '../../../components/ui/Timeline'
import { QUOTE_STATUS_LABELS } from '../../../lib/api/quotes'
import { formatDate, formatDateOnly } from '../../../lib/dates'
import { ArrowRightIcon } from '@/components/ui/icons'
import { WorkshopBrand } from '@/components/ui/WorkshopBrand'

const WO_STATUS_LABELS: Record<string, string> = {
  pending: 'Pendiente',
  in_progress: 'En proceso',
  waiting_parts: 'Esperando repuestos',
  completed: 'Completada',
  delivered: 'Entregada',
  cancelled: 'Cancelada',
}

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

const KIND_LABELS: Record<string, string> = { service: 'Servicio', product: 'Repuesto / producto', manual: 'Otro' }

/** Detalle de una orden: qué se hizo (servicios y repuestos). Sin costos ni importes por renglón. */
function OrderDetailModal({ code, number, onClose }: { code: string; number: number; onClose: () => void }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['owner-work-order', code, number],
    queryFn: () => getOwnerWorkOrder(code, number),
    retry: false,
  })

  return (
    <Modal title={`Orden #${number}`} onClose={onClose} className="max-w-lg">
      {isLoading && <p className="text-sm text-tc-text-muted">Cargando…</p>}
      {isError && <p className="text-sm text-tc-danger-text">No se pudo cargar el detalle. Escaneá el código QR nuevamente.</p>}
      {data && (
        <div className="space-y-4">
          <StatusBadge status={data.status} label={WO_STATUS_LABELS[data.status] ?? data.status} />
          <dl className="grid grid-cols-2 gap-px overflow-hidden rounded-xl border border-tc-border bg-tc-border text-sm">
            <div className="bg-white px-3 py-2">
              <dt className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Ingreso</dt>
              <dd className="font-medium text-tc-text">{formatDate(data.createdAt)}</dd>
            </div>
            <div className="bg-white px-3 py-2">
              <dt className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">
                {data.deliveredAt ? 'Entrega' : 'Terminada'}
              </dt>
              <dd className="font-medium text-tc-text">
                {data.deliveredAt ?? data.completedAt ? formatDate((data.deliveredAt ?? data.completedAt)!) : '—'}
              </dd>
            </div>
            <div className="col-span-2 bg-white px-3 py-2">
              <dt className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Odómetro</dt>
              <dd className="font-medium tabular-nums text-tc-text">{data.odometerKm.toLocaleString('es-AR')} km</dd>
            </div>
          </dl>

          {data.reportedIssue && (
            <div>
              <p className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Motivo de la consulta</p>
              <p className="mt-1 text-sm text-tc-text">{data.reportedIssue}</p>
            </div>
          )}

          <div>
            <p className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Trabajos realizados</p>
            {data.items.length === 0 ? (
              <p className="mt-1 text-sm text-tc-text-muted">Todavía no hay trabajos cargados.</p>
            ) : (
              <ul className="mt-2 divide-y divide-tc-border rounded-xl border border-tc-border">
                {data.items.map((it, i) => (
                  <li key={i} className="flex items-start justify-between gap-3 px-3 py-2.5 text-sm">
                    <div className="min-w-0">
                      <p className="font-medium text-tc-text">{it.description}</p>
                      <p className="text-xs text-tc-text-muted">{KIND_LABELS[it.kind] ?? it.kind}</p>
                    </div>
                    <span className="shrink-0 tabular-nums text-tc-text-secondary">
                      {it.quantity !== 1 ? `× ${Number(it.quantity).toLocaleString('es-AR')}` : ''}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {data.mechanicNotes && (
            <div>
              <p className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Observaciones del taller</p>
              <p className="mt-1 text-sm text-tc-text">{data.mechanicNotes}</p>
            </div>
          )}

          <div className="space-y-1.5 border-t border-tc-border pt-3">
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold text-tc-text">Total</span>
              <span className="text-lg font-bold tabular-nums text-tc-text">{fmtARS(data.total)}</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold text-tc-text">Adeudado</span>
              <span
                className={`text-lg tabular-nums ${
                  data.pending <= 0
                    ? 'font-bold text-tc-success-text'
                    : data.pending >= data.total
                      ? 'font-bold text-tc-danger-solid'
                      : 'font-bold text-tc-warning-solid'
                }`}
              >
                {fmtARS(Math.max(0, data.pending))}
              </span>
            </div>
          </div>
        </div>
      )}
    </Modal>
  )
}

/** Solo los campos que ya expone el DTO público (número, estado, fechas y total): el Timeline no recibe nada más. */
function toTimelineItem(wo: OwnerWorkOrderDto, onOpen: (n: number) => void): TimelineItem {
  const parts = [
    wo.completedAt ? `Entrega: ${formatDate(wo.completedAt)}` : null,
    wo.total != null ? `Total: ${fmtARS(wo.total)}` : null,
  ].filter(Boolean)
  return {
    id: String(wo.number),
    date: wo.createdAt,
    title: `OT #${wo.number}`,
    onSelect: () => onOpen(wo.number),
    kind: 'order',
    markerClass: WORK_ORDER_MARKER[wo.status],
    badge: <StatusBadge status={wo.status} label={WO_STATUS_LABELS[wo.status] ?? wo.status} />,
    detail: parts.length ? parts.join(' · ') : null,
  }
}

export function OwnerVehiclePage() {
  const { code } = useParams<{ code: string }>()
  const navigate = useNavigate()
  const [openOrder, setOpenOrder] = useState<number | null>(null)

  const { data, isLoading, isError } = useQuery({
    queryKey: ['owner-vehicle', code],
    queryFn: () => getOwnerVehicle(code!),
    retry: false,
  })

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-tc-navy">
        <div className="space-y-2 w-48">
          <div className="h-2 animate-pulse bg-tc-navy-mid" />
          <div className="h-2 w-3/4 animate-pulse bg-tc-navy-mid" />
        </div>
      </div>
    )
  }

  if (isError || !data) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-tc-navy px-4 text-center">
        <div className="rounded-xl overflow-hidden border border-tc-border bg-white p-8 max-w-sm w-full space-y-3">
          <p className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Sesión expirada</p>
          <p className="font-bold text-tc-text">Tu sesión venció</p>
          <p className="text-sm text-tc-text-secondary">
            Escaneá el código QR nuevamente para acceder.
          </p>
          <button
            className="mt-2 w-full border border-tc-navy bg-tc-navy py-2.5 text-xs font-bold tracking-wider uppercase text-white hover:bg-transparent hover:text-tc-navy transition-colors"
            onClick={() => navigate(`/q/${code}`, { replace: true })}
          >
            Volver al inicio
          </button>
        </div>
      </div>
    )
  }

  const vehicleTitle = [data.brand, data.model].filter(Boolean).join(' ') || 'Vehículo'

  return (
    <div className="min-h-screen bg-tc-surface pb-8">
      {/* Header */}
      <div className="bg-tc-navy border-b border-tc-border px-4 py-5">
        <div className="mx-auto max-w-md">
          <WorkshopBrand name={data.workshopName} logoUrl={data.logoUrl} address={data.workshopAddress} phone={data.workshopPhone} tone="dark" className="mb-4" />
          <p className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">
            Portal del dueño
          </p>
          <h1 className="mt-1 text-xl font-bold text-white">{vehicleTitle}</h1>
          <p className="mt-0.5 font-mono font-bold text-tc-purple tracking-widest">
            {data.plate}
          </p>
          <p className="mt-1 text-sm text-tc-text-muted">
            Odómetro: {data.odometerKm.toLocaleString('es-AR')} km
          </p>
        </div>
      </div>

      <div className="mx-auto max-w-md px-4 space-y-5 pt-5">
        {/* Balance pendiente */}
        {data.pendingBalance > 0 && (
          <div className="border border-tc-warning-border bg-tc-warning-bg p-4">
            <p className="text-[11px] font-bold tracking-wider uppercase text-tc-warning-text mb-1">Saldo pendiente</p>
            <p className="text-2xl font-bold text-tc-warning-text tabular-nums">
              {fmtARS(data.pendingBalance)}
            </p>
          </div>
        )}

        {/* Presupuestos activos */}
        {data.activeQuotes.length > 0 && (
          <div className="rounded-xl border border-tc-border bg-white overflow-hidden">
            <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
              <span className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Presupuestos activos</span>
            </div>
            <div className="divide-y divide-tc-border">
              {data.activeQuotes.map((q) => (
                <div key={q.number} className="px-4 py-3">
                  <div className="flex items-start justify-between">
                    <div>
                      <span className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Presupuesto #{q.number}</span>
                      <div className="mt-1"><StatusBadge status={q.status} label={(QUOTE_STATUS_LABELS as Record<string, string>)[q.status] ?? q.status.replace(/_/g, ' ')} /></div>
                    </div>
                    {q.validUntil && (
                      <span className="text-xs text-tc-text-muted shrink-0">
                        Vence {formatDateOnly(q.validUntil)}
                      </span>
                    )}
                  </div>
                  {q.publicLink && (
                    <a
                      href={q.publicLink}
                      className="mt-2 block text-xs font-bold tracking-wider uppercase text-tc-purple hover:opacity-70"
                    >
                      Ver presupuesto <ArrowRightIcon className="ml-0.5 inline-block h-4 w-4 align-text-bottom" />
                    </a>
                  )}
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Historial de OTs */}
        <div className="rounded-xl border border-tc-border bg-white overflow-hidden">
          <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
            <span className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Historial de órdenes de trabajo</span>
          </div>
          {data.workOrders.length === 0 ? (
            <p className="px-4 py-6 text-sm text-tc-text-muted">No hay órdenes registradas.</p>
          ) : (
            <div className="px-4 py-4">
              <Timeline items={data.workOrders.map(wo => toTimelineItem(wo, setOpenOrder))} showTime={false} label="Historial de órdenes de trabajo" />
            </div>
          )}
        </div>

        {openOrder != null && <OrderDetailModal code={code!} number={openOrder} onClose={() => setOpenOrder(null)} />}

        <p className="text-center text-xs text-tc-text-muted">
          Esta sesión es válida por 30 minutos.
        </p>
      </div>
    </div>
  )
}
