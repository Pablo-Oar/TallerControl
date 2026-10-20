import { useState } from 'react'
import { Link } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  quotesApi,
  type QuoteListDto,
  type QuoteStatus,
  QUOTE_STATUS_LABELS,
} from '../../../lib/api/quotes'
import { Button } from '../../../components/ui/Button'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { QuoteFormModal } from './QuoteFormModal'

const STATUS_OPTIONS: QuoteStatus[] = [
  'draft', 'sent', 'approved', 'partially_approved', 'rejected', 'expired', 'converted', 'cancelled',
]

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(n)

export function QuotesPage() {
  const queryClient = useQueryClient()
  const [statusFilter, setStatusFilter] = useState<QuoteStatus | ''>('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)

  const { data, isLoading, error } = useQuery({
    queryKey: ['quotes', statusFilter, page],
    queryFn: () => quotesApi.search(statusFilter || null, null, null, null, null, page),
  })

  function handleSuccess() {
    setCreating(false)
    queryClient.invalidateQueries({ queryKey: ['quotes'] })
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Presupuestos</h1>
        <Button onClick={() => setCreating(true)} size="sm">Nuevo presupuesto</Button>
      </div>

      <div className="flex flex-wrap gap-2">
        <select
          value={statusFilter}
          onChange={e => { setStatusFilter(e.target.value as QuoteStatus | ''); setPage(1) }}
          className="border border-tc-border bg-white px-3 py-1.5 text-sm text-tc-text-secondary outline-none focus:border-tc-border-mid"
        >
          <option value="">Todos los estados</option>
          {STATUS_OPTIONS.map(s => (
            <option key={s} value={s}>{QUOTE_STATUS_LABELS[s]}</option>
          ))}
        </select>
      </div>

      {isLoading && (
        <div className="space-y-1">
          {[0, 1, 2, 3].map(i => (
            <div key={i} className="h-14 animate-pulse bg-slate-50 border border-tc-border" />
          ))}
        </div>
      )}

      {error && (
        <p className="border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          No se pudo cargar los presupuestos.
        </p>
      )}

      {data && (
        <>
          <div className="border border-tc-border bg-white overflow-hidden">
            {/* Header */}
            <div className="grid grid-cols-[3rem_1fr_7rem_6rem] md:grid-cols-[3rem_1fr_7rem_6rem_7rem_7rem] border-b border-tc-border bg-tc-stub-bg">
              {[['#', '', false], ['Cliente / Vehículo', '', false], ['Estado', '', false], ['Total', 'text-right', false], ['Válido hasta', '', true], ['Creado', '', true]].map(([h, cls, hide], i) => (
                <div key={i} className={`px-4 py-2 text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted ${cls} ${hide ? 'hidden md:block' : ''}`}>
                  {h}
                </div>
              ))}
            </div>

            {data.items.length === 0 ? (
              <p className="px-4 py-8 text-center text-sm text-tc-text-muted">No hay presupuestos.</p>
            ) : (
              <ul className="divide-y divide-tc-border">
                {data.items.map((q: QuoteListDto) => (
                  <li
                    key={q.id}
                    className="grid grid-cols-[3rem_1fr_7rem_6rem] md:grid-cols-[3rem_1fr_7rem_6rem_7rem_7rem] items-center hover:bg-tc-surface transition-colors"
                  >
                    {/* Stub: number */}
                    <div className="flex h-full items-center border-r border-tc-border bg-tc-stub-bg px-3 py-3">
                      <Link
                        to={`/app/quotes/${q.id}`}
                        className="text-xs font-bold tabular-nums text-tc-purple hover:text-tc-purple-mid"
                      >
                        #{q.number}
                      </Link>
                    </div>

                    {/* Customer + vehicle */}
                    <div className="px-4 py-3">
                      <p className="text-sm font-medium text-tc-text truncate">{q.customerName}</p>
                      <p className="mt-0.5 text-[11px] font-bold tracking-widest text-tc-text-muted font-mono">{q.vehiclePlate}</p>
                    </div>

                    {/* Status */}
                    <div className="px-2 py-3">
                      <StatusBadge status={q.status} />
                    </div>

                    {/* Total */}
                    <div className="px-4 py-3 text-right">
                      <span className="tabular-nums text-sm font-medium text-tc-purple">
                        {fmtARS(q.total)}
                      </span>
                    </div>

                    {/* Valid until */}
                    <div className="hidden md:block px-4 py-3">
                      <span className="text-xs text-tc-text-secondary">
                        {new Date(q.validUntil).toLocaleDateString('es-AR')}
                      </span>
                    </div>

                    {/* Created */}
                    <div className="hidden md:block px-4 py-3">
                      <span className="text-xs text-tc-text-muted">
                        {new Date(q.createdAt).toLocaleDateString('es-AR')}
                      </span>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {data.total > data.pageSize && (
            <div className="flex items-center justify-between text-sm text-tc-text-secondary">
              <span className="tabular-nums">{data.total} presupuesto{data.total !== 1 ? 's' : ''}</span>
              <div className="flex items-center gap-1">
                <button
                  onClick={() => setPage(p => p - 1)}
                  disabled={page === 1}
                  className="border border-tc-border px-3 py-1 text-xs hover:bg-tc-surface disabled:opacity-40 transition-colors"
                >
                  Anterior
                </button>
                <span className="px-2 text-xs tabular-nums">Pág. {page} / {Math.ceil(data.total / (data.pageSize || 25))}</span>
                <button
                  onClick={() => setPage(p => p + 1)}
                  disabled={page * (data.pageSize || 25) >= data.total}
                  className="border border-tc-border px-3 py-1 text-xs hover:bg-tc-surface disabled:opacity-40 transition-colors"
                >
                  Siguiente
                </button>
              </div>
            </div>
          )}
        </>
      )}

      {creating && (
        <QuoteFormModal onSuccess={handleSuccess} onClose={() => setCreating(false)} />
      )}
    </div>
  )
}
