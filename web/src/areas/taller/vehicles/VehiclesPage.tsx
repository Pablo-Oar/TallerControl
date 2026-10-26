import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { vehiclesApi, type VehicleDto, type VehicleListDto } from '../../../lib/api/vehicles'
import { Button } from '../../../components/ui/Button'
import { DataTable, RowAction, type DataTableColumn, type SortState } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { notify } from '../../../components/ui/dialogs'
import { VehicleFormModal } from './VehicleFormModal'

const COLUMNS: DataTableColumn<VehicleListDto>[] = [
  {
    key: 'plate',
    header: 'Patente',
    sortable: true,
    widthClass: 'w-36',
    cell: v => <span className="font-mono text-sm font-bold tracking-widest text-tc-text">{v.plate}</span>,
  },
  {
    key: 'brand',
    header: 'Marca / Modelo',
    sortable: true,
    cell: v => (
      <span className="truncate text-tc-text-secondary">
        {[v.brand, v.model].filter(Boolean).join(' ') || <span className="text-tc-text-muted">—</span>}
      </span>
    ),
  },
  {
    key: 'year',
    header: 'Año',
    sortable: true,
    align: 'right',
    widthClass: 'w-24',
    cell: v => <span className="tabular-nums text-tc-text-secondary">{v.year ?? '—'}</span>,
  },
  { key: 'customer', header: 'Cliente', sortable: true, cell: v => <span className="text-tc-text-secondary">{v.customerName}</span> },
]

export function VehiclesPage() {
  const queryClient = useQueryClient()
  const [searchParams] = useSearchParams()
  const initialQ = searchParams.get('q') ?? ''
  const [q, setQ] = useState(initialQ)
  const [search, setSearch] = useState(initialQ)
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<VehicleListDto | null>(null)
  const [showDeleted, setShowDeleted] = useState(false)
  const [sort, setSort] = useState<SortState | null>(null)

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['vehicles', search, page, showDeleted, sort],
    queryFn: () => vehiclesApi.search(search || null, page, 25, showDeleted, sort),
  })

  const { data: editVehicle } = useQuery({
    queryKey: ['vehicles', editing?.id],
    queryFn: () => vehiclesApi.get(editing!.id),
    enabled: !!editing,
  })

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    setSearch(q)
    setPage(1)
  }

  function handleSuccess(dto: VehicleDto) {
    setCreating(false)
    setEditing(null)
    queryClient.invalidateQueries({ queryKey: ['vehicles'] })
    queryClient.setQueryData(['vehicles', dto.id], dto)
  }

  async function handleRestore(v: VehicleListDto) {
    try {
      await vehiclesApi.restore(v.id)
      queryClient.invalidateQueries({ queryKey: ['vehicles'] })
    } catch {
      notify('No se pudo restaurar el vehículo')
    }
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Vehículos</h1>
        <Button onClick={() => setCreating(true)} size="sm">Nuevo vehículo</Button>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <form onSubmit={handleSearch} className="flex min-w-[14rem] flex-1 gap-2">
          <input
            type="search"
            value={q}
            onChange={e => setQ(e.target.value)}
            placeholder="Buscar por patente, marca o modelo…"
            className="min-w-0 flex-1 border border-tc-border bg-white px-3 py-1.5 text-sm text-tc-text-secondary outline-none focus:border-tc-border-mid"
          />
          <Button type="submit" variant="secondary" size="sm">Buscar</Button>
        </form>
        <button
          onClick={() => { setShowDeleted(v => !v); setPage(1) }}
          className={`shrink-0 whitespace-nowrap border px-3 py-2 max-md:min-h-11 text-xs font-bold tracking-wider uppercase transition-colors ${showDeleted ? 'border-red-300 bg-red-50 text-red-700' : 'border-tc-border bg-white text-tc-text-secondary hover:bg-tc-surface'}`}
        >
          {showDeleted ? 'Ocultar eliminados' : 'Ver eliminados'}
        </button>
      </div>

      <DataTable<VehicleListDto>
        label="Vehículos"
        columns={COLUMNS}
        sort={sort}
        onSortChange={s => { setSort(s); setPage(1) }}
        rows={data?.items ?? []}
        getRowId={v => v.id}
        rowHref={v => (v.isDeleted ? null : `/app/vehicles/${v.id}`)}
        rowLabel={v => `Ver vehículo ${v.plate}`}
        rowClassName={v => (v.isDeleted ? 'opacity-60' : undefined)}
        actions={v =>
          v.isDeleted ? (
            <RowAction tone="success" onClick={() => handleRestore(v)}>Restaurar</RowAction>
          ) : (
            <RowAction onClick={() => setEditing(v)}>Editar</RowAction>
          )
        }
        renderCard={v => (
          <>
            <p className="font-mono text-sm font-bold tracking-widest text-tc-text">{v.plate}</p>
            <p className="mt-0.5 truncate text-sm text-tc-text-secondary">
              {[v.brand, v.model, v.year].filter(Boolean).join(' ') || '—'}
            </p>
            <p className="truncate text-xs text-tc-text-muted">{v.customerName}</p>
          </>
        )}
        loading={isLoading}
        error={!!error}
        onRetry={() => refetch()}
        emptyMessage={`No hay vehículos${search ? ' para esa búsqueda' : ''}.`}
      />

      {data && <Pagination page={page} pageSize={data.pageSize} total={data.total} noun="vehículos" onPage={setPage} />}

      {creating && <VehicleFormModal onSuccess={handleSuccess} onClose={() => setCreating(false)} />}
      {editing && editVehicle && (
        <VehicleFormModal
          vehicle={editVehicle}
          onSuccess={handleSuccess}
          onClose={() => setEditing(null)}
          onDelete={() => {
            setEditing(null)
            queryClient.invalidateQueries({ queryKey: ['vehicles'] })
          }}
        />
      )}
    </div>
  )
}
