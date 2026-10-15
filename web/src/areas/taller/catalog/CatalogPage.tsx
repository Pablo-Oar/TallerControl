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
    queryFn: () =>
      catalogApi.search(
        search || null,
        kindFilter || null,
        activeOnly ? true : null,
        page,
      ),
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
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Catálogo</h1>
        <Button onClick={() => setCreating(true)}>Nuevo ítem</Button>
      </div>

      <form onSubmit={handleSearch} className="mb-4 flex flex-wrap gap-2">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Buscar por nombre o SKU…"
          className="min-w-0 flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
        />
        <select
          value={kindFilter}
          onChange={e => { setKindFilter(e.target.value as CatalogItemKind | ''); setPage(1) }}
          className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option value="">Todos los tipos</option>
          <option value="service">Servicios</option>
          <option value="product">Productos</option>
        </select>
        <label className="flex items-center gap-1.5 text-sm text-slate-600">
          <input
            type="checkbox"
            checked={activeOnly}
            onChange={e => { setActiveOnly(e.target.checked); setPage(1) }}
            className="h-4 w-4 rounded"
          />
          Solo activos
        </label>
        <Button type="submit" variant="secondary">Buscar</Button>
      </form>

      {isLoading && (
        <div className="flex justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
        </div>
      )}

      {error && (
        <p className="text-sm text-red-600">No se pudo cargar el catálogo.</p>
      )}

      {data && (
        <>
          <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">Nombre</th>
                  <th className="px-4 py-3">Tipo</th>
                  <th className="px-4 py-3">SKU</th>
                  <th className="px-4 py-3 text-right">Precio neto</th>
                  <th className="px-4 py-3 text-right">IVA</th>
                  <th className="px-4 py-3 text-right">Stock</th>
                  <th className="px-4 py-3 text-center">Estado</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={8} className="px-4 py-8 text-center text-slate-400">
                      No hay ítems{search ? ' para esa búsqueda' : ''}.
                    </td>
                  </tr>
                )}
                {data.items.map((item) => (
                  <tr key={item.id} className="hover:bg-slate-50">
                    <td className="px-4 py-3 font-medium">{item.name}</td>
                    <td className="px-4 py-3 text-slate-500">{KIND_LABELS[item.kind]}</td>
                    <td className="px-4 py-3 font-mono text-xs text-slate-400">{item.sku ?? '—'}</td>
                    <td className="px-4 py-3 text-right tabular-nums">${item.netPrice.toFixed(2)}</td>
                    <td className="px-4 py-3 text-right text-slate-500">{item.vatRate}%</td>
                    <td className="px-4 py-3 text-right tabular-nums">
                      {item.trackStock ? (
                        <span className={item.isLowStock ? 'font-medium text-amber-600' : ''}>
                          {item.stockQty}
                          {item.isLowStock && (
                            <span className="ml-1 text-xs text-amber-500">▼</span>
                          )}
                        </span>
                      ) : (
                        <span className="text-slate-300">—</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-center">
                      {item.isActive ? (
                        <span className="rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-700">
                          Activo
                        </span>
                      ) : (
                        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-500">
                          Inactivo
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center justify-end gap-3">
                        {item.trackStock && item.isActive && (
                          <button
                            onClick={() => setAdjusting(item)}
                            className="text-xs text-amber-600 hover:underline"
                          >
                            Stock
                          </button>
                        )}
                        <button
                          onClick={() => setEditing(item)}
                          className="cursor-pointer text-xs text-blue-600 hover:underline"
                        >
                          Editar
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {data.total > data.pageSize && (
            <div className="mt-4 flex items-center justify-between text-sm text-slate-500">
              <span>{data.total} ítems</span>
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
        <CatalogFormModal
          onSuccess={handleSuccess}
          onClose={() => setCreating(false)}
        />
      )}

      {editing && editItem && (
        <CatalogFormModal
          item={editItem}
          onSuccess={handleSuccess}
          onClose={() => setEditing(null)}
        />
      )}

      {adjusting && (
        <StockAdjustmentModal
          item={adjusting}
          onSuccess={handleAdjustSuccess}
          onClose={() => setAdjusting(null)}
        />
      )}
    </div>
  )
}
