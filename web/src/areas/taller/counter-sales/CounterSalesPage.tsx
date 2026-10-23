import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { counterSalesApi, type CounterSaleDto, type CounterSaleStatus } from '../../../lib/api/counterSales'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { DataTable, RowAction, sortableWhen, type DataTableColumn } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { ApiError } from '../../../lib/api/client'
import { CounterSaleFormModal } from './CounterSaleFormModal'
import { InvoiceSection } from '../invoicing/InvoiceSection'
import { confirmDialog, notify } from '../../../components/ui/dialogs'
import { ConfirmWithCashModal } from '../../../components/ConfirmWithCashModal'
import { FormTextarea } from '../../../components/ui/FormField'

const STATUS_LABELS: Record<CounterSaleStatus, string> = {
  draft: 'Borrador',
  confirmed: 'Confirmada',
  cancelled: 'Cancelada',
}

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

const COLUMNS: DataTableColumn<CounterSaleDto>[] = [
  {
    key: 'number',
    header: 'Nº',
    widthClass: 'w-24',
    sortable: true,
    sortValue: s => s.documentNumber,
    cell: s => <span className="font-mono font-bold tabular-nums text-tc-text">#{s.documentNumber}</span>,
  },
  {
    key: 'date',
    header: 'Fecha',
    widthClass: 'w-36',
    sortable: true,
    sortValue: s => s.date,
    cell: s => <span className="tabular-nums text-tc-text-secondary">{new Date(s.date).toLocaleDateString('es-AR')}</span>,
  },
  {
    key: 'total',
    header: 'Total',
    align: 'right',
    sortable: true,
    sortValue: s => s.total,
    cell: s => <span className="font-bold tabular-nums text-tc-purple">{fmtARS(s.total)}</span>,
  },
  {
    key: 'status',
    header: 'Estado',
    align: 'center',
    widthClass: 'w-40',
    sortable: true,
    sortValue: s => s.status,
    cell: s => <StatusBadge status={s.status} label={STATUS_LABELS[s.status]} />,
  },
]

export function CounterSalesPage() {
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<CounterSaleDto | null>(null)
  const [confirming, setConfirming] = useState<CounterSaleDto | null>(null)
  const [returning, setReturning] = useState<CounterSaleDto | null>(null)
  const [returnReason, setReturnReason] = useState('')
  const [returnBusy, setReturnBusy] = useState(false)
  const [creating, setCreating] = useState(false)

  const { data, isLoading, error, refetch } = useQuery({
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

  async function confirmSale(s: CounterSaleDto, accountId: string | null) {
    await counterSalesApi.confirm(s.id, { accountId, version: s.version })
    queryClient.invalidateQueries({ queryKey: ['counter-sales'] })
    queryClient.invalidateQueries({ queryKey: ['cash-accounts'] })
    setConfirming(null)
    setSelected(null)
  }

  async function handleReturn() {
    if (!returning || !returnReason.trim()) return
    setReturnBusy(true)
    try {
      await counterSalesApi.cancel(returning.id, returnReason.trim())
      queryClient.invalidateQueries({ queryKey: ['counter-sales'] })
      queryClient.invalidateQueries({ queryKey: ['cash-accounts'] })
      queryClient.invalidateQueries({ queryKey: ['catalog'] })
      queryClient.invalidateQueries({ queryKey: ['invoices'] })
      setReturning(null)
      setSelected(null)
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'No se pudo registrar la devolución')
    } finally {
      setReturnBusy(false)
    }
  }

  async function handleCancel(s: CounterSaleDto) {
    if (!(await confirmDialog('¿Descartar el borrador de la venta?', { danger: true }))) return
    try {
      await counterSalesApi.cancel(s.id)
      queryClient.invalidateQueries({ queryKey: ['counter-sales'] })
      setSelected(null)
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'Error al cancelar la venta')
    }
  }

  async function handleDelete(s: CounterSaleDto) {
    if (!(await confirmDialog('¿Eliminar la venta?', { danger: true }))) return
    try {
      await counterSalesApi.delete(s.id, 'Eliminación manual')
      queryClient.invalidateQueries({ queryKey: ['counter-sales'] })
      setSelected(null)
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'Error al eliminar la venta')
    }
  }


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

      <DataTable<CounterSaleDto>
        label="Ventas de mostrador"
        columns={sortableWhen(COLUMNS, (data?.total ?? 0) <= (data?.pageSize ?? 25))}
        rows={data?.items ?? []}
        getRowId={s => s.id}
        actions={s => <RowAction tone="purple" onClick={() => setSelected(s)}>Ver</RowAction>}
        cardStub={s => <span className="text-xs font-bold tabular-nums text-tc-text-secondary">#{s.documentNumber}</span>}
        renderCard={s => (
          <>
            <p className="text-sm tabular-nums text-tc-text-secondary">{new Date(s.date).toLocaleDateString('es-AR')}</p>
            <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1">
              <StatusBadge status={s.status} label={STATUS_LABELS[s.status]} />
              <span className="text-sm font-bold tabular-nums text-tc-purple">{fmtARS(s.total)}</span>
            </div>
          </>
        )}
        loading={isLoading}
        error={!!error}
        onRetry={() => refetch()}
        emptyMessage="No hay ventas de mostrador registradas."
      />

      {data && <Pagination page={page} pageSize={data.pageSize} total={data.total} noun="ventas" onPage={setPage} />}

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

            {detail.status === 'cancelled' && (
              <p className="border border-tc-border bg-tc-surface px-3 py-2 text-xs text-tc-text-secondary">
                Venta devuelta{detail.cancelReason ? `: ${detail.cancelReason}` : '.'}
              </p>
            )}

            <div>
              <p className="mb-1 text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Facturación</p>
              <InvoiceSection
                sourceType="counter_sale"
                sourceId={detail.id}
                eligible={detail.status === 'confirmed'}
                ineligibleReason="Se puede facturar una vez confirmada la venta."
              />
            </div>

            <div className="flex justify-end gap-2">
              {detail.status === 'confirmed' && (
                <Button size="sm" variant="danger" onClick={() => { setReturning(detail); setReturnReason('') }}>Devolución</Button>
              )}
              {detail.status === 'draft' && (
                <>
                  <Button size="sm" onClick={() => setConfirming(detail)}>Confirmar</Button>
                  <Button size="sm" variant="secondary" onClick={() => handleCancel(detail)}>Descartar</Button>
                  <Button size="sm" variant="danger" onClick={() => handleDelete(detail)}>Eliminar</Button>
                </>
              )}
              <Button size="sm" variant="secondary" onClick={() => setSelected(null)}>Cerrar</Button>
            </div>
          </div>
        </Modal>
      )}

      {returning && (
        <Modal title={`Devolución de la venta #${returning.documentNumber}`} onClose={() => setReturning(null)} className="max-w-sm">
          <div className="space-y-4">
            <p className="text-sm text-tc-text-secondary">
              Los productos vuelven al stock y, si se cobró en una cuenta de Caja, el importe sale de esa cuenta. Si la venta estaba
              facturada, se emite la <b>nota de crédito</b> en ARCA primero: si ARCA la rechaza, la devolución no se registra.
            </p>
            <FormTextarea label="Motivo" requiredMark rows={2} value={returnReason} onChange={e => setReturnReason(e.target.value)} />
            <div className="flex justify-end gap-2">
              <Button type="button" variant="secondary" size="sm" onClick={() => setReturning(null)}>Volver</Button>
              <Button variant="danger" size="sm" loading={returnBusy} disabled={!returnReason.trim()} onClick={handleReturn}>Registrar devolución</Button>
            </div>
          </div>
        </Modal>
      )}

      {confirming && (
        <ConfirmWithCashModal
          title={`Confirmar venta #${confirming.documentNumber}`}
          message="Se descuenta el stock de los productos con control. Si elegís una cuenta, se registra el ingreso en la Caja."
          confirmLabel="Confirmar venta"
          accountLabel="Ingresa a la cuenta"
          onConfirm={accountId => confirmSale(confirming, accountId)}
          onClose={() => setConfirming(null)}
        />
      )}
    </div>
  )
}
