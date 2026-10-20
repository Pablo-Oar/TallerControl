/** Status stamp vocabulary for TallerControl (Jet-Age Ticket Wallet world). */

type WorkOrderStatus = 'pending' | 'in_progress' | 'waiting_parts' | 'completed' | 'delivered' | 'cancelled'
type QuoteStatus = 'draft' | 'sent' | 'approved' | 'partially_approved' | 'rejected' | 'expired' | 'converted' | 'cancelled'
type AppointmentStatus = 'pending' | 'confirmed' | 'completed' | 'cancelled'
type GenericStatus = WorkOrderStatus | QuoteStatus | AppointmentStatus | string

interface StatusBadgeProps {
  status: GenericStatus
  label?: string
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
  // Appointments
  confirmed:         { label: 'CONFIRMADO',   cls: 'border border-tc-done-border bg-tc-done-bg text-tc-done-text' },
}

export function StatusBadge({ status, label, className = '' }: StatusBadgeProps) {
  const found = config[status]
  const text = label ?? found?.label ?? status.toUpperCase().replace(/_/g, ' ')
  const cls = found?.cls ?? 'border border-slate-300 bg-slate-50 text-tc-text-secondary'

  return (
    <span
      // Ancho fijo y texto centrado: los sellos de una columna "Estado" se alinean aunque el texto cambie.
      className={`inline-flex w-28 shrink-0 items-center justify-center whitespace-nowrap px-2 py-0.5 text-center text-[10px] font-bold tracking-widest leading-none ${cls} ${className}`}
    >
      {text}
    </span>
  )
}
