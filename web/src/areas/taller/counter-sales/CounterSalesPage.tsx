import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { counterSalesApi, type CounterSaleDto, type CounterSaleStatus } from '../../../lib/api/counterSales'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { ApiError } from '../../../lib/api/client'
import { CounterSaleFormModal } from './CounterSaleFormModal'

const STATUS_LABELS: Record<CounterSaleStatus, string> = {
  draft: 'Borrador',
  confirmed: 'Confirmada',
  cancelled: 'Cancelada',
}

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

export function CounterSalesPage() {
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<CounterSaleDto | null>(null)
  const [creating, setCreating] = useState(false)

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

  const totalPages = data ? Math.ceil(data.total / data.pageSize) : 1

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-2">
        <h1 className="text-xl font-bold tracking-widest text-tc-text uppercase">Ventas de Mostrador</h1>
        <Button onClick={() => setCreating(true)} size="sm">Nueva venta</Button>
      </div>

      <form onSubmit={handleSearch} className="flex gap-1">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Nº de venta o producto…"
          className="flex-1 border border-tc-border bg-white px-3 py-1.5 text-sm outline-none focus:border-tc-border-mid transition-colors"
        />
        <Button type="submit" variant="secondary" size="sm">Buscar</Button>
      </form>

      {isLoading && (
        <div className="border border-tc-border bg-white">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="h-10 animate-pulse border-b border-tc-border bg-tc-surface last:border-0" />
          ))}
        </div>
      )}

      {!isLoading && data && data.items.length === 0 && (
        <div className="border border-dashed border-tc-border px-4 py-10 text-center text-sm text-tc-text-muted">
          No hay ventas de mostrador registradas.
        </div>
      )}

      {!isLoading && data && data.items.length > 0 && (
        <div className="border border-tc-border bg-white overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-tc-stub-bg border-b border-tc-border">
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Nº</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Fecha</th>
                <th className="px-4 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Total</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Estado</th>
                <th className="px-4 py-2" />
              </tr>
            </thead>
            <tbody>
              {data.items.map(s => (
                <tr key={s.id} className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
                  <td className="px-4 py-2.5 font-mono font-bold text-tc-text tabular-nums">#{s.documentNumber}</td>
                  <td className="px-4 py-2.5 text-tc-text-secondary tabular-nums">
                    {new Date(s.date).toLocaleDateString('es-AR')}
                  </td>
                  <td className="px-4 py-2.5 text-right font-bold text-tc-purple tabular-nums">{fmtARS(s.total)}</td>
                  <td className="px-4 py-2.5">
                    <StatusBadge status={s.status} label={STATUS_LABELS[s.status]} />
                  </td>
                  <td className="px-4 py-2.5 text-right">
                    <button onClick={() => setSelected(s)} className="text-xs text-tc-purple hover:opacity-70">
                      Ver
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {data.total > data.pageSize && (
            <div className="flex items-center gap-1 border-t border-tc-border px-4 py-2">
              <button
                className="border border-tc-border bg-white px-3 py-1 text-xs disabled:opacity-40 hover:bg-tc-surface"
                disabled={page <= 1}
                onClick={() => setPage(p => p - 1)}
              >
                ← Anterior
              </button>
              <span className="border border-tc-border bg-tc-stub-bg px-3 py-1 text-xs tabular-nums">
                {page} / {totalPages}
              </span>
              <button
                className="border border-tc-border bg-white px-3 py-1 text-xs disabled:opacity-40 hover:bg-tc-surface"
                disabled={page * data.pageSize >= data.total}
                onClick={() => setPage(p => p + 1)}
              >
                Siguiente →
              </button>
            </div>
          )}
        </div>
      )}

      {creating && (
        <CounterSaleFormModal
          onSuccess={() => {
            setCreating(false)
            queryClient.invalidateQueries({ queryKey: ['counter-sales'] })
          }}
          onClose={() => setCreating(false)}
        />
      )}

      {selected && detail && (
        <Modal title={`Venta #${detail.documentNumber}`} onClose={() => setSelected(null)} className="max-w-lg">
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-2 border border-tc-border bg-tc-stub-bg p-3">
              <div>
                <p className="text-[10px] font-bold tracking-wider uppercase text-tc-text-muted">Estado</p>
                <StatusBadge status={detail.status} label={STATUS_LABELS[detail.status]} />
              </div>
              <div>
                <p className="text-[10px] font-bold tracking-wider uppercase text-tc-text-muted">Fecha</p>
                <p className="text-sm text-tc-text tabular-nums">{new Date(detail.date).toLocaleDateString('es-AR')}</p>
              </div>
              <div>
                <p className="text-[10px] font-bold tracking-wider uppercase text-tc-text-muted">Total</p>
                <p className="text-sm font-bold text-tc-purple tabular-nums">{fmtARS(detail.total)}</p>
              </div>
            </div>

            {detail.items.length > 0 && (
              <div className="border border-tc-border bg-white overflow-hidden">
                <div className="bg-tc-stub-bg border-b border-tc-border px-3 py-1.5">
                  <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Ítems</span>
                </div>
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-tc-border">
                      <th className="px-3 py-1.5 text-left text-[10px] font-bold tracking-wider uppercase text-tc-text-muted">Descripción</th>
                      <th className="px-3 py-1.5 text-right text-[10px] font-bold tracking-wider uppercase text-tc-text-muted">Cant.</th>
                      <th className="px-3 py-1.5 text-right text-[10px] font-bold tracking-wider uppercase text-tc-text-muted">Total</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.items.map(i => (
                      <tr key={i.id} className="border-b border-tc-border last:border-0">
                        <td className="px-3 py-2 text-tc-text-secondary">{i.description}</td>
                        <td className="px-3 py-2 text-right tabular-nums text-tc-text-secondary">{i.quantity}</td>
                        <td className="px-3 py-2 text-right tabular-nums font-medium text-tc-purple">{fmtARS(i.total)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            <div className="flex justify-end gap-2">
              {detail.status === 'draft' && (
                <>
                  <Button size="sm" onClick={() => handleConfirm(detail)}>Confirmar</Button>
                  <Button size="sm" variant="secondary" onClick={() => handleCancel(detail)}>Cancelar</Button>
                  <Button size="sm" variant="danger" onClick={() => handleDelete(detail)}>Eliminar</Button>
                </>
              )}
              <Button size="sm" variant="secondary" onClick={() => setSelected(null)}>Cerrar</Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  )
}
