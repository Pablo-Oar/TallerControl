import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { purchasesApi, type PurchaseDto, type PurchaseStatus } from '../../../lib/api/purchases'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../lib/api/client'

const STATUS_LABELS: Record<PurchaseStatus, string> = {
  draft: 'Borrador',
  confirmed: 'Confirmada',
  cancelled: 'Cancelada',
}

const STATUS_COLORS: Record<PurchaseStatus, string> = {
  draft: 'bg-yellow-100 text-yellow-700',
  confirmed: 'bg-green-100 text-green-700',
  cancelled: 'bg-slate-100 text-slate-500',
}

function formatMoney(n: number) {
  return n.toLocaleString('es-AR', { style: 'currency', currency: 'ARS' })
}

export function PurchasesPage() {
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<PurchaseDto | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['purchases', search, page],
    queryFn: () => purchasesApi.search(search || null, page),
  })

  const { data: detail } = useQuery({
    queryKey: ['purchases', selected?.id],
    queryFn: () => purchasesApi.get(selected!.id),
    enabled: !!selected,
  })

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    setSearch(q)
    setPage(1)
  }

  async function handleConfirm(p: PurchaseDto) {
    if (!confirm('¿Confirmar la compra? Se actualizará el stock.')) return
    try {
      await purchasesApi.confirm(p.id, { accountId: null, version: p.version })
      queryClient.invalidateQueries({ queryKey: ['purchases'] })
      setSelected(null)
    } catch (err) {
      alert(err instanceof ApiError ? err.detail : 'Error al confirmar la compra')
    }
  }

  async function handleCancel(p: PurchaseDto) {
    if (!confirm('¿Cancelar la compra?')) return
    try {
      await purchasesApi.cancel(p.id)
      queryClient.invalidateQueries({ queryKey: ['purchases'] })
      setSelected(null)
    } catch (err) {
      alert(err instanceof ApiError ? err.detail : 'Error al cancelar la compra')
    }
  }

  async function handleDelete(p: PurchaseDto) {
    if (!confirm('¿Eliminar la compra?')) return
    try {
      await purchasesApi.delete(p.id, 'Eliminación manual')
      queryClient.invalidateQueries({ queryKey: ['purchases'] })
      setSelected(null)
    } catch (err) {
      alert(err instanceof ApiError ? err.detail : 'Error al eliminar la compra')
    }
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Compras</h1>
      </div>

      <form onSubmit={handleSearch} className="mb-4 flex gap-2">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Buscar compras…"
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
        />
        <Button type="submit" variant="secondary">Buscar</Button>
      </form>

      {isLoading && (
        <div className="flex justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
        </div>
      )}

      {!isLoading && data && data.items.length === 0 && (
        <p className="py-8 text-center text-sm text-slate-500">No hay compras registradas.</p>
      )}

      {!isLoading && data && data.items.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200 text-left text-xs font-medium uppercase text-slate-500">
                <th className="pb-2 pr-4">Nº</th>
                <th className="pb-2 pr-4">Fecha</th>
                <th className="pb-2 pr-4">Total</th>
                <th className="pb-2 pr-4">Estado</th>
                <th className="pb-2 pr-4">CITI</th>
                <th className="pb-2" />
              </tr>
            </thead>
            <tbody>
              {data.items.map(p => (
                <tr key={p.id} className="border-b border-slate-100">
                  <td className="py-2 pr-4 font-medium">#{p.documentNumber}</td>
                  <td className="py-2 pr-4 text-slate-600">
                    {new Date(p.invoiceDate).toLocaleDateString('es-AR')}
                  </td>
                  <td className="py-2 pr-4 font-medium">{formatMoney(p.total)}</td>
                  <td className="py-2 pr-4">
                    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[p.status]}`}>
                      {STATUS_LABELS[p.status]}
                    </span>
                  </td>
                  <td className="py-2 pr-4">
                    {p.isExcludedFromCiti ? (
                      <span className="text-xs text-amber-600">Excluida</span>
                    ) : (
                      <span className="text-xs text-green-600">Incluida</span>
                    )}
                  </td>
                  <td className="py-2 text-right">
                    <button
                      onClick={() => setSelected(p)}
                      className="text-blue-600 hover:underline"
                    >
                      Ver
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {data.total > data.pageSize && (
            <div className="mt-4 flex justify-center gap-2">
              <Button variant="secondary" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>
                Anterior
              </Button>
              <span className="flex items-center text-sm text-slate-500">
                {page} / {Math.ceil(data.total / data.pageSize)}
              </span>
              <Button variant="secondary" disabled={page * data.pageSize >= data.total} onClick={() => setPage(p => p + 1)}>
                Siguiente
              </Button>
            </div>
          )}
        </div>
      )}

      {/* Panel de detalle */}
      {selected && detail && (
        <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/40">
          <div className="w-full max-w-lg rounded-xl bg-white p-6 shadow-xl">
            <div className="mb-4 flex items-center justify-between">
              <h2 className="text-lg font-semibold">Compra #{detail.documentNumber}</h2>
              <button onClick={() => setSelected(null)} className="text-slate-400 hover:text-slate-700">✕</button>
            </div>

            <div className="mb-4 space-y-1 text-sm">
              <p>
                <span className="font-medium">Estado:</span>{' '}
                <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[detail.status]}`}>
                  {STATUS_LABELS[detail.status]}
                </span>
              </p>
              <p><span className="font-medium">Fecha:</span> {new Date(detail.invoiceDate).toLocaleDateString('es-AR')}</p>
              <p><span className="font-medium">Total:</span> {formatMoney(detail.total)}</p>
              {detail.isExcludedFromCiti && (
                <p className="text-amber-600 text-xs">⚠ Sin datos fiscales completos — excluida del CITI</p>
              )}
            </div>

            {detail.items.length > 0 && (
              <table className="mb-4 w-full text-sm">
                <thead>
                  <tr className="border-b text-xs text-slate-500">
                    <th className="pb-1 text-left">Descripción</th>
                    <th className="pb-1 text-right">Cant.</th>
                    <th className="pb-1 text-right">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.items.map(i => (
                    <tr key={i.id} className="border-b border-slate-50">
                      <td className="py-1">{i.description}</td>
                      <td className="py-1 text-right">{i.quantity}</td>
                      <td className="py-1 text-right">{formatMoney(i.total)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            <div className="flex justify-end gap-2">
              {detail.status === 'draft' && (
                <>
                  <Button onClick={() => handleConfirm(detail)}>Confirmar</Button>
                  <Button variant="secondary" onClick={() => handleCancel(detail)}>Cancelar</Button>
                  <Button variant="secondary" onClick={() => handleDelete(detail)}>Eliminar</Button>
                </>
              )}
              <Button variant="secondary" onClick={() => setSelected(null)}>Cerrar</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
