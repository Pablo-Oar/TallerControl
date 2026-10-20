import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { catalogApi, type CatalogItemDto, type CatalogItemKind, type CatalogItemListDto } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { CatalogFormModal } from './CatalogFormModal'
import { StockAdjustmentModal } from './StockAdjustmentModal'

const KIND_LABELS: Record<CatalogItemKind, string> = {
  service: 'Servicio',
  product: 'Producto',
}

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(n)

export function CatalogPage() {
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [kindFilter, setKindFilter] = useState<CatalogItemKind | ''>('')
  const [activeOnly, setActiveOnly] = useState<boolean>(true)
  const [page, setPage] = useState(1)

  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<CatalogItemListDto | null>(null)
  const [adjusting, setAdjusting] = useState<CatalogItemListDto | null>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: ['catalog', search, kindFilter, activeOnly, page],
    queryFn: () => catalogApi.search(search || null, kindFilter || null, activeOnly ? true : null, page),
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

  function handleAdjustSuccess() {
    setAdjusting(null)
    queryClient.invalidateQueries({ queryKey: ['catalog'] })
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Catálogo</h1>
        <Button onClick={() => setCreating(true)} size="sm">Nuevo ítem</Button>
      </div>

      <form onSubmit={handleSearch} className="flex flex-wrap gap-2">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Buscar por nombre o SKU…"
          className="min-w-0 flex-1 border border-tc-border bg-white px-3 py-1.5 text-sm text-tc-text-secondary outline-none focus:border-tc-border-mid"
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
        <label className="flex items-center gap-1.5 text-sm text-tc-text-secondary">
          <input
            type="checkbox"
            checked={activeOnly}
            onChange={e => { setActiveOnly(e.target.checked); setPage(1) }}
            className="h-4 w-4"
          />
          Solo activos
        </label>
        <Button type="submit" variant="secondary" size="sm">Buscar</Button>
      </form>

      {isLoading && (
        <div className="space-y-1">
          {[0, 1, 2, 3].map(i => (
            <div key={i} className="h-14 animate-pulse bg-slate-50 border border-tc-border" />
          ))}
        </div>
      )}

      {error && (
        <p className="border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          No se pudo cargar el catálogo.
        </p>
      )}

      {data && (
        <>
          <div className="border border-tc-border bg-white overflow-hidden">
            {/* Header */}
            <div className="grid grid-cols-[1fr_5rem_5rem_6rem] md:grid-cols-[1fr_5rem_6rem_5rem_4rem_5rem_6rem] border-b border-tc-border bg-tc-stub-bg">
              {[
                ['Nombre', '', false],
                ['Tipo', '', false],
                ['SKU', '', true],
                ['Precio neto', 'text-right', false],
                ['IVA', 'text-right', true],
                ['Stock', 'text-right', true],
                ['', '', false],
              ].map(([h, cls, hide], i) => (
                <div key={i} className={`px-4 py-2 text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted ${cls} ${hide ? 'hidden md:block' : ''}`}>
                  {h}
                </div>
              ))}
            </div>

            {data.items.length === 0 ? (
              <p className="px-4 py-8 text-center text-sm text-tc-text-muted">
                No hay ítems{search ? ' para esa búsqueda' : ''}.
              </p>
            ) : (
              <ul className="divide-y divide-tc-border">
                {data.items.map(item => (
                  <li
                    key={item.id}
                    className={[
                      'grid grid-cols-[1fr_5rem_5rem_6rem] md:grid-cols-[1fr_5rem_6rem_5rem_4rem_5rem_6rem] items-center hover:bg-tc-surface transition-colors',
                      !item.isActive ? 'opacity-60' : '',
                    ].join(' ')}
                  >
                    <div className="px-4 py-3">
                      <p className="text-sm font-medium text-tc-text truncate">{item.name}</p>
                      {!item.isActive && (
                        <span className="text-[10px] font-bold tracking-wider text-tc-text-muted uppercase">Inactivo</span>
                      )}
                    </div>

                    <div className="px-4 py-3">
                      <span className="text-xs text-tc-text-secondary">{KIND_LABELS[item.kind]}</span>
                    </div>

                    <div className="hidden md:block px-4 py-3">
                      <span className="font-mono text-xs text-tc-text-muted">{item.sku ?? '—'}</span>
                    </div>

                    <div className="px-4 py-3 text-right">
                      <span className="tabular-nums text-sm text-tc-purple">{fmtARS(item.netPrice)}</span>
                    </div>

                    <div className="hidden md:block px-4 py-3 text-right">
                      <span className="text-xs text-tc-text-secondary">{item.vatRate}%</span>
                    </div>

                    <div className="hidden md:block px-4 py-3 text-right">
                      {item.trackStock ? (
                        <span className={['tabular-nums text-sm', item.isLowStock ? 'font-semibold text-amber-600' : 'text-tc-text-secondary'].join(' ')}>
                          {item.stockQty}
                          {item.isLowStock && <span className="ml-0.5 text-[10px]">▼</span>}
                        </span>
                      ) : (
                        <span className="text-slate-300">—</span>
                      )}
                    </div>

                    <div className="flex h-full items-center justify-center gap-2 border-l border-tc-border bg-tc-stub-bg px-2">
                      {item.trackStock && item.isActive && (
                        <button
                          onClick={() => setAdjusting(item)}
                          className="text-[10px] font-bold tracking-wider uppercase text-amber-500 hover:text-amber-700 transition-colors"
                        >
                          Stock
                        </button>
                      )}
                      <button
                        onClick={() => setEditing(item)}
                        className="text-[10px] font-bold tracking-wider uppercase text-tc-navy-text-dim hover:text-tc-purple transition-colors"
                      >
                        Editar
                      </button>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {data.total > data.pageSize && (
            <div className="flex items-center justify-between text-sm text-tc-text-secondary">
              <span className="tabular-nums">{data.total} ítems</span>
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
        <CatalogFormModal onSuccess={handleSuccess} onClose={() => setCreating(false)} />
      )}
      {editing && editItem && (
        <CatalogFormModal item={editItem} onSuccess={handleSuccess} onClose={() => setEditing(null)} />
      )}
      {adjusting && (
        <StockAdjustmentModal item={adjusting} onSuccess={handleAdjustSuccess} onClose={() => setAdjusting(null)} />
      )}
    </div>
  )
}
