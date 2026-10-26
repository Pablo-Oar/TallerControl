import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  quotesApi,
  type QuoteListDto,
  type QuoteStatus,
  QUOTE_STATUS_LABELS,
} from '../../../lib/api/quotes'
import { Button } from '../../../components/ui/Button'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { DataTable, type DataTableColumn, type SortState } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { QuoteFormModal } from './QuoteFormModal'
import { formatDateOnly } from '../../../lib/dates'

const STATUS_OPTIONS: QuoteStatus[] = [
  'draft', 'sent', 'approved', 'partially_approved', 'rejected', 'expired', 'converted', 'cancelled',
]

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(n)

/** Columnas de la lista; el orden es del servidor (`number|status|total|validuntil|createdat`). */
const COLUMNS: DataTableColumn<QuoteListDto>[] = [
  {
    key: 'number',
    header: '#',
    align: 'center',
    widthClass: 'w-20',
    sortable: true,
    cell: q => <span className="text-xs font-bold tabular-nums text-tc-text-secondary">#{q.number}</span>,
  },
  {
    key: 'customer',
    header: 'Cliente / Vehículo',
    cell: q => (
      <div className="min-w-0">
        <p className="truncate font-medium text-tc-text">{q.customerName}</p>
        <p className="mt-0.5 font-mono text-[11px] font-bold tracking-widest text-tc-text-muted">{q.vehiclePlate}</p>
      </div>
    ),
  },
  {
    key: 'status',
    header: 'Estado',
    align: 'center',
    widthClass: 'w-40',
    sortable: true,
    cell: q => <StatusBadge status={q.status} />,
  },
  {
    key: 'total',
    header: 'Total',
    align: 'right',
    widthClass: 'w-32',
    sortable: true,
    cell: q => <span className="tabular-nums font-medium text-tc-purple">{fmtARS(q.total)}</span>,
  },
  {
    key: 'validuntil',
    header: 'Válido hasta',
    widthClass: 'w-36',
    sortable: true,
    cell: q => <span className="text-xs text-tc-text-secondary">{formatDateOnly(q.validUntil)}</span>,
  },
  {
    key: 'createdat',
    header: 'Creado',
    widthClass: 'w-36',
    sortable: true,
    cell: q => <span className="text-xs text-tc-text-muted">{new Date(q.createdAt).toLocaleDateString('es-AR')}</span>,
  },
]

export function QuotesPage() {
  const queryClient = useQueryClient()
  const [statusFilter, setStatusFilter] = useState<QuoteStatus | ''>('')
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState<SortState | null>(null)
  const [creating, setCreating] = useState(false)

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['quotes', statusFilter, page, sort],
    queryFn: () => quotesApi.search(statusFilter || null, null, null, null, null, page, 25, sort),
  })

  function handleSuccess() {
    setCreating(false)
    queryClient.invalidateQueries({ queryKey: ['quotes'] })
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
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

      <DataTable<QuoteListDto>
        label="Presupuestos"
        columns={COLUMNS}
        rows={data?.items ?? []}
        getRowId={q => q.id}
        rowHref={q => `/app/quotes/${q.id}`}
        rowLabel={q => `Ver presupuesto #${q.number}`}
        cardStub={q => <span className="text-xs font-bold tabular-nums text-tc-text-secondary">#{q.number}</span>}
        renderCard={q => (
          <>
            <p className="truncate text-sm font-medium text-tc-text">{q.customerName}</p>
            <p className="mt-0.5 font-mono text-[11px] font-bold tracking-widest text-tc-text-muted">{q.vehiclePlate}</p>
            <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1">
              <StatusBadge status={q.status} />
              <span className="tabular-nums text-sm font-medium text-tc-purple">{fmtARS(q.total)}</span>
            </div>
          </>
        )}
        sort={sort}
        onSortChange={s => { setSort(s); setPage(1) }}
        loading={isLoading}
        error={!!error}
        onRetry={() => refetch()}
        emptyMessage="No hay presupuestos."
      />

      {data && (
        <Pagination
          page={page}
          pageSize={data.pageSize}
          total={data.total}
          noun={`presupuesto${data.total !== 1 ? 's' : ''}`}
          onPage={setPage}
        />
      )}

      {creating && <QuoteFormModal onSuccess={handleSuccess} onClose={() => setCreating(false)} />}
    </div>
  )
}
