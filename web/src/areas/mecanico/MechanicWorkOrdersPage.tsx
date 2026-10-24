import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import {
  workOrdersApi,
  type WorkOrderStatus,
  STATUS_LABELS,
  PRIORITY_LABELS,
} from '../../lib/api/workOrders'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { LoadError } from '../../components/LoadError'
import { useAuth } from '../../lib/auth/AuthContext'

type Tab = 'mine' | 'all' | 'unassigned'

const TABS: { id: Tab; label: string; empty: string }[] = [
  { id: 'mine', label: 'Mías', empty: 'No tenés órdenes asignadas. Mirá las que están sin asignar.' },
  { id: 'all', label: 'Todas', empty: 'El taller no tiene órdenes que coincidan.' },
  { id: 'unassigned', label: 'Sin asignar', empty: 'No hay órdenes sin asignar.' },
]

// «Activas» (por defecto) deja afuera las entregadas y anuladas; «Cerradas» las muestra.
const MECHANIC_STATUSES: WorkOrderStatus[] = ['pending', 'in_progress', 'waiting_parts', 'completed']

type StatusFilter = WorkOrderStatus | 'open' | 'closed'

export function MechanicWorkOrdersPage() {
  const [tab, setTab] = useState<Tab>('mine')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('open')
  const [page, setPage] = useState(1)
  const [params, setParams] = useSearchParams()
  // Búsqueda por patente de la barra del encabezado (`/m?q=`): filtra las órdenes ya cargadas, sin salir del área.
  const q = (params.get('q') ?? '').trim().toUpperCase().replace(/[^A-Z0-9]/g, '')
  const { user } = useAuth()

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['mechanic-work-orders', tab, statusFilter, page, q],
    queryFn: () =>
      workOrdersApi.search(
        statusFilter === 'open' || statusFilter === 'closed' ? null : statusFilter,
        null, null, null, null, null, page, 25, tab, null, null, q || null,
        statusFilter === 'open' ? false : statusFilter === 'closed' ? true : null,
      ),
  })

  const visible = data?.items ?? []

  return (
    <div className="space-y-4 px-4 pb-6">
      <h1 className="text-lg font-bold tracking-tight text-tc-text">Mis trabajos</h1>

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
                : 'border-transparent text-tc-text-secondary hover:text-tc-text-secondary'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* Filtros de estado — chips horizontales */}
      <div className="flex flex-wrap gap-2">
        <button
          onClick={() => { setStatusFilter('open'); setPage(1) }}
          className={`min-h-[40px] whitespace-nowrap border px-3 py-1 text-xs font-bold tracking-wider uppercase transition-colors ${
            statusFilter === 'open'
              ? 'border-tc-navy bg-tc-navy text-white'
              : 'border-tc-border bg-white text-tc-text-secondary hover:bg-tc-surface'
          }`}
        >
          Activas
        </button>
        {MECHANIC_STATUSES.map(s => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setPage(1) }}
            className={`min-h-[40px] whitespace-nowrap border px-3 py-1 text-xs font-bold tracking-wider uppercase transition-colors ${
              statusFilter === s
                ? 'border-tc-navy bg-tc-navy text-white'
                : 'border-tc-border bg-white text-tc-text-secondary hover:bg-tc-surface'
            }`}
          >
            {STATUS_LABELS[s]}
          </button>
        ))}
        <button
          onClick={() => { setStatusFilter('closed'); setPage(1) }}
          className={`min-h-[40px] whitespace-nowrap border px-3 py-1 text-xs font-bold tracking-wider uppercase transition-colors ${
            statusFilter === 'closed'
              ? 'border-tc-navy bg-tc-navy text-white'
              : 'border-tc-border bg-white text-tc-text-secondary hover:bg-tc-surface'
          }`}
        >
          Cerradas
        </button>
      </div>

      {isLoading && (
        <div className="space-y-2" role="status" aria-live="polite">
          <p className="text-xs font-bold tracking-wider uppercase text-tc-text-muted">Cargando trabajos…</p>
          {[...Array(4)].map((_, i) => (
            <div key={i} className="h-20 animate-pulse border border-tc-border bg-tc-surface" />
          ))}
        </div>
      )}

      {error && <LoadError what="los trabajos" onRetry={() => refetch()} />}

      {data && (
        <div className="space-y-2">
          {q && (
            <div className="flex items-center justify-between gap-2 border border-tc-border bg-white px-3 py-2 text-sm">
              <span>
                Patente: <span className="font-mono font-bold tracking-widest">{q}</span>
              </span>
              <button
                type="button"
                onClick={() => setParams({})}
                className="min-h-[44px] px-2 text-xs font-bold uppercase tracking-wider text-tc-purple"
              >
                Quitar filtro
              </button>
            </div>
          )}
          {visible.length === 0 && (
            <p className="border border-dashed border-tc-border px-4 py-8 text-center text-sm text-tc-text-muted">
              {q
                ? `Ninguna orden de esta pestaña tiene la patente ${q}.`
                : statusFilter === 'closed'
                  ? 'No hay órdenes cerradas (entregadas o anuladas) en esta pestaña.'
                  : TABS.find(t => t.id === tab)!.empty}
            </p>
          )}
          {visible.map(order => (
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
                    <span className="font-mono font-bold text-sm text-tc-text">#{order.number}</span>
                    <StatusBadge status={order.status} label={STATUS_LABELS[order.status]} />
                  </div>
                  <span className="text-xs text-tc-text-muted shrink-0">{PRIORITY_LABELS[order.priority]}</span>
                </div>
                <p className="font-medium text-sm text-tc-text">{order.customerName}</p>
                <p className="text-xs text-tc-text-secondary mt-0.5">
                  <span className="font-mono tracking-widest">{order.vehiclePlate}</span>
                  {order.vehicleBrand && <> — {order.vehicleBrand} {order.vehicleModel}</>}
                </p>
                {order.assignedMechanicName == null ? (
                  <p className="mt-1 text-[10px] font-bold tracking-wider uppercase text-amber-600">Sin asignar</p>
                ) : order.assignedMechanicId === user?.membershipId ? (
                  <p className="mt-1 text-xs font-bold text-tc-purple">{order.assignedMechanicName}</p>
                ) : (
                  <p className="mt-1 text-xs font-medium text-tc-text-muted">{order.assignedMechanicName} · solo lectura</p>
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
            className="min-h-[44px] border border-tc-border bg-white px-3 py-1.5 text-xs disabled:opacity-40 hover:bg-tc-surface"
          >
            ← Anterior
          </button>
          <span className="border border-tc-border bg-tc-stub-bg px-3 py-1.5 text-xs tabular-nums">
            Pág. {page}
          </span>
          <button
            onClick={() => setPage(p => p + 1)}
            disabled={page * data.pageSize >= data.total}
            className="min-h-[44px] border border-tc-border bg-white px-3 py-1.5 text-xs disabled:opacity-40 hover:bg-tc-surface"
          >
            Siguiente →
          </button>
        </div>
      )}
    </div>
  )
}
