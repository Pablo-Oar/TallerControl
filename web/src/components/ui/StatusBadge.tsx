import { useRef, useState } from 'react'
import { createPortal } from 'react-dom'

/** Status stamp vocabulary for TallerControl (Jet-Age Ticket Wallet world). */

type WorkOrderStatus = 'pending' | 'in_progress' | 'waiting_parts' | 'completed' | 'delivered' | 'cancelled'
type QuoteStatus = 'draft' | 'sent' | 'approved' | 'partially_approved' | 'rejected' | 'expired' | 'converted' | 'cancelled'
type AppointmentStatus = 'pending' | 'confirmed' | 'completed' | 'cancelled'
type GenericStatus = WorkOrderStatus | QuoteStatus | AppointmentStatus | string

interface StatusBadgeProps {
  status: GenericStatus
  label?: string
  /** Texto del tooltip; si no se pasa, se usa el estándar del estado. */
  hint?: string
  className?: string
}

const config: Record<string, { label: string; cls: string }> = {
  // Work orders
  pending:           { label: 'PENDIENTE',    cls: 'border border-tc-pending-border bg-tc-pending-bg text-tc-pending-text' },
  in_progress:       { label: 'EN PROGRESO',  cls: 'border border-tc-inprog-border bg-tc-inprog-bg text-tc-inprog-text' },
  waiting_parts:     { label: 'ESPERANDO',    cls: 'border border-tc-warning-border bg-tc-warning-bg text-tc-warning-text' },
  completed:         { label: 'COMPLETADA',   cls: 'border border-tc-done-border bg-tc-done-bg text-tc-done-text' },
  delivered:         { label: 'ENTREGADA',    cls: 'border border-tc-success-border bg-tc-success-bg text-tc-success-text' },
  cancelled:         { label: 'ANULADA',      cls: 'border border-tc-void-border bg-white text-tc-void-text' },
  // Quotes
  draft:             { label: 'BORRADOR',     cls: 'border border-slate-300 bg-slate-50 text-tc-text-secondary' },
  sent:              { label: 'ENVIADO',      cls: 'border border-tc-inprog-border bg-tc-inprog-bg text-tc-inprog-text' },
  approved:          { label: 'APROBADO',     cls: 'border border-tc-done-border bg-tc-done-bg text-tc-done-text' },
  partially_approved:{ label: 'PARCIAL',      cls: 'border border-tc-success-border bg-tc-success-bg text-tc-success-text' },
  rejected:          { label: 'RECHAZADO',    cls: 'border border-tc-danger-border bg-tc-danger-bg text-tc-danger-text' },
  expired:           { label: 'VENCIDO',      cls: 'border border-slate-300 bg-slate-50 text-tc-text-secondary' },
  converted:         { label: 'CONVERTIDO',   cls: 'border border-tc-purple bg-tc-purple-light text-tc-purple-mid' },
  // Tiempo en el estado (Inicio): mismo tamaño que un sello de estado, para que las dos columnas se alineen
  time_stuck:        { label: 'ATRASADA',     cls: 'border border-tc-danger-border bg-tc-danger-bg text-tc-danger-text' },
  time_ok:           { label: 'AL DÍA',       cls: 'border border-slate-300 bg-slate-50 text-tc-text-secondary' },
  // Facturas ARCA
  authorized:        { label: 'AUTORIZADA',   cls: 'border border-tc-done-border bg-tc-done-bg text-tc-done-text' },
  // Appointments
  confirmed:         { label: 'CONFIRMADO',   cls: 'border border-tc-done-border bg-tc-done-bg text-tc-done-text' },
}

/** Qué significa cada estado (redacción común a órdenes, presupuestos, turnos, compras, ventas y tarjetas). */
const HINTS: Record<string, string> = {
  pending: 'Todavía no se empezó o falta confirmarlo',
  in_progress: 'Se está trabajando en el vehículo',
  waiting_parts: 'Detenida a la espera de repuestos',
  completed: 'Trabajo terminado',
  delivered: 'Vehículo entregado al cliente',
  cancelled: 'Anulado: ya no tiene validez',
  draft: 'Borrador: todavía no se envió ni se confirmó, se puede editar',
  sent: 'Esperando aprobación del cliente',
  approved: 'Aprobado por el cliente',
  partially_approved: 'El cliente aprobó solo una parte de los renglones',
  rejected: 'El cliente no lo aprobó',
  expired: 'Pasó la fecha de validez sin respuesta del cliente',
  converted: 'Aprobado por el cliente y transformado en Orden de Trabajo',
  confirmed: 'Confirmado',
  authorized: 'Autorizada por ARCA',
  time_stuck: 'Lleva más días en este estado de lo normal',
  time_ok: 'Dentro del plazo normal',
  free: 'Tarjeta sin asignar a un vehículo',
  assigned: 'Tarjeta asignada a un vehículo',
  lost: 'Tarjeta reportada como perdida',
  revoked: 'Tarjeta dada de baja de forma definitiva',
  valid: 'Cobro vigente',
  voided: 'Cobro anulado',
  active: 'Usuario con acceso al sistema',
  disabled: 'Usuario sin acceso al sistema',
  low_stock: 'Stock por debajo del mínimo',
  ok: 'Stock suficiente',
}

export function StatusBadge({ status, label, hint, className = '' }: StatusBadgeProps) {
  const found = config[status]
  const text = label ?? found?.label ?? status.toUpperCase().replace(/_/g, ' ')
  const cls = found?.cls ?? 'border border-slate-300 bg-slate-50 text-tc-text-secondary'

  const tip = hint ?? HINTS[status]
  // Se muestra en minúsculas con la inicial en mayúscula (las etiquetas vienen en mayúsculas).
  const shown = text.charAt(0).toUpperCase() + text.slice(1).toLowerCase()

  // El tooltip se dibuja en una capa fija (portal) para que ningún recuadro con overflow lo recorte,
  // y se corre hacia adentro si el sello está pegado al borde de la pantalla.
  const ref = useRef<HTMLSpanElement>(null)
  const [pos, setPos] = useState<{ x: number; y: number } | null>(null)

  function show() {
    const r = ref.current?.getBoundingClientRect()
    if (!r) return
    const half = 112 // mitad del ancho máximo del tooltip (14rem)
    const x = Math.min(Math.max(r.left + r.width / 2, half + 8), window.innerWidth - half - 8)
    setPos({ x, y: r.bottom + 6 })
  }

  return (
    <span ref={ref} className="inline-flex shrink-0 align-middle" onMouseEnter={show} onMouseLeave={() => setPos(null)}>
      <span
        // Píldora con borde, ícono y texto centrados; ancho mínimo parejo para que una columna "Estado" se alinee.
        className={`inline-flex min-w-[8.5rem] shrink-0 items-center justify-center gap-1.5 whitespace-nowrap rounded-full px-3 py-1 text-center text-xs font-bold leading-none ${cls} ${className}`}
      >
        <StatusIcon kind={ICON[status] ?? 'dot'} />
        {shown}
      </span>
      {tip && pos &&
        createPortal(
          <span
            role="tooltip"
            style={{ left: pos.x, top: pos.y }}
            className="pointer-events-none fixed z-[200] w-max max-w-56 -translate-x-1/2 border border-tc-border bg-white px-2.5 py-1.5 text-center text-xs font-normal leading-snug text-tc-text-secondary shadow-floating"
          >
            {tip}
          </span>,
          document.body,
        )}
    </span>
  )
}

type IconKind = 'check' | 'alert' | 'ban' | 'clock' | 'dot'

const ICON: Record<string, IconKind> = {
  completed: 'check', delivered: 'check', approved: 'check', converted: 'check', confirmed: 'check',
  authorized: 'check', valid: 'check', active: 'check', ok: 'check', assigned: 'check',
  cancelled: 'ban', rejected: 'ban', revoked: 'ban', voided: 'ban', disabled: 'ban',
  pending: 'alert', sent: 'alert', waiting_parts: 'alert', partially_approved: 'alert', low_stock: 'alert',
  time_stuck: 'alert', lost: 'alert',
  in_progress: 'clock', expired: 'clock', time_ok: 'clock',
}

/** Círculo relleno con el color del texto y un símbolo claro adentro (SVG propio, sin biblioteca de íconos). */
function StatusIcon({ kind }: { kind: IconKind }) {
  return (
    <svg viewBox="0 0 16 16" aria-hidden="true" className="h-3.5 w-3.5 shrink-0">
      <circle cx="8" cy="8" r="8" fill="currentColor" />
      {kind === 'check' && <path d="m4.6 8.4 2.2 2.2 4.6-4.8" fill="none" stroke="#fff" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />}
      {kind === 'alert' && (
        <>
          <path d="M8 4.2v4.4" stroke="#fff" strokeWidth="1.8" strokeLinecap="round" />
          <circle cx="8" cy="11.3" r="1" fill="#fff" />
        </>
      )}
      {kind === 'ban' && <path d="m4.8 11.2 6.4-6.4" stroke="#fff" strokeWidth="1.8" strokeLinecap="round" />}
      {kind === 'clock' && <path d="M8 4.3V8l2.4 1.5" fill="none" stroke="#fff" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />}
      {kind === 'dot' && <circle cx="8" cy="8" r="2.4" fill="#fff" />}
    </svg>
  )
}
