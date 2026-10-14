import { useState } from 'react'
import { Link } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  workOrdersApi,
  type WorkOrderListDto,
  type WorkOrderStatus,
  STATUS_LABELS,
  STATUS_COLORS,
  PRIORITY_LABELS,
} from '../../../lib/api/workOrders'
import { Button } from '../../../components/ui/Button'
import { WorkOrderFormModal } from './WorkOrderFormModal'

const STATUS_OPTIONS: WorkOrderStatus[] = [
  'pending', 'in_progress', 'waiting_parts', 'completed', 'delivered', 'cancelled',
]

export function WorkOrdersPage() {
  const queryClient = useQueryClient()
  const [statusFilter, setStatusFilter] = useState<WorkOrderStatus | ''>('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)

  const { data, isLoading, error } = useQuery({
    queryKey: ['work-orders', statusFilter, page],
    queryFn: () =>
      workOrdersApi.search(
        statusFilter || null,
        null, null, null, null, null,
        page,
      ),
  })

  function handleSuccess() {
    setCreating(false)
    queryClient.invalidateQueries({ queryKey: ['work-orders'] })
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Órdenes de trabajo</h1>
        <Button onClick={() => setCreating(true)}>Nueva orden</Button>
      </div>

      <div className="mb-4 flex flex-wrap gap-2">
        <select
          value={statusFilter}
          onChange={e => { setStatusFilter(e.target.value as WorkOrderStatus | ''); setPage(1) }}
          className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option value="">Todos los estados</option>
          {STATUS_OPTIONS.map(s => (
            <option key={s} value={s}>{STATUS_LABELS[s]}</option>
          ))}
        </select>
      </div>

      {isLoading && (
        <div className="flex justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
        </div>
      )}

      {error && (
        <p className="text-sm text-red-600">No se pudo cargar las órdenes.</p>
      )}

      {data && (
        <>
          <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">#</th>
                  <th className="px-4 py-3">Estado</th>
                  <th className="px-4 py-3">Prioridad</th>
                  <th className="px-4 py-3">Cliente</th>
                  <th className="px-4 py-3">Vehículo</th>
                  <th className="px-4 py-3">Mecánico</th>
                  <th className="px-4 py-3 text-right">Total</th>
                  <th className="px-4 py-3 text-right">Saldo</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={9} className="px-4 py-8 text-center text-slate-400">
                      No hay órdenes{statusFilter ? ' con ese estado' : ''}.
                    </td>
                  </tr>
                )}
                {data.items.map(order => (
                  <WorkOrderRow key={order.id} order={order} />
                ))}
              </tbody>
            </table>
          </div>

          {data.total > data.pageSize && (
            <div className="mt-4 flex items-center justify-between text-sm text-slate-500">
              <span>{data.total} órdenes</span>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => setPage(p => p - 1)}
                  disabled={page === 1}
                  className="rounded px-3 py-1 hover:bg-slate-100 disabled:opacity-40"
                >
                  Anterior
                </button>
                <span>Pág. {page}</span>
                <button
                  onClick={() => setPage(p => p + 1)}
                  disabled={page * data.pageSize >= data.total}
                  className="rounded px-3 py-1 hover:bg-slate-100 disabled:opacity-40"
                >
                  Siguiente
                </button>
              </div>
            </div>
          )}
        </>
      )}

      {creating && (
        <WorkOrderFormModal
          onSuccess={handleSuccess}
          onClose={() => setCreating(false)}
        />
      )}
    </div>
  )
}

function WorkOrderRow({ order }: { order: WorkOrderListDto }) {
  const vehicleLabel = [order.vehicleBrand, order.vehicleModel]
    .filter(Boolean).join(' ') || order.vehiclePlate

  return (
    <tr className="hover:bg-slate-50">
      <td className="px-4 py-3 font-mono font-medium">#{order.number}</td>
      <td className="px-4 py-3">
        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[order.status]}`}>
          {STATUS_LABELS[order.status]}
        </span>
      </td>
      <td className="px-4 py-3 text-slate-500">{PRIORITY_LABELS[order.priority]}</td>
      <td className="px-4 py-3">{order.customerName}</td>
      <td className="px-4 py-3">
        <span className="font-mono text-xs">{order.vehiclePlate}</span>
        {vehicleLabel !== order.vehiclePlate && (
          <span className="ml-1 text-slate-400 text-xs">— {vehicleLabel}</span>
        )}
      </td>
      <td className="px-4 py-3 text-slate-500">{order.assignedMechanicName ?? '—'}</td>
      <td className="px-4 py-3 text-right tabular-nums">
        {order.total != null ? `$${order.total.toFixed(2)}` : '—'}
      </td>
      <td className="px-4 py-3 text-right tabular-nums">
        {order.balance != null ? (
          <span className={order.balance > 0 ? 'text-amber-600 font-medium' : ''}>
            ${order.balance.toFixed(2)}
          </span>
        ) : '—'}
      </td>
      <td className="px-4 py-3">
        <Link
          to={`/app/work-orders/${order.id}`}
          className="text-xs text-blue-600 hover:underline"
        >
          Ver
        </Link>
      </td>
    </tr>
  )
}
