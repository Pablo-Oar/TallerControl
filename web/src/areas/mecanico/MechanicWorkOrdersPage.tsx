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
import { Pagination } from '../../components/ui/Pagination'
import { FormSelect } from '../../components/ui/FormField'

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
    <div className="space-y-4 pb-6">
      <h1 className="text-lg font-bold tracking-tight text-tc-text">Mis trabajos</h1>

      {/* Pestañas: órdenes mías / sin asignar (doc 06 /m) */}
      <div role="tablist" aria-label="Mis trabajos" className="flex border-b border-tc-border">
        {TABS.map(t => (
          <button
            key={t.id}
            role="tab"
            aria-selected={tab === t.id}
            onClick={() => { setTab(t.id); setPage(1) }}
            className={`min-h-[44px] flex-1 whitespace-nowrap border-b-4 px-1 text-sm font-bold tracking-wide uppercase transition-colors ${
              tab === t.id
                ? 'border-tc-purple text-tc-purple'
                : 'border-transparent text-tc-text-secondary hover:text-tc-text'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* Filtro de estado: un select compacto (la grilla de chips ocupaba ~210 px antes del primer trabajo) */}
      <FormSelect
        label="Estado"
        size="sm"
        value={statusFilter}
        onChange={e => { setStatusFilter(e.target.value as StatusFilter); setPage(1) }}
      >
        <option value="open">Activas</option>
        {MECHANIC_STATUSES.map(s => (
          <option key={s} value={s}>{STATUS_LABELS[s]}</option>
        ))}
        <option value="closed">Cerradas</option>
      </FormSelect>

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
        <div className="space-y-1.5">
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
          {visible.map(order => {
            const vehicle = [order.vehicleBrand, order.vehicleModel].filter(Boolean).join(' ')
            const mine = order.assignedMechanicId != null && order.assignedMechanicId === user?.membershipId
            return (
              <Link
                key={order.id}
                to={`/m/work-orders/${order.id}`}
                className="flex items-stretch overflow-hidden rounded-xl border border-tc-border bg-white transition-colors hover:bg-tc-surface"
              >
                {/* Talón: número de orden, igual que en el panel del taller */}
                <div className="flex w-14 shrink-0 items-center justify-center border-r border-tc-border bg-tc-stub-bg px-1">
                  <span className="text-xs font-bold tabular-nums text-tc-text-secondary">#{order.number}</span>
                </div>

                <div className="min-w-0 flex-1 px-3 py-3">
                  <p className="truncate text-sm text-tc-text-secondary">{order.customerName}</p>
                  <div className="mt-0.5 flex items-center gap-1.5">
                    <span className="font-mono text-sm font-bold tracking-widest text-tc-text">{order.vehiclePlate}</span>
                    {vehicle && <span className="truncate text-xs text-tc-text-secondary">— {vehicle}</span>}
                  </div>
                  <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1">
                    <StatusBadge status={order.status} label={STATUS_LABELS[order.status]} />
                    {order.priority === 'high' && (
                      <span className="border border-amber-600 bg-amber-100 px-1.5 py-0.5 text-xs font-bold uppercase tracking-wider text-amber-800">{PRIORITY_LABELS[order.priority]}</span>
                    )}
                  </div>
                  <p className="mt-1.5 truncate text-xs">
                    {order.assignedMechanicName == null ? (
                      order.status === 'delivered' || order.status === 'cancelled'
                        ? null
                        : <span className="font-semibold text-amber-700">Sin asignar</span>
                    ) : mine ? (
                      <span className="font-semibold text-tc-purple">{order.assignedMechanicName}</span>
                    ) : (
                      <span className="text-tc-text-secondary">{order.assignedMechanicName} · solo lectura</span>
                    )}
                  </p>
                </div>

                <div className="flex min-h-[44px] w-8 shrink-0 items-center justify-center border-l border-tc-border bg-tc-stub-bg text-tc-navy-text-dim" aria-hidden="true">
                  <svg className="h-3 w-3" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2">
                    <path d="M2 6h8M6 2l4 4-4 4" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                </div>
              </Link>
            )
          })}
        </div>
      )}

      {data && <Pagination page={page} pageSize={data.pageSize} total={data.total} noun="trabajos" onPage={setPage} />}
    </div>
  )
}
