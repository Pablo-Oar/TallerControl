import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { counterSalesApi, type CounterSaleDto, type CounterSaleStatus } from '../../../lib/api/counterSales'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../lib/api/client'

const STATUS_LABELS: Record<CounterSaleStatus, string> = {
  draft: 'Borrador',
  confirmed: 'Confirmada',
  cancelled: 'Cancelada',
}

const STATUS_COLORS: Record<CounterSaleStatus, string> = {
  draft: 'bg-yellow-100 text-yellow-700',
  confirmed: 'bg-green-100 text-green-700',
  cancelled: 'bg-slate-100 text-slate-500',
}

function formatMoney(n: number) {
  return n.toLocaleString('es-AR', { style: 'currency', currency: 'ARS' })
}

export function CounterSalesPage() {
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<CounterSaleDto | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['counter-sales', search, page],
    queryFn: () => counterSalesApi.search(search || null, page),
  })

  const { data: detail } = useQuery({
    queryKey: ['counter-sales', selected?.id],
    queryFn: () => counterSalesApi.get(selected!.id),
    enabled: !!selected,
  })

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    setSearch(q)
    setPage(1)
  }

  async function handleConfirm(s: CounterSaleDto) {
    if (!confirm('¿Confirmar la venta? Se descontará el stock.')) return
    try {
      await counterSalesApi.confirm(s.id, { accountId: null, version: s.version })
      queryClient.invalidateQueries({ queryKey: ['counter-sales'] })
      setSelected(null)
    } catch (err) {
      alert(err instanceof ApiError ? err.detail : 'Error al confirmar la venta')
    }
  }

  async function handleCancel(s: CounterSaleDto) {
    if (!confirm('¿Cancelar la venta?')) return
    try {
      await counterSalesApi.cancel(s.id)
      queryClient.invalidateQueries({ queryKey: ['counter-sales'] })
      setSelected(null)
    } catch (err) {
      alert(err instanceof ApiError ? err.detail : 'Error al cancelar la venta')
    }
  }

  async function handleDelete(s: CounterSaleDto) {
    if (!confirm('¿Eliminar la venta?')) return
    try {
      await counterSalesApi.delete(s.id, 'Eliminación manual')
      queryClient.invalidateQueries({ queryKey: ['counter-sales'] })
      setSelected(null)
    } catch (err) {
      alert(err instanceof ApiError ? err.detail : 'Error al eliminar la venta')
    }
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Ventas de Mostrador</h1>
      </div>

      <form onSubmit={handleSearch} className="mb-4 flex gap-2">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Buscar ventas…"
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
        <p className="py-8 text-center text-sm text-slate-500">No hay ventas de mostrador registradas.</p>
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
                <th className="pb-2" />
              </tr>
            </thead>
            <tbody>
              {data.items.map(s => (
                <tr key={s.id} className="border-b border-slate-100">
                  <td className="py-2 pr-4 font-medium">#{s.documentNumber}</td>
                  <td className="py-2 pr-4 text-slate-600">
                    {new Date(s.date).toLocaleDateString('es-AR')}
                  </td>
                  <td className="py-2 pr-4 font-medium">{formatMoney(s.total)}</td>
                  <td className="py-2 pr-4">
                    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[s.status]}`}>
                      {STATUS_LABELS[s.status]}
                    </span>
                  </td>
                  <td className="py-2 text-right">
                    <button onClick={() => setSelected(s)} className="text-blue-600 hover:underline">
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
              <h2 className="text-lg font-semibold">Venta #{detail.documentNumber}</h2>
              <button onClick={() => setSelected(null)} className="text-slate-400 hover:text-slate-700">✕</button>
            </div>

            <div className="mb-4 space-y-1 text-sm">
              <p>
                <span className="font-medium">Estado:</span>{' '}
                <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[detail.status]}`}>
                  {STATUS_LABELS[detail.status]}
                </span>
              </p>
              <p><span className="font-medium">Fecha:</span> {new Date(detail.date).toLocaleDateString('es-AR')}</p>
              <p><span className="font-medium">Total:</span> {formatMoney(detail.total)}</p>
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
