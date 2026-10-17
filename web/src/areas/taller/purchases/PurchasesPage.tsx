import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { purchasesApi, type PurchaseDto, type PurchaseStatus } from '../../../lib/api/purchases'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { ApiError } from '../../../lib/api/client'
import { PurchaseFormModal } from './PurchaseFormModal'

const STATUS_LABELS: Record<PurchaseStatus, string> = {
  draft: 'Borrador',
  confirmed: 'Confirmada',
  cancelled: 'Cancelada',
}

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

export function PurchasesPage() {
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<PurchaseDto | null>(null)
  const [creating, setCreating] = useState(false)

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

  const totalPages = data ? Math.ceil(data.total / data.pageSize) : 1

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold tracking-tight text-slate-900">Compras</h1>
        <Button onClick={() => setCreating(true)} size="sm">Nueva compra</Button>
      </div>

      <form onSubmit={handleSearch} className="flex gap-1">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Buscar compras…"
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
        <div className="border border-dashed border-tc-border px-4 py-10 text-center text-sm text-slate-400">
          No hay compras registradas.
        </div>
      )}

      {!isLoading && data && data.items.length > 0 && (
        <div className="border border-tc-border bg-white overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-tc-stub-bg border-b border-tc-border">
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Nº</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Fecha</th>
                <th className="px-4 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Total</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Estado</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">CITI</th>
                <th className="px-4 py-2" />
              </tr>
            </thead>
            <tbody>
              {data.items.map(p => (
                <tr key={p.id} className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
                  <td className="px-4 py-2.5 font-mono font-bold text-slate-800 tabular-nums">#{p.documentNumber}</td>
                  <td className="px-4 py-2.5 text-slate-600 tabular-nums">
                    {new Date(p.invoiceDate).toLocaleDateString('es-AR')}
                  </td>
                  <td className="px-4 py-2.5 text-right font-bold text-tc-purple tabular-nums">{fmtARS(p.total)}</td>
                  <td className="px-4 py-2.5">
                    <StatusBadge status={p.status} label={STATUS_LABELS[p.status]} />
                  </td>
                  <td className="px-4 py-2.5">
                    {p.isExcludedFromCiti ? (
                      <span className="text-[10px] font-bold tracking-wider uppercase text-amber-600">Excluida</span>
                    ) : (
                      <span className="text-[10px] font-bold tracking-wider uppercase text-green-700">Incluida</span>
                    )}
                  </td>
                  <td className="px-4 py-2.5 text-right">
                    <button onClick={() => setSelected(p)} className="text-xs text-tc-purple hover:opacity-70">
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
        <PurchaseFormModal
          onSuccess={(dto) => {
            setCreating(false)
            queryClient.invalidateQueries({ queryKey: ['purchases'] })
            setSelected(dto)
          }}
          onClose={() => setCreating(false)}
        />
      )}

      {selected && detail && (
        <Modal title={`Compra #${detail.documentNumber}`} onClose={() => setSelected(null)} className="max-w-lg">
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-2 border border-tc-border bg-tc-stub-bg p-3">
              <div>
                <p className="text-[10px] font-bold tracking-wider uppercase text-slate-400">Estado</p>
                <StatusBadge status={detail.status} label={STATUS_LABELS[detail.status]} />
              </div>
              <div>
                <p className="text-[10px] font-bold tracking-wider uppercase text-slate-400">Fecha</p>
                <p className="text-sm text-slate-800 tabular-nums">{new Date(detail.invoiceDate).toLocaleDateString('es-AR')}</p>
              </div>
              <div>
                <p className="text-[10px] font-bold tracking-wider uppercase text-slate-400">Total</p>
                <p className="text-sm font-bold text-tc-purple tabular-nums">{fmtARS(detail.total)}</p>
              </div>
            </div>

            {detail.isExcludedFromCiti && (
              <p className="border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
                ⚠ Sin datos fiscales completos — excluida del CITI
              </p>
            )}

            {detail.items.length > 0 && (
              <div className="border border-tc-border bg-white overflow-hidden">
                <div className="bg-tc-stub-bg border-b border-tc-border px-3 py-1.5">
                  <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Ítems</span>
                </div>
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-tc-border">
                      <th className="px-3 py-1.5 text-left text-[10px] font-bold tracking-wider uppercase text-slate-400">Descripción</th>
                      <th className="px-3 py-1.5 text-right text-[10px] font-bold tracking-wider uppercase text-slate-400">Cant.</th>
                      <th className="px-3 py-1.5 text-right text-[10px] font-bold tracking-wider uppercase text-slate-400">Total</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.items.map(i => (
                      <tr key={i.id} className="border-b border-tc-border last:border-0">
                        <td className="px-3 py-2 text-slate-700">{i.description}</td>
                        <td className="px-3 py-2 text-right tabular-nums text-slate-600">{i.quantity}</td>
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
