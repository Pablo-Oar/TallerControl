import { useState } from 'react'
import { Link } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  workOrdersApi,
  type WorkOrderListDto,
  type WorkOrderStatus,
  STATUS_LABELS,
  PRIORITY_LABELS,
} from '../../../lib/api/workOrders'
import { Button } from '../../../components/ui/Button'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { WorkOrderFormModal } from './WorkOrderFormModal'

const STATUS_OPTIONS: WorkOrderStatus[] = [
  'pending', 'in_progress', 'waiting_parts', 'completed', 'delivered', 'cancelled',
]

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(n)

export function WorkOrdersPage() {
  const queryClient = useQueryClient()
  const [statusFilter, setStatusFilter] = useState<WorkOrderStatus | ''>('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)

  const { data, isLoading, error } = useQuery({
    queryKey: ['work-orders', statusFilter, page],
    queryFn: () =>
      workOrdersApi.search(statusFilter || null, null, null, null, null, null, page),
  })

  function handleSuccess() {
    setCreating(false)
    queryClient.invalidateQueries({ queryKey: ['work-orders'] })
  }

  return (
    <div className="space-y-5">
      {/* Page header */}
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">
          Órdenes de trabajo
        </h1>
        <Button onClick={() => setCreating(true)} size="sm">Nueva orden</Button>
      </div>

      {/* Filter bar */}
      <div className="flex flex-wrap gap-2">
        <select
          value={statusFilter}
          onChange={e => { setStatusFilter(e.target.value as WorkOrderStatus | ''); setPage(1) }}
          className="border border-tc-border bg-white px-3 py-1.5 text-sm text-tc-text-secondary outline-none focus:border-tc-border-mid"
        >
          <option value="">Todos los estados</option>
          {STATUS_OPTIONS.map(s => (
            <option key={s} value={s}>{STATUS_LABELS[s]}</option>
          ))}
        </select>
      </div>

      {/* Loading skeleton */}
      {isLoading && (
        <div className="space-y-1">
          {[0, 1, 2, 3, 4].map(i => (
            <div key={i} className="h-14 animate-pulse bg-slate-50 border border-tc-border" />
          ))}
        </div>
      )}

      {error && (
        <p className="border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          No se pudo cargar las órdenes.
        </p>
      )}

      {data && (
        <>
          {/* Coupon stack */}
          <div className="border border-tc-border bg-white overflow-hidden">
            {/* Column header */}
            <div className="hidden md:grid md:grid-cols-[4rem_minmax(0,1fr)_9rem_8rem_8rem_10rem_3rem] items-center gap-0 border-b border-tc-border bg-tc-stub-bg px-0">
              <div className="px-3 py-2 text-center text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">#</div>
              <div className="px-4 py-2 text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Cliente / Vehículo</div>
              <div className="px-2 py-2 text-center text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Estado</div>
              <div className="hidden md:block px-4 py-2 text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted text-right">Total</div>
              <div className="hidden md:block px-4 py-2 text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted text-right">Saldo</div>
              <div className="hidden md:block px-4 py-2 text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Mecánico</div>
              <div />
            </div>

            {/* Rows */}
            {data.items.length === 0 ? (
              <p className="px-4 py-8 text-center text-sm text-tc-text-muted">
                No hay órdenes{statusFilter ? ' con ese estado' : ''}.
              </p>
            ) : (
              <ul className="divide-y divide-tc-border">
                {data.items.map(order => (
                  <WorkOrderRow key={order.id} order={order} />
                ))}
              </ul>
            )}
          </div>

          {/* Pagination */}
          {data.total > data.pageSize && (
            <div className="flex items-center justify-between text-sm text-tc-text-secondary">
              <span className="tabular-nums">{data.total} órdenes</span>
              <div className="flex items-center gap-1">
                <button
                  onClick={() => setPage(p => p - 1)}
                  disabled={page === 1}
                  className="border border-tc-border px-3 py-1 text-xs hover:bg-tc-surface disabled:opacity-40 transition-colors"
                >
                  Anterior
                </button>
                <span className="px-2 text-xs tabular-nums">Pág. {page}</span>
                <button
                  onClick={() => setPage(p => p + 1)}
                  disabled={page * data.pageSize >= data.total}
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
        <WorkOrderFormModal onSuccess={handleSuccess} onClose={() => setCreating(false)} />
      )}
    </div>
  )
}

function WorkOrderRow({ order }: { order: WorkOrderListDto }) {
  const vehicleLabel = [order.vehicleBrand, order.vehicleModel].filter(Boolean).join(' ') || order.vehiclePlate

  return (
    <li className="grid grid-cols-[3.5rem_minmax(0,1fr)_2.75rem] md:grid-cols-[4rem_minmax(0,1fr)_9rem_8rem_8rem_10rem_3rem] items-center gap-0 hover:bg-tc-surface group transition-colors">
      {/* Stub: number */}
      <div className="flex h-full items-center justify-center border-r border-tc-border bg-tc-stub-bg px-2 py-3">
        <span className="text-xs font-bold tabular-nums text-tc-text-secondary">#{order.number}</span>
      </div>

      {/* Body: customer + vehicle */}
      <div className="min-w-0 px-3 py-3 md:px-4">
        <p className="text-sm font-medium text-tc-text truncate">{order.customerName}</p>
        <div className="mt-0.5 flex items-center gap-1.5">
          <span className="text-[11px] font-bold tracking-widest text-tc-text-muted font-mono">{order.vehiclePlate}</span>
          {vehicleLabel !== order.vehiclePlate && (
            <span className="text-xs text-tc-text-muted truncate">— {vehicleLabel}</span>
          )}
          <span className="shrink-0 text-xs text-tc-text-muted">· {PRIORITY_LABELS[order.priority]}</span>
        </div>
        {/* Móvil: estado y total debajo, así nada se recorta */}
        <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 md:hidden">
          <StatusBadge status={order.status} />
          {order.total != null && (
            <span className="tabular-nums text-sm font-medium text-tc-purple">{fmtARS(order.total)}</span>
          )}
        </div>
      </div>

      {/* Status stamp */}
      <div className="hidden md:block px-2 py-3">
        <StatusBadge status={order.status} />
      </div>

      {/* Total */}
      <div className="hidden md:block px-4 py-3 text-right">
        <span className="tabular-nums text-sm text-tc-purple font-medium">
          {order.total != null ? fmtARS(order.total) : '—'}
        </span>
      </div>

      {/* Balance */}
      <div className="hidden md:block px-4 py-3 text-right">
        {order.balance != null ? (
          <span className={['tabular-nums text-sm', order.balance > 0 ? 'font-semibold text-amber-600' : 'text-tc-text-muted'].join(' ')}>
            {fmtARS(order.balance)}
          </span>
        ) : (
          <span className="text-tc-text-muted">—</span>
        )}
      </div>

      {/* Mechanic */}
      <div className="hidden min-w-0 md:block px-3 py-3">
        <span className="block truncate text-xs text-tc-text-muted">{order.assignedMechanicName ?? '—'}</span>
      </div>

      {/* Nav arrow */}
      <div className="flex h-full min-h-[44px] items-center justify-center border-l border-tc-border bg-tc-stub-bg px-1">
        <Link
          to={`/app/work-orders/${order.id}`}
          className="flex h-full w-full items-center justify-center text-tc-navy-text-dim hover:text-tc-purple transition-colors"
          aria-label={`Ver orden #${order.number}`}
        >
          <svg className="h-3 w-3" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
            <path d="M2 6h8M6 2l4 4-4 4" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </Link>
      </div>
    </li>
  )
}
