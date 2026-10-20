import { useState } from 'react'
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
import { DataTable, type DataTableColumn, type SortState } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { WorkOrderFormModal } from './WorkOrderFormModal'

const STATUS_OPTIONS: WorkOrderStatus[] = [
  'pending', 'in_progress', 'waiting_parts', 'completed', 'delivered', 'cancelled',
]

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(n)

const vehicleLabel = (o: WorkOrderListDto) =>
  [o.vehicleBrand, o.vehicleModel].filter(Boolean).join(' ') || o.vehiclePlate

/** Columnas de la lista; el orden es del servidor (`number|status|total|balance`). */
const COLUMNS: DataTableColumn<WorkOrderListDto>[] = [
  {
    key: 'number',
    header: '#',
    align: 'center',
    widthClass: 'w-20',
    sortable: true,
    cell: o => <span className="text-xs font-bold tabular-nums text-tc-text-secondary">#{o.number}</span>,
  },
  {
    key: 'customer',
    header: 'Cliente / Vehículo',
    cell: o => (
      <div className="min-w-0">
        <p className="truncate font-medium text-tc-text">{o.customerName}</p>
        <div className="mt-0.5 flex items-center gap-1.5">
          <span className="font-mono text-[11px] font-bold tracking-widest text-tc-text-muted">{o.vehiclePlate}</span>
          {vehicleLabel(o) !== o.vehiclePlate && <span className="truncate text-xs text-tc-text-muted">— {vehicleLabel(o)}</span>}
          <span className="shrink-0 text-xs text-tc-text-muted">· {PRIORITY_LABELS[o.priority]}</span>
        </div>
      </div>
    ),
  },
  {
    key: 'status',
    header: 'Estado',
    align: 'center',
    widthClass: 'w-40',
    sortable: true,
    cell: o => <StatusBadge status={o.status} />,
  },
  {
    key: 'total',
    header: 'Total',
    align: 'right',
    widthClass: 'w-32',
    sortable: true,
    cell: o => (
      <span className="tabular-nums font-medium text-tc-purple">{o.total != null ? fmtARS(o.total) : '—'}</span>
    ),
  },
  {
    key: 'balance',
    header: 'Saldo',
    align: 'right',
    widthClass: 'w-32',
    sortable: true,
    cell: o =>
      o.balance != null ? (
        <span className={['tabular-nums', o.balance > 0 ? 'font-semibold text-amber-600' : 'text-tc-text-muted'].join(' ')}>
          {fmtARS(o.balance)}
        </span>
      ) : (
        <span className="text-tc-text-muted">—</span>
      ),
  },
  {
    key: 'mechanic',
    header: 'Mecánico',
    widthClass: 'w-44',
    cell: o => <span className="block max-w-[10rem] truncate text-xs text-tc-text-muted">{o.assignedMechanicName ?? '—'}</span>,
  },
]

export function WorkOrdersPage() {
  const queryClient = useQueryClient()
  const [statusFilter, setStatusFilter] = useState<WorkOrderStatus | ''>('')
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState<SortState | null>(null)
  const [creating, setCreating] = useState(false)

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['work-orders', statusFilter, page, sort],
    queryFn: () =>
      workOrdersApi.search(statusFilter || null, null, null, null, null, null, page, 25, null, sort),
  })

  function handleSuccess() {
    setCreating(false)
    queryClient.invalidateQueries({ queryKey: ['work-orders'] })
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Órdenes de trabajo</h1>
        <Button onClick={() => setCreating(true)} size="sm">Nueva orden</Button>
      </div>

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

      <DataTable<WorkOrderListDto>
        label="Órdenes de trabajo"
        columns={COLUMNS}
        rows={data?.items ?? []}
        getRowId={o => o.id}
        rowHref={o => `/app/work-orders/${o.id}`}
        rowLabel={o => `Ver orden #${o.number}`}
        cardStub={o => <span className="text-xs font-bold tabular-nums text-tc-text-secondary">#{o.number}</span>}
        renderCard={o => (
          <>
            <p className="truncate text-sm font-medium text-tc-text">{o.customerName}</p>
            <div className="mt-0.5 flex items-center gap-1.5">
              <span className="font-mono text-[11px] font-bold tracking-widest text-tc-text-muted">{o.vehiclePlate}</span>
              {vehicleLabel(o) !== o.vehiclePlate && <span className="truncate text-xs text-tc-text-muted">— {vehicleLabel(o)}</span>}
              <span className="shrink-0 text-xs text-tc-text-muted">· {PRIORITY_LABELS[o.priority]}</span>
            </div>
            <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1">
              <StatusBadge status={o.status} />
              {o.total != null && <span className="tabular-nums text-sm font-medium text-tc-purple">{fmtARS(o.total)}</span>}
            </div>
          </>
        )}
        sort={sort}
        onSortChange={s => { setSort(s); setPage(1) }}
        loading={isLoading}
        error={!!error}
        onRetry={() => refetch()}
        emptyMessage={`No hay órdenes${statusFilter ? ' con ese estado' : ''}.`}
      />

      {data && <Pagination page={page} pageSize={data.pageSize} total={data.total} noun="órdenes" onPage={setPage} />}

      {creating && <WorkOrderFormModal onSuccess={handleSuccess} onClose={() => setCreating(false)} />}
    </div>
  )
}
