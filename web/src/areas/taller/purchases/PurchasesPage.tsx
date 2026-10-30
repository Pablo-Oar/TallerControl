import { formatDateOnly } from '../../../lib/dates'
import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { purchasesApi, type PurchaseDto, type PurchaseStatus } from '../../../lib/api/purchases'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { DataTable, RowAction, sortableWhen, type DataTableColumn } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { ApiError } from '../../../lib/api/client'
import { PurchaseFormModal } from './PurchaseFormModal'
import { confirmDialog, notify } from '../../../components/ui/dialogs'
import { FormTextarea } from '../../../components/ui/FormField'
import { ConfirmWithCashModal } from '../../../components/ConfirmWithCashModal'
import { useCreateFromQuery } from '../../../lib/useCreateFromQuery'

const STATUS_LABELS: Record<PurchaseStatus, string> = {
  draft: 'Borrador',
  confirmed: 'Confirmada',
  cancelled: 'Cancelada',
}

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

const COLUMNS: DataTableColumn<PurchaseDto>[] = [
  {
    key: 'number',
    header: 'Nº',
    sortLabel: 'Número',
    widthClass: 'w-24',
    sortable: true,
    sortValue: p => p.documentNumber,
    cell: p => <span className="font-mono font-bold tabular-nums text-tc-text">#{p.documentNumber}</span>,
  },
  {
    key: 'date',
    header: 'Fecha',
    widthClass: 'w-36',
    sortable: true,
    sortValue: p => p.invoiceDate,
    cell: p => <span className="tabular-nums text-tc-text-secondary">{formatDateOnly(p.invoiceDate)}</span>,
  },
  {
    key: 'total',
    header: 'Total',
    align: 'right',
    sortable: true,
    sortValue: p => p.total,
    cell: p => <span className="font-bold tabular-nums text-tc-purple">{fmtARS(p.total)}</span>,
  },
  {
    key: 'status',
    header: 'Estado',
    align: 'center',
    widthClass: 'w-40',
    sortable: true,
    sortValue: p => p.status,
    cell: p => <StatusBadge status={p.status} label={STATUS_LABELS[p.status]} />,
  },
  {
    key: 'citi',
    header: 'CITI',
    widthClass: 'w-28',
    cell: p =>
      p.isExcludedFromCiti ? (
        <span className="text-[11px] font-bold tracking-wider uppercase text-amber-600">Excluida</span>
      ) : (
        <span className="text-[11px] font-bold tracking-wider uppercase text-green-700">Incluida</span>
      ),
  },
]

export function PurchasesPage() {
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<PurchaseDto | null>(null)
  const [confirming, setConfirming] = useState<PurchaseDto | null>(null)
  const [creating, setCreating] = useCreateFromQuery()
  const [editing, setEditing] = useState<PurchaseDto | null>(null)
  const [voiding, setVoiding] = useState<PurchaseDto | null>(null)
  const [voidReason, setVoidReason] = useState('')
  const [voidBusy, setVoidBusy] = useState(false)

  const { data, isLoading, error, refetch } = useQuery({
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

  async function confirmPurchase(p: PurchaseDto, accountId: string | null) {
    await purchasesApi.confirm(p.id, { accountId, version: p.version })
    queryClient.invalidateQueries({ queryKey: ['purchases'] })
    queryClient.invalidateQueries({ queryKey: ['cash-accounts'] })
    setConfirming(null)
    setSelected(null)
  }

  async function handleVoid() {
    if (!voiding || !voidReason.trim()) return
    setVoidBusy(true)
    try {
      await purchasesApi.cancel(voiding.id, voidReason.trim())
      queryClient.invalidateQueries({ queryKey: ['purchases'] })
      queryClient.invalidateQueries({ queryKey: ['cash-accounts'] })
      queryClient.invalidateQueries({ queryKey: ['catalog'] })
      setVoiding(null)
      setSelected(null)
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'No se pudo anular la compra')
    } finally {
      setVoidBusy(false)
    }
  }

  async function handleCancel(p: PurchaseDto) {
    if (!(await confirmDialog('¿Descartar el borrador de la compra?', { danger: true }))) return
    try {
      await purchasesApi.cancel(p.id)
      queryClient.invalidateQueries({ queryKey: ['purchases'] })
      setSelected(null)
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'Error al cancelar la compra')
    }
  }

  async function handleDelete(p: PurchaseDto) {
    if (!(await confirmDialog('¿Eliminar la compra?', { danger: true }))) return
    try {
      await purchasesApi.delete(p.id, 'Eliminación manual')
      queryClient.invalidateQueries({ queryKey: ['purchases'] })
      setSelected(null)
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'Error al eliminar la compra')
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Compras</h1>
        <Button onClick={() => setCreating(true)} size="sm">Nueva compra</Button>
      </div>

      <form onSubmit={handleSearch} className="flex flex-wrap items-center gap-2">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Buscar compras…"
          className="min-w-0 flex-1 basis-48 border border-tc-border bg-white px-3 py-1.5 text-sm outline-none focus:border-tc-border-mid transition-colors"
        />
        <Button type="submit" variant="secondary" size="sm" className="shrink-0">Buscar</Button>
      </form>

      <DataTable<PurchaseDto>
        label="Compras"
        columns={sortableWhen(COLUMNS, (data?.total ?? 0) <= (data?.pageSize ?? 10))}
        rows={data?.items ?? []}
        getRowId={p => p.id}
        actions={p => <RowAction tone="purple" onClick={() => setSelected(p)}>Ver</RowAction>}
        cardStub={p => <span className="text-xs font-bold tabular-nums text-tc-text-secondary">#{p.documentNumber}</span>}
        renderCard={p => (
          <>
            <p className="text-sm tabular-nums text-tc-text-secondary">{formatDateOnly(p.invoiceDate)}</p>
            <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1">
              <StatusBadge status={p.status} label={STATUS_LABELS[p.status]} />
              <span className="text-sm font-bold tabular-nums text-tc-purple">{fmtARS(p.total)}</span>
            </div>
          </>
        )}
        loading={isLoading}
        error={!!error}
        onRetry={() => refetch()}
        emptyMessage="No hay compras registradas."
      />

      {data && <Pagination page={page} pageSize={data.pageSize} total={data.total} noun="compras" onPage={setPage} />}

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

      {editing && (
        <PurchaseFormModal
          purchase={editing}
          onSuccess={dto => {
            setEditing(null)
            queryClient.invalidateQueries({ queryKey: ['purchases'] })
            setSelected(dto)
          }}
          onClose={() => setEditing(null)}
        />
      )}

      {voiding && (
        <Modal title={`Anular la compra #${voiding.documentNumber}`} onClose={() => setVoiding(null)} className="max-w-sm">
          <div className="space-y-4">
            <p className="text-sm text-tc-text-secondary">
              Se descuenta el stock que esta compra sumó y, si se pagó desde una cuenta de Caja, el importe vuelve a ingresar a esa cuenta.
              La compra queda anulada y sale de los reportes y del CITI.
            </p>
            <FormTextarea label="Motivo" requiredMark rows={2} value={voidReason} onChange={e => setVoidReason(e.target.value)} />
            <div className="flex justify-end gap-2">
              <Button type="button" variant="secondary" size="sm" onClick={() => setVoiding(null)}>Volver</Button>
              <Button variant="danger" size="sm" loading={voidBusy} disabled={!voidReason.trim()} onClick={handleVoid}>Anular compra</Button>
            </div>
          </div>
        </Modal>
      )}

      {selected && detail && (
        <Modal title={`Compra #${detail.documentNumber}`} onClose={() => setSelected(null)} className="max-w-lg">
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-2 border border-tc-border bg-tc-stub-bg p-3">
              <div>
                <p className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Estado</p>
                <StatusBadge status={detail.status} label={STATUS_LABELS[detail.status]} />
              </div>
              <div>
                <p className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Fecha</p>
                <p className="text-sm text-tc-text tabular-nums">{formatDateOnly(detail.invoiceDate)}</p>
              </div>
              <div>
                <p className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Total</p>
                <p className="text-sm font-bold text-tc-purple tabular-nums">{fmtARS(detail.total)}</p>
              </div>
            </div>

            {detail.status === 'cancelled' && (
              <p className="border border-tc-border bg-tc-surface px-3 py-2 text-xs text-tc-text-secondary">
                Compra anulada{detail.cancelReason ? `: ${detail.cancelReason}` : '.'}
              </p>
            )}

            {detail.status !== 'cancelled' && detail.isExcludedFromCiti && (
              <p className="border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
                ⚠ Sin datos fiscales completos — excluida del CITI
              </p>
            )}

            {detail.items.length > 0 && (
              <div className="border border-tc-border bg-white overflow-hidden">
                <div className="bg-tc-stub-bg border-b border-tc-border px-3 py-1.5">
                  <span className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Ítems</span>
                </div>
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-tc-border">
                      <th className="px-3 py-1.5 text-left text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Descripción</th>
                      <th className="px-3 py-1.5 text-right text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Cant.</th>
                      <th className="px-3 py-1.5 text-right text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Total</th>
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
              {detail.status === 'confirmed' && (
                <Button size="sm" variant="danger" onClick={() => { setVoiding(detail); setVoidReason('') }}>Anular compra</Button>
              )}
              {detail.status === 'draft' && (
                <>
                  <Button size="sm" onClick={() => setConfirming(detail)}>Confirmar</Button>
                  <Button size="sm" variant="secondary" onClick={() => setEditing(detail)}>Editar</Button>
                  <Button size="sm" variant="secondary" onClick={() => handleCancel(detail)}>Descartar</Button>
                  <Button size="sm" variant="danger" onClick={() => handleDelete(detail)}>Eliminar</Button>
                </>
              )}
              <Button size="sm" variant="secondary" onClick={() => setSelected(null)}>Cerrar</Button>
            </div>
          </div>
        </Modal>
      )}

      {confirming && (
        <ConfirmWithCashModal
          title={`Confirmar compra #${confirming.documentNumber}`}
          message="Se suma el stock de los productos con control. Si elegís una cuenta, se registra el egreso en la Caja."
          confirmLabel="Confirmar compra"
          accountLabel="Sale de la cuenta"
          onConfirm={accountId => confirmPurchase(confirming, accountId)}
          onClose={() => setConfirming(null)}
        />
      )}
    </div>
  )
}
