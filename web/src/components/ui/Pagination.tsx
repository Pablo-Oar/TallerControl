import { cn } from '@/lib/utils'

/** Paginación de las listas: total a la izquierda, Anterior / Pág. N / Siguiente a la derecha. */
export function Pagination({
  page,
  pageSize,
  total,
  noun,
  onPage,
}: {
  page: number
  pageSize: number
  total: number
  /** Sustantivo en plural: "órdenes", "presupuestos". */
  noun: string
  onPage: (page: number) => void
}) {
  if (total <= pageSize) return null
  const btn = 'min-h-[36px] rounded-lg border border-tc-border bg-white px-3 text-xs font-medium text-tc-text-secondary transition-colors hover:bg-tc-surface disabled:opacity-40'
  return (
    <div className="flex items-center justify-between text-sm text-tc-text-secondary">
      <span className="tabular-nums">
        {total} {noun}
      </span>
      <div className="flex items-center gap-1">
        <button type="button" className={cn(btn)} onClick={() => onPage(page - 1)} disabled={page === 1}>
          Anterior
        </button>
        <span className="px-2 text-xs tabular-nums">Pág. {page}</span>
        <button type="button" className={cn(btn)} onClick={() => onPage(page + 1)} disabled={page * pageSize >= total}>
          Siguiente
        </button>
      </div>
    </div>
  )
}
