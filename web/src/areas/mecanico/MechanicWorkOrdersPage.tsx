import { useState } from 'react'
import { Link } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import {
  workOrdersApi,
  type WorkOrderStatus,
  STATUS_LABELS,
  STATUS_COLORS,
  PRIORITY_LABELS,
} from '../../lib/api/workOrders'

const MECHANIC_STATUSES: WorkOrderStatus[] = ['pending', 'in_progress', 'waiting_parts']

export function MechanicWorkOrdersPage() {
  const [statusFilter, setStatusFilter] = useState<WorkOrderStatus | ''>('')
  const [page, setPage] = useState(1)

  const { data, isLoading, error } = useQuery({
    queryKey: ['mechanic-work-orders', statusFilter, page],
    queryFn: () =>
      workOrdersApi.search(statusFilter || null, null, null, null, null, null, page),
  })

  return (
    <div>
      <h1 className="mb-4 text-xl font-semibold">Mis trabajos</h1>

      <div className="mb-3 flex gap-2 overflow-x-auto pb-1">
        <button
          onClick={() => { setStatusFilter(''); setPage(1) }}
          className={`whitespace-nowrap rounded-full px-3 py-1 text-sm font-medium ${
            statusFilter === '' ? 'bg-blue-600 text-white' : 'bg-slate-200 text-slate-600'
          }`}
        >
          Todos
        </button>
        {MECHANIC_STATUSES.map(s => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setPage(1) }}
            className={`whitespace-nowrap rounded-full px-3 py-1 text-sm font-medium ${
              statusFilter === s ? 'bg-blue-600 text-white' : 'bg-slate-200 text-slate-600'
            }`}
          >
            {STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      {isLoading && (
        <div className="flex justify-center py-8">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
        </div>
      )}

      {error && <p className="text-sm text-red-600">No se pudo cargar los trabajos.</p>}

      {data && (
        <div className="space-y-3">
          {data.items.length === 0 && (
            <p className="py-6 text-center text-slate-400">No hay órdenes asignadas.</p>
          )}
          {data.items.map(order => (
            <Link
              key={order.id}
              to={`/m/work-orders/${order.id}`}
              className="block rounded-lg border border-slate-200 bg-white p-4 shadow-sm hover:border-blue-200"
            >
              <div className="flex items-start justify-between gap-2">
                <div>
                  <span className="font-mono font-medium">#{order.number}</span>
                  <span className={`ml-2 rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[order.status]}`}>
                    {STATUS_LABELS[order.status]}
                  </span>
                </div>
                <span className="text-xs text-slate-400">{PRIORITY_LABELS[order.priority]}</span>
              </div>
              <p className="mt-1 font-medium">{order.customerName}</p>
              <p className="text-sm text-slate-500">
                <span className="font-mono">{order.vehiclePlate}</span>
                {order.vehicleBrand && <> — {order.vehicleBrand} {order.vehicleModel}</>}
              </p>
              {order.assignedMechanicName == null && (
                <p className="mt-1 text-xs text-amber-600">Sin asignar</p>
              )}
            </Link>
          ))}
        </div>
      )}

      {data && data.total > data.pageSize && (
        <div className="mt-4 flex items-center justify-center gap-4 text-sm">
          <button
            onClick={() => setPage(p => p - 1)}
            disabled={page === 1}
            className="rounded-lg border border-slate-200 bg-white px-4 py-2 disabled:opacity-40"
          >
            Anterior
          </button>
          <span className="text-slate-500">Pág. {page}</span>
          <button
            onClick={() => setPage(p => p + 1)}
            disabled={page * data.pageSize >= data.total}
            className="rounded-lg border border-slate-200 bg-white px-4 py-2 disabled:opacity-40"
          >
            Siguiente
          </button>
        </div>
      )}
    </div>
  )
}
