import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { catalogApi, type CatalogItemDto, type CatalogItemKind, type CatalogItemListDto } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { DataTable, RowAction, type DataTableColumn, type SortState } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { useCan } from '../../../lib/auth/permissions'
import { CatalogFormModal } from './CatalogFormModal'
import { StockAdjustmentModal } from './StockAdjustmentModal'
import { StockMovementsModal } from './StockMovementsModal'
import { Modal } from '../../../components/ui/Modal'
import { FormTextarea } from '../../../components/ui/FormField'
import { notify } from '../../../components/ui/dialogs'
import { ApiError } from '../../../lib/api/client'

const KIND_LABELS: Record<CatalogItemKind, string> = {
  service: 'Servicio',
  product: 'Producto',
}

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(n)

const COLUMNS: DataTableColumn<CatalogItemListDto>[] = [
  {
    key: 'name',
    header: 'Nombre',
    sortable: true,
    cell: i => (
      <div className="min-w-0">
        <p className="truncate font-medium text-tc-text">{i.name}</p>
        {!i.isActive && <span className="text-[11px] font-bold tracking-wider text-tc-text-muted uppercase">Inactivo</span>}
      </div>
    ),
  },
  { key: 'kind', header: 'Tipo', widthClass: 'w-28', sortable: true, cell: i => <span className="text-xs text-tc-text-secondary">{KIND_LABELS[i.kind]}</span> },
  { key: 'sku', header: 'SKU', widthClass: 'w-32', sortable: true, cell: i => <span className="font-mono text-xs text-tc-text-muted">{i.sku ?? '—'}</span> },
  {
    key: 'price',
    header: 'Precio neto',
    align: 'right',
    widthClass: 'w-32',
    sortable: true,
    cell: i => <span className="tabular-nums text-tc-purple">{fmtARS(i.netPrice)}</span>,
  },
  { key: 'vat', header: 'IVA', align: 'right', widthClass: 'w-20', sortable: true, cell: i => <span className="text-xs text-tc-text-secondary">{i.vatRate}%</span> },
  {
    key: 'stock',
    header: 'Stock',
    align: 'right',
    widthClass: 'w-24',
    sortable: true,
    cell: i =>
      i.trackStock ? (
        <span className={['tabular-nums', i.isLowStock ? 'font-semibold text-amber-600' : 'text-tc-text-secondary'].join(' ')}>
          {i.stockQty}
          {i.isLowStock && <span className="ml-0.5 text-[11px]">▼</span>}
        </span>
      ) : (
        <span className="text-tc-text-muted">—</span>
      ),
  },
]

export function CatalogPage() {
  const can = useCan()
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [kindFilter, setKindFilter] = useState<CatalogItemKind | ''>('')
  const [activeOnly, setActiveOnly] = useState<boolean>(true)
  const [showDeleted, setShowDeleted] = useState(false)
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState<SortState | null>(null)

  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<CatalogItemListDto | null>(null)
  const [adjusting, setAdjusting] = useState<CatalogItemListDto | null>(null)
  const [viewingMovements, setViewingMovements] = useState<CatalogItemListDto | null>(null)
  const [deleting, setDeleting] = useState<CatalogItemListDto | null>(null)
  const [deleteReason, setDeleteReason] = useState('')
  const [busy, setBusy] = useState(false)

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['catalog', search, kindFilter, activeOnly, showDeleted, page, sort],
    queryFn: () => catalogApi.search(search || null, kindFilter || null, !showDeleted && activeOnly ? true : null, page, 25, sort, showDeleted),
  })

  const { data: editItem } = useQuery({
    queryKey: ['catalog', editing?.id],
    queryFn: () => catalogApi.get(editing!.id),
    enabled: !!editing,
  })

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    setSearch(q)
    setPage(1)
  }

  function handleSuccess(dto: CatalogItemDto) {
    setCreating(false)
    setEditing(null)
    queryClient.invalidateQueries({ queryKey: ['catalog'] })
    queryClient.setQueryData(['catalog', dto.id], dto)
  }

  async function confirmDelete() {
    if (!deleting || !deleteReason.trim()) return
    setBusy(true)
    try {
      await catalogApi.delete(deleting.id, deleteReason.trim())
      setDeleting(null)
      setDeleteReason('')
      queryClient.invalidateQueries({ queryKey: ['catalog'] })
    } catch (e) {
      notify(e instanceof ApiError ? e.detail : 'No se pudo eliminar el ítem')
    } finally {
      setBusy(false)
    }
  }

  async function restore(item: CatalogItemListDto) {
    try {
      await catalogApi.restore(item.id)
      queryClient.invalidateQueries({ queryKey: ['catalog'] })
    } catch (e) {
      notify(e instanceof ApiError ? e.detail : 'No se pudo restaurar el ítem')
    }
  }

  function handleAdjustSuccess() {
    setAdjusting(null)
    queryClient.invalidateQueries({ queryKey: ['catalog'] })
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Catálogo</h1>
        <Button onClick={() => setCreating(true)} size="sm">Nuevo ítem</Button>
      </div>

      <form onSubmit={handleSearch} className="flex flex-wrap gap-2">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Buscar por nombre o SKU…"
          className="min-w-0 basis-full border border-tc-border bg-white px-3 py-2 text-sm text-tc-text-secondary outline-none focus:border-tc-border-mid sm:basis-0 sm:flex-1"
        />
        <select
          value={kindFilter}
          onChange={e => { setKindFilter(e.target.value as CatalogItemKind | ''); setPage(1) }}
          className="border border-tc-border bg-white px-3 py-1.5 text-sm text-tc-text-secondary outline-none focus:border-tc-border-mid"
        >
          <option value="">Todos los tipos</option>
          <option value="service">Servicios</option>
          <option value="product">Productos</option>
        </select>
        {!showDeleted && (
          <label className="flex items-center gap-1.5 text-sm text-tc-text-secondary">
            <input
              type="checkbox"
              checked={activeOnly}
              onChange={e => { setActiveOnly(e.target.checked); setPage(1) }}
              className="h-4 w-4"
            />
            Solo activos
          </label>
        )}
        {can('catalog.write') && (
          <label className="flex items-center gap-1.5 text-sm text-tc-text-secondary">
            <input
              type="checkbox"
              checked={showDeleted}
              onChange={e => { setShowDeleted(e.target.checked); setPage(1) }}
              className="h-4 w-4"
            />
            Ver eliminados
          </label>
        )}
        <Button type="submit" variant="secondary" size="sm">Buscar</Button>
      </form>

      <DataTable<CatalogItemListDto>
        label="Catálogo"
        columns={COLUMNS}
        sort={sort}
        onSortChange={s => { setSort(s); setPage(1) }}
        rows={data?.items ?? []}
        getRowId={i => i.id}
        rowClassName={i => (!i.isActive ? 'opacity-60' : undefined)}
        actions={i =>
          showDeleted ? (
            can('catalog.write') && <RowAction onClick={() => restore(i)}>Restaurar</RowAction>
          ) : (
            <>
              {i.trackStock && <RowAction onClick={() => setViewingMovements(i)}>Movimientos</RowAction>}
              {i.trackStock && i.isActive && can('stock.adjust') && (
                <RowAction tone="warn" onClick={() => setAdjusting(i)}>Stock</RowAction>
              )}
              <RowAction onClick={() => setEditing(i)}>Editar</RowAction>
              {can('catalog.write') && (
                <RowAction tone="danger" onClick={() => { setDeleting(i); setDeleteReason('') }}>Eliminar</RowAction>
              )}
            </>
          )
        }
        renderCard={i => (
          <>
            <p className="truncate text-sm font-medium text-tc-text">{i.name}</p>
            {!i.isActive && <span className="text-[11px] font-bold tracking-wider text-tc-text-muted uppercase">Inactivo</span>}
            <p className="mt-0.5 text-xs text-tc-text-secondary">
              {KIND_LABELS[i.kind]} · <span className="tabular-nums text-tc-purple">{fmtARS(i.netPrice)}</span>
              {i.trackStock && (
                <span className={i.isLowStock ? 'font-semibold text-amber-600' : ''}>
                  {' '}· Stock {i.stockQty}{i.isLowStock ? ' ▼' : ''}
                </span>
              )}
            </p>
          </>
        )}
        loading={isLoading}
        error={!!error}
        onRetry={() => refetch()}
        emptyMessage={showDeleted ? 'No hay ítems eliminados.' : `No hay ítems${search ? ' para esa búsqueda' : ''}.`}
      />

      {data && <Pagination page={page} pageSize={data.pageSize} total={data.total} noun="ítems" onPage={setPage} />}

      {creating && <CatalogFormModal onSuccess={handleSuccess} onClose={() => setCreating(false)} />}
      {editing && editItem && (
        <CatalogFormModal item={editItem} onSuccess={handleSuccess} onClose={() => setEditing(null)} />
      )}
      {adjusting && (
        <StockAdjustmentModal item={adjusting} onSuccess={handleAdjustSuccess} onClose={() => setAdjusting(null)} />
      )}
      {viewingMovements && <StockMovementsModal item={viewingMovements} onClose={() => setViewingMovements(null)} />}

      {deleting && (
        <Modal title="Eliminar ítem" onClose={() => setDeleting(null)} className="max-w-sm">
          <div className="space-y-4">
            <p className="text-sm text-tc-text-secondary">
              «{deleting.name}» deja de aparecer en el catálogo y en los selectores. Las órdenes y presupuestos que ya lo usan
              no cambian, y lo podés restaurar desde «Ver eliminados».
            </p>
            <FormTextarea
              label="Motivo"
              requiredMark
              rows={2}
              value={deleteReason}
              onChange={e => setDeleteReason(e.target.value)}
            />
            <div className="flex justify-end gap-2">
              <Button type="button" variant="secondary" size="sm" onClick={() => setDeleting(null)}>Volver</Button>
              <Button variant="danger" size="sm" loading={busy} disabled={!deleteReason.trim()} onClick={confirmDelete}>
                Eliminar
              </Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  )
}
