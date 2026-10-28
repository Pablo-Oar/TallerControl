import { RowMenu } from '../../../components/ui/RowMenu'
import { formatDate } from '../../../lib/dates'
import { confirmDialog } from '../../../components/ui/dialogs'
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  searchQrCards,
  generateBatch,
  assignByCode,
  unassignQrCard,
  markLost,
  revokeQrCard,
  getQrCardPrintUrl,
  type QrCardListDto,
  type QrCardStatus,
} from '../../../lib/api/qrCards'
import { Button } from '../../../components/ui/Button'
import { FormField, FormTextarea } from '../../../components/ui/FormField'
import { VehiclePicker } from '../../../components/ui/EntityPickers'
import { Modal } from '../../../components/ui/Modal'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { DataTable, RowAction, RowActionLink, type DataTableColumn } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { QrScannerModal } from './QrScannerModal'
import { QrActivityModal } from './QrActivityModal'
import { inputCls } from '../../../lib/styles'
import { useCan } from '../../../lib/auth/permissions'

const STATUS_LABELS: Record<QrCardStatus, string> = {
  free: 'Libre',
  assigned: 'Asignada',
  lost: 'Perdida',
  revoked: 'Revocada',
}

const COLUMNS: DataTableColumn<QrCardListDto>[] = [
  {
    key: 'code',
    header: 'Código',
    widthClass: 'w-48',
    cell: c => <span className="font-mono text-sm font-bold tracking-widest text-tc-text">{c.code}</span>,
  },
  {
    key: 'status',
    header: 'Estado',
    align: 'center',
    widthClass: 'w-40',
    cell: c => <StatusBadge status={c.status} label={STATUS_LABELS[c.status]} />,
  },
  {
    key: 'plate',
    header: 'Vehículo',
    cell: c => (
      <span className="font-mono tracking-widest text-tc-text-secondary">
        {c.vehiclePlate ?? <span className="font-sans tracking-normal text-tc-text-muted">—</span>}
      </span>
    ),
  },
  {
    key: 'assigned',
    header: 'Asignada',
    widthClass: 'w-36',
    cell: c =>
      c.assignedAt ? (
        <span className="tabular-nums text-tc-text-secondary">{formatDate(c.assignedAt)}</span>
      ) : (
        <span className="text-tc-text-muted">—</span>
      ),
  },
]

export function QrCardsPage() {
  const qc = useQueryClient()
  const can = useCan()
  const [statusFilter, setStatusFilter] = useState<QrCardStatus | ''>('')
  const [q, setQ] = useState('')
  const [page, setPage] = useState(1)
  const [showBatchModal, setShowBatchModal] = useState(false)
  const [showAssignByCodeModal, setShowAssignByCodeModal] = useState(false)
  const [showScannerModal, setShowScannerModal] = useState(false)
  const [activityCard, setActivityCard] = useState<{ id: string; code: string } | null>(null)
  const [revokeCardId, setRevokeCardId] = useState<string | null>(null)
  const [revokeReason, setRevokeReason] = useState('')
  const [batchQty, setBatchQty] = useState(10)
  const [assignCode, setAssignCode] = useState('')
  const [assignVehicleId, setAssignVehicleId] = useState('')
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['qr-cards', statusFilter, q, page],
    queryFn: () =>
      searchQrCards({
        status: statusFilter || undefined,
        q: q || undefined,
        page,
        pageSize: 10,
      }),
  })

  const invalidate = () => qc.invalidateQueries({ queryKey: ['qr-cards'] })

  const batchMut = useMutation({
    mutationFn: () => generateBatch(batchQty),
    onSuccess: () => { invalidate(); setShowBatchModal(false) },
    onError: (e: Error) => setError(e.message),
  })

  const assignByCodeMut = useMutation({
    mutationFn: () => assignByCode(assignCode.toUpperCase(), assignVehicleId),
    onSuccess: () => {
      invalidate()
      setShowAssignByCodeModal(false)
      setAssignCode('')
      setAssignVehicleId('')
    },
    onError: (e: Error) => setError(e.message),
  })

  const unassignMut = useMutation({
    mutationFn: (id: string) => unassignQrCard(id),
    onSuccess: invalidate,
    onError: (e: Error) => setError(e.message),
  })

  const markLostMut = useMutation({
    mutationFn: ({ id, lost }: { id: string; lost: boolean }) => markLost(id, lost),
    onSuccess: invalidate,
    onError: (e: Error) => setError(e.message),
  })

  const revokeMut = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => revokeQrCard(id, reason),
    onSuccess: () => {
      invalidate()
      setRevokeCardId(null)
      setRevokeReason('')
    },
    onError: (e: Error) => setError(e.message),
  })


  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Tarjetas QR</h1>
        <Button size="sm" onClick={() => setShowBatchModal(true)} className="md:order-last">Generar lote</Button>
      </div>

      {/* Herramientas: una sola fila de tres en el celular, a la derecha del título en escritorio */}
      <div className="grid grid-cols-3 gap-2 md:flex md:flex-wrap">
        <Button variant="secondary" size="sm" onClick={() => setShowScannerModal(true)}>
          Escanear
        </Button>
        <Button variant="secondary" size="sm" onClick={() => setShowAssignByCodeModal(true)}>
          <span className="md:hidden">Asignar</span>
          <span className="hidden md:inline">Asignar por código</span>
        </Button>
        <a
          href="/api/v1/qr/cards/print-sheet"
          target="_blank"
          rel="noreferrer"
          className="inline-flex items-center justify-center border border-tc-border bg-white px-3 py-1.5 text-xs font-bold tracking-wider text-tc-text-secondary transition-colors hover:bg-tc-surface max-md:min-h-11"
        >
          <span className="md:hidden">Imprimir</span>
          <span className="hidden md:inline">Imprimir libres</span>
        </a>
      </div>

      {error && (
        <div className="border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700 flex items-center justify-between">
          <span>{error}</span>
          <button className="text-xs underline" onClick={() => setError(null)}>Cerrar</button>
        </div>
      )}

      <div className="flex gap-2">
        <input
          className={`${inputCls} min-w-0 flex-1 md:max-w-xs md:flex-none`}
          placeholder="Buscar código…"
          value={q}
          onChange={(e) => { setQ(e.target.value); setPage(1) }}
        />
        <select
          className={`${inputCls} w-36 shrink-0 md:w-auto`}
          value={statusFilter}
          onChange={(e) => { setStatusFilter(e.target.value as QrCardStatus | ''); setPage(1) }}
        >
          <option value="">Estados</option>
          <option value="free">Libre</option>
          <option value="assigned">Asignada</option>
          <option value="lost">Perdida</option>
          <option value="revoked">Revocada</option>
        </select>
      </div>

      <DataTable<QrCardListDto>
        label="Tarjetas QR"
        columns={COLUMNS}
        rows={data?.items ?? []}
        getRowId={c => c.id}
        actions={c => (
          <>
            <RowAction onClick={() => setActivityCard({ id: c.id, code: c.code })}>Actividad</RowAction>
            {c.status === 'assigned' && <RowActionLink tone="purple" href={getQrCardPrintUrl(c.id)} target="_blank" rel="noreferrer">PDF</RowActionLink>}
            {(c.status === 'assigned' || c.status === 'lost' || can('qr.revoke')) && (
              <RowMenu
                items={[
                  ...(c.status === 'assigned'
                    ? [{
                        label: 'Marcar perdida',
                        tone: 'warn' as const,
                        onSelect: async () => {
                          if (await confirmDialog('¿Marcar esta tarjeta como perdida? Deja de servir para ver el vehículo hasta que la desmarques.', { confirmLabel: 'Marcar perdida', danger: true })) markLostMut.mutate({ id: c.id, lost: true })
                        },
                      }]
                    : []),
                  ...(c.status === 'lost' ? [{ label: 'Desmarcar perdida', onSelect: () => markLostMut.mutate({ id: c.id, lost: false }) }] : []),
                  ...(c.status === 'assigned' || c.status === 'lost'
                    ? [{
                        label: 'Desasignar',
                        onSelect: async () => {
                          if (c.status === 'lost' || (await confirmDialog('¿Desasignar esta tarjeta del vehículo? Queda libre para asignarla a otro.', { confirmLabel: 'Desasignar' }))) unassignMut.mutate(c.id)
                        },
                      }]
                    : []),
                  ...(can('qr.revoke') ? [{ label: 'Revocar', tone: 'danger' as const, onSelect: () => setRevokeCardId(c.id) }] : []),
                ]}
              />
            )}
          </>
        )}
        cardActionsClassName="-mx-3 -mb-3 mt-3 flex divide-x divide-tc-border border-t border-tc-border bg-tc-stub-bg [&>*]:min-h-11 [&>*]:flex-1 [&>*]:justify-center [&>*]:rounded-none [&>*]:bg-transparent [&>*]:text-center"
        renderCard={c => (
          <>
            <div className="flex items-center justify-between gap-3">
              <span className="font-mono text-[15px] font-bold tracking-[0.14em] text-tc-text">{c.code}</span>
              <StatusBadge status={c.status} label={STATUS_LABELS[c.status]} />
            </div>
            <p className="mt-1 flex items-center gap-2 text-xs text-tc-text-secondary">
              {c.vehiclePlate ? (
                <>
                  <span className="font-mono tracking-widest text-tc-text">{c.vehiclePlate}</span>
                  {c.assignedAt && <span className="text-tc-text-muted">· desde {formatDate(c.assignedAt)}</span>}
                </>
              ) : (
                <span className="text-tc-text-muted">Sin vehículo asignado</span>
              )}
            </p>
          </>
        )}
        loading={isLoading}
        emptyMessage="No hay tarjetas."
      />

      {data && <Pagination page={page} pageSize={data.pageSize} total={data.total} noun="tarjetas" onPage={setPage} />}

      {showBatchModal && (
        <Modal title="Generar lote de tarjetas" onClose={() => setShowBatchModal(false)} className="max-w-xs">
          <div className="space-y-4">
            <FormField
              label="Cantidad"
              type="number"
              min={1}
              max={500}
              value={batchQty}
              onChange={(e) => setBatchQty(Number(e.target.value))}
              inputClassName="tabular-nums"
            />
            <div className="flex justify-end gap-2">
              <Button variant="secondary" size="sm" onClick={() => setShowBatchModal(false)}>
                Cancelar
              </Button>
              <Button size="sm" loading={batchMut.isPending} onClick={() => batchMut.mutate()}>
                Generar
              </Button>
            </div>
          </div>
        </Modal>
      )}

      {showAssignByCodeModal && (
        <Modal title="Asignar tarjeta por código" onClose={() => setShowAssignByCodeModal(false)} className="max-w-sm">
          <div className="space-y-4">
            <FormField
              label="Código de la tarjeta"
              inputClassName="font-mono tracking-widest uppercase"
              placeholder="Ej. K3M9T2XQ7R"
              value={assignCode}
              onChange={(e) => setAssignCode(e.target.value.toUpperCase())}
            />
            <VehiclePicker value={assignVehicleId} onChange={id => setAssignVehicleId(id)} />
            <div className="flex justify-end gap-2">
              <Button variant="secondary" size="sm" onClick={() => setShowAssignByCodeModal(false)}>
                Cancelar
              </Button>
              <Button
                size="sm"
                loading={assignByCodeMut.isPending}
                disabled={!assignCode || !assignVehicleId}
                onClick={() => assignByCodeMut.mutate()}
              >
                Asignar
              </Button>
            </div>
          </div>
        </Modal>
      )}

      {showScannerModal && (
        <QrScannerModal onClose={() => setShowScannerModal(false)} />
      )}

      {revokeCardId && (
        <Modal title="Revocar tarjeta" onClose={() => setRevokeCardId(null)} className="max-w-sm">
          <div className="space-y-4">
            <p className="text-sm text-tc-text-secondary">
              Esta acción es irreversible. Ingresá el motivo.
            </p>
            <FormTextarea
              label="Motivo de revocación"
              rows={3}
              value={revokeReason}
              onChange={(e) => setRevokeReason(e.target.value)}
            />
            <div className="flex justify-end gap-2">
              <Button variant="secondary" size="sm" onClick={() => setRevokeCardId(null)}>
                Cancelar
              </Button>
              <Button
                variant="danger"
                size="sm"
                loading={revokeMut.isPending}
                disabled={!revokeReason.trim()}
                onClick={() => revokeMut.mutate({ id: revokeCardId, reason: revokeReason })}
              >
                Revocar
              </Button>
            </div>
          </div>
        </Modal>
      )}

      {activityCard && (
        <QrActivityModal cardId={activityCard.id} code={activityCard.code} onClose={() => setActivityCard(null)} />
      )}
    </div>
  )
}
