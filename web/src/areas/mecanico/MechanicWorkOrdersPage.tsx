import { useState } from 'react'
import { Link } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import {
  workOrdersApi,
  type WorkOrderStatus,
  STATUS_LABELS,
  PRIORITY_LABELS,
} from '../../lib/api/workOrders'
import { StatusBadge } from '../../components/ui/StatusBadge'

type Tab = 'mine' | 'unassigned'

const TABS: { id: Tab; label: string; empty: string }[] = [
  { id: 'mine', label: 'Mías', empty: 'No tenés órdenes asignadas. Mirá las que están sin asignar.' },
  { id: 'unassigned', label: 'Sin asignar', empty: 'No hay órdenes sin asignar.' },
]

const MECHANIC_STATUSES: WorkOrderStatus[] = ['pending', 'in_progress', 'waiting_parts']

export function MechanicWorkOrdersPage() {
  const [tab, setTab] = useState<Tab>('mine')
  const [statusFilter, setStatusFilter] = useState<WorkOrderStatus | ''>('')
  const [page, setPage] = useState(1)

  const { data, isLoading, error } = useQuery({
    queryKey: ['mechanic-work-orders', tab, statusFilter, page],
    queryFn: () =>
      workOrdersApi.search(statusFilter || null, null, null, null, null, null, page, 25, tab),
  })

  return (
    <div className="space-y-4 px-4 pb-6">
      <h1 className="text-lg font-bold tracking-tight text-slate-900">Mis trabajos</h1>

      {/* Pestañas: órdenes mías / sin asignar (doc 06 /m) */}
      <div role="tablist" aria-label="Mis trabajos" className="flex border-b border-tc-border">
        {TABS.map(t => (
          <button
            key={t.id}
            role="tab"
            aria-selected={tab === t.id}
            onClick={() => { setTab(t.id); setPage(1) }}
            className={`min-h-[44px] flex-1 border-b-2 px-3 text-sm font-bold tracking-wider uppercase transition-colors ${
              tab === t.id
                ? 'border-tc-purple text-tc-purple'
                : 'border-transparent text-slate-500 hover:text-slate-700'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* Filtros de estado — chips horizontales */}
      <div className="flex gap-2 overflow-x-auto pb-1">
        <button
          onClick={() => { setStatusFilter(''); setPage(1) }}
          className={`whitespace-nowrap border px-3 py-1 text-xs font-bold tracking-wider uppercase transition-colors ${
            statusFilter === ''
              ? 'border-tc-navy bg-tc-navy text-white'
              : 'border-tc-border bg-white text-slate-600 hover:bg-tc-surface'
          }`}
        >
          Todos
        </button>
        {MECHANIC_STATUSES.map(s => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setPage(1) }}
            className={`whitespace-nowrap border px-3 py-1 text-xs font-bold tracking-wider uppercase transition-colors ${
              statusFilter === s
                ? 'border-tc-navy bg-tc-navy text-white'
                : 'border-tc-border bg-white text-slate-600 hover:bg-tc-surface'
            }`}
          >
            {STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      {isLoading && (
        <div className="space-y-2">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="h-20 animate-pulse border border-tc-border bg-tc-surface" />
          ))}
        </div>
      )}

      {error && (
        <p className="border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
          No se pudo cargar los trabajos.
        </p>
      )}

      {data && (
        <div className="space-y-2">
          {data.items.length === 0 && (
            <p className="border border-dashed border-tc-border px-4 py-8 text-center text-sm text-slate-400">
              {TABS.find(t => t.id === tab)!.empty}
            </p>
          )}
          {data.items.map(order => (
            <Link
              key={order.id}
              to={`/m/work-orders/${order.id}`}
              className="flex items-stretch border border-tc-border bg-white hover:bg-tc-surface transition-colors"
            >
              {/* Stub lateral */}
              <div className="w-1.5 shrink-0 bg-tc-stub-bg border-r border-tc-border" />
              <div className="flex-1 px-4 py-3">
                <div className="flex items-start justify-between gap-2 mb-1">
                  <div className="flex items-center gap-2">
                    <span className="font-mono font-bold text-sm text-slate-800">#{order.number}</span>
                    <StatusBadge status={order.status} label={STATUS_LABELS[order.status]} />
                  </div>
                  <span className="text-xs text-slate-400 shrink-0">{PRIORITY_LABELS[order.priority]}</span>
                </div>
                <p className="font-medium text-sm text-slate-800">{order.customerName}</p>
                <p className="text-xs text-slate-500 mt-0.5">
                  <span className="font-mono tracking-widest">{order.vehiclePlate}</span>
                  {order.vehicleBrand && <> — {order.vehicleBrand} {order.vehicleModel}</>}
                </p>
                {order.assignedMechanicName == null && (
                  <p className="mt-1 text-[10px] font-bold tracking-wider uppercase text-amber-600">Sin asignar</p>
                )}
              </div>
            </Link>
          ))}
        </div>
      )}

      {data && data.total > data.pageSize && (
        <div className="flex items-center gap-1">
          <button
            onClick={() => setPage(p => p - 1)}
            disabled={page === 1}
            className="border border-tc-border bg-white px-3 py-1.5 text-xs disabled:opacity-40 hover:bg-tc-surface"
          >
            ← Anterior
          </button>
          <span className="border border-tc-border bg-tc-stub-bg px-3 py-1.5 text-xs tabular-nums">
            Pág. {page}
          </span>
          <button
            onClick={() => setPage(p => p + 1)}
            disabled={page * data.pageSize >= data.total}
            className="border border-tc-border bg-white px-3 py-1.5 text-xs disabled:opacity-40 hover:bg-tc-surface"
          >
            Siguiente →
          </button>
        </div>
      )}
    </div>
  )
}
