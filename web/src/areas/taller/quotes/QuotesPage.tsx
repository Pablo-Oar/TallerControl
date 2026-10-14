import { useState } from 'react'
import { Link } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  quotesApi,
  type QuoteListDto,
  type QuoteStatus,
  QUOTE_STATUS_LABELS,
  QUOTE_STATUS_COLORS,
} from '../../../lib/api/quotes'
import { Button } from '../../../components/ui/Button'
import { QuoteFormModal } from './QuoteFormModal'

const STATUS_OPTIONS: QuoteStatus[] = [
  'draft', 'sent', 'approved', 'partially_approved', 'rejected', 'expired', 'converted', 'cancelled',
]

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
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Presupuestos</h1>
        <Button onClick={() => setCreating(true)}>Nuevo presupuesto</Button>
      </div>

      <div className="mb-4 flex flex-wrap gap-2">
        <select
          value={statusFilter}
          onChange={e => { setStatusFilter(e.target.value as QuoteStatus | ''); setPage(1) }}
          className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option value="">Todos los estados</option>
          {STATUS_OPTIONS.map(s => (
            <option key={s} value={s}>{QUOTE_STATUS_LABELS[s]}</option>
          ))}
        </select>
      </div>

      {isLoading && (
        <div className="flex justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
        </div>
      )}

      {error && (
        <p className="text-sm text-red-600">No se pudo cargar los presupuestos.</p>
      )}

      {data && (
        <>
          <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">#</th>
                  <th className="px-4 py-3">Estado</th>
                  <th className="px-4 py-3">Cliente</th>
                  <th className="px-4 py-3">Vehículo</th>
                  <th className="px-4 py-3 text-right">Total</th>
                  <th className="px-4 py-3">Válido hasta</th>
                  <th className="px-4 py-3">Creado</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.items.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="px-4 py-8 text-center text-slate-400">
                      No hay presupuestos
                    </td>
                  </tr>
                ) : (
                  data.items.map((q: QuoteListDto) => (
                    <tr key={q.id} className="hover:bg-slate-50">
                      <td className="px-4 py-3 font-medium">
                        <Link to={`/app/quotes/${q.id}`} className="text-blue-600 hover:underline">
                          #{q.number}
                        </Link>
                      </td>
                      <td className="px-4 py-3">
                        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${QUOTE_STATUS_COLORS[q.status]}`}>
                          {QUOTE_STATUS_LABELS[q.status]}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-slate-700">{q.customerName}</td>
                      <td className="px-4 py-3 text-slate-600">{q.vehiclePlate}</td>
                      <td className="px-4 py-3 text-right font-medium text-slate-800">
                        ${q.total.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
                      </td>
                      <td className="px-4 py-3 text-slate-600">
                        {new Date(q.validUntil).toLocaleDateString('es-AR')}
                      </td>
                      <td className="px-4 py-3 text-slate-500">
                        {new Date(q.createdAt).toLocaleDateString('es-AR')}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Paginación */}
          {(() => {
            const totalPages = Math.ceil(data.total / (data.pageSize || 25))
            return totalPages > 1 ? (
              <div className="mt-4 flex items-center justify-between text-sm text-slate-600">
                <p>
                  {data.total} presupuesto{data.total !== 1 ? 's' : ''}
                </p>
                <div className="flex gap-2">
                  <Button
                    variant="secondary"
                    disabled={page === 1}
                    onClick={() => setPage(p => p - 1)}
                  >
                    Anterior
                  </Button>
                  <span className="self-center">
                    Pág. {page} / {totalPages}
                  </span>
                  <Button
                    variant="secondary"
                    disabled={page >= totalPages}
                    onClick={() => setPage(p => p + 1)}
                  >
                    Siguiente
                  </Button>
                </div>
              </div>
            ) : null
          })()}
        </>
      )}

      {creating && (
        <QuoteFormModal
          onSuccess={handleSuccess}
          onClose={() => setCreating(false)}
        />
      )}
    </div>
  )
}
