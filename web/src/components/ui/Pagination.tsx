import { cn } from '@/lib/utils'

/** Tamaño de página de todas las listas. */
export const PAGE_SIZE = 10

/** Números a mostrar: primera, última y una ventana alrededor de la actual; `null` = «…». */
export function pageWindow(page: number, pages: number): (number | null)[] {
  if (pages <= 7) return Array.from({ length: pages }, (_, i) => i + 1)
  const out: (number | null)[] = [1]
  const from = Math.max(2, page - 1)
  const to = Math.min(pages - 1, page + 1)
  if (from > 2) out.push(null)
  for (let p = from; p <= to; p++) out.push(p)
  if (to < pages - 1) out.push(null)
  out.push(pages)
  return out
}

/** Paginación de las listas: «Mostrando 11–20 de 57» y un índice numerado con Anterior / Siguiente. */
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
  const pages = Math.ceil(total / pageSize)
  const first = (page - 1) * pageSize + 1
  const last = Math.min(page * pageSize, total)
  const btn =
    'inline-flex min-h-[44px] min-w-[44px] items-center justify-center rounded-lg border border-tc-border bg-white px-2 text-xs font-medium tabular-nums text-tc-text-secondary transition-colors hover:bg-tc-surface disabled:opacity-40 md:min-h-[36px] md:min-w-[36px]'
  return (
    <nav aria-label="Paginación" className="flex flex-col items-center gap-2 text-sm text-tc-text-secondary sm:flex-row sm:justify-between">
      <span className="tabular-nums">
        Mostrando {first}–{last} de {total} {noun}
      </span>
      <div className="flex flex-wrap items-center justify-center gap-1">
        <button type="button" className={btn} onClick={() => onPage(page - 1)} disabled={page === 1}>
          <span aria-hidden>←</span>
          <span className="sr-only">Anterior</span>
        </button>
        {pageWindow(page, pages).map((p, i) =>
          p === null ? (
            <span key={`gap-${i}`} aria-hidden className="px-1 text-tc-text-muted">…</span>
          ) : (
            <button
              key={p}
              type="button"
              aria-label={`Página ${p}`}
              aria-current={p === page ? 'page' : undefined}
              onClick={() => onPage(p)}
              className={cn(btn, p === page && 'border-tc-purple bg-tc-purple font-bold text-white hover:bg-tc-purple')}
            >
              {p}
            </button>
          ),
        )}
        <button type="button" className={btn} onClick={() => onPage(page + 1)} disabled={page >= pages}>
          <span aria-hidden>→</span>
          <span className="sr-only">Siguiente</span>
        </button>
      </div>
    </nav>
  )
}
