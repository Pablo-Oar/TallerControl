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
  waiting_parts:     { label: 'ESPERANDO',    cls: 'border border-amber-300 bg-amber-50 text-amber-700' },
  completed:         { label: 'COMPLETADA',   cls: 'border border-tc-done-border bg-tc-done-bg text-tc-done-text' },
  delivered:         { label: 'ENTREGADA',    cls: 'border border-teal-300 bg-teal-50 text-teal-700' },
  cancelled:         { label: 'ANULADA',      cls: 'border border-tc-void-border bg-white text-tc-void-text line-through' },
  // Quotes
  draft:             { label: 'BORRADOR',     cls: 'border border-slate-300 bg-slate-50 text-tc-text-secondary' },
  sent:              { label: 'ENVIADO',      cls: 'border border-tc-inprog-border bg-tc-inprog-bg text-tc-inprog-text' },
  approved:          { label: 'APROBADO',     cls: 'border border-tc-done-border bg-tc-done-bg text-tc-done-text' },
  partially_approved:{ label: 'PARCIAL',      cls: 'border border-teal-300 bg-teal-50 text-teal-700' },
  rejected:          { label: 'RECHAZADO',    cls: 'border border-red-300 bg-red-50 text-red-700' },
  expired:           { label: 'VENCIDO',      cls: 'border border-slate-300 bg-slate-50 text-tc-text-secondary' },
  converted:         { label: 'CONVERTIDO',   cls: 'border border-tc-purple bg-tc-purple-light text-tc-purple-mid' },
  // Tiempo en el estado (Inicio): mismo tamaño que un sello de estado, para que las dos columnas se alineen
  time_stuck:        { label: 'ATRASADA',     cls: 'border border-red-300 bg-red-50 text-red-700' },
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
  time_stuck: 'Hace más días en este estado de lo que indica el aviso del Inicio',
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

  return (
    <span className="group/st relative inline-flex shrink-0 align-middle">
      <span
        // Ancho fijo y texto centrado: los sellos de una columna "Estado" se alinean aunque el texto cambie.
        // El espaciado entre letras deja un hueco al final: se compensa a la izquierda para que se vea centrado.
        className={`inline-flex w-28 shrink-0 items-center justify-center whitespace-nowrap py-0.5 pr-2 pl-[calc(0.5rem+0.1em)] text-center text-[11px] font-bold tracking-widest leading-none ${cls} ${className}`}
      >
        {text}
      </span>
      {tip && (
        <span
          role="tooltip"
          className="pointer-events-none invisible absolute top-full left-1/2 z-30 mt-1.5 w-max max-w-56 -translate-x-1/2 border border-tc-border bg-white px-2.5 py-1.5 text-center text-xs font-normal normal-case tracking-normal leading-snug text-tc-text-secondary opacity-0 shadow-lg transition-opacity group-hover/st:visible group-hover/st:opacity-100"
        >
          {tip}
        </span>
      )}
    </span>
  )
}
