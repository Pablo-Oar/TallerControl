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
import { Modal } from '../../../components/ui/Modal'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { QrScannerModal } from './QrScannerModal'

const STATUS_LABELS: Record<QrCardStatus, string> = {
  free: 'Libre',
  assigned: 'Asignada',
  lost: 'Perdida',
  revoked: 'Revocada',
}

const inputCls = 'border border-tc-border bg-white px-3 py-1.5 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors'
const inputFullCls = 'w-full border border-tc-border bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors'
const labelCls = 'mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500'

export function QrCardsPage() {
  const qc = useQueryClient()
  const [statusFilter, setStatusFilter] = useState<QrCardStatus | ''>('')
  const [q, setQ] = useState('')
  const [page, setPage] = useState(1)
  const [showBatchModal, setShowBatchModal] = useState(false)
  const [showAssignByCodeModal, setShowAssignByCodeModal] = useState(false)
  const [showScannerModal, setShowScannerModal] = useState(false)
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
        pageSize: 25,
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

  const totalPages = data ? Math.ceil(data.total / data.pageSize) : 1

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-xl font-bold tracking-widest text-slate-900 uppercase">Tarjetas QR</h1>
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" size="sm" onClick={() => setShowScannerModal(true)}>
            Escanear código
          </Button>
          <Button variant="secondary" size="sm" onClick={() => setShowAssignByCodeModal(true)}>
            Asignar por código
          </Button>
          <Button size="sm" onClick={() => setShowBatchModal(true)}>Generar lote</Button>
        </div>
      </div>

      {error && (
        <div className="border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700 flex items-center justify-between">
          <span>{error}</span>
          <button className="text-xs underline" onClick={() => setError(null)}>Cerrar</button>
        </div>
      )}

      <div className="flex flex-wrap gap-2">
        <input
          className={inputCls}
          placeholder="Buscar código…"
          value={q}
          onChange={(e) => { setQ(e.target.value); setPage(1) }}
        />
        <select
          className={inputCls}
          value={statusFilter}
          onChange={(e) => { setStatusFilter(e.target.value as QrCardStatus | ''); setPage(1) }}
        >
          <option value="">Todos los estados</option>
          <option value="free">Libre</option>
          <option value="assigned">Asignada</option>
          <option value="lost">Perdida</option>
          <option value="revoked">Revocada</option>
        </select>
      </div>

      <div className="border border-tc-border bg-white overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead>
            <tr className="bg-tc-stub-bg border-b border-tc-border">
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Código</th>
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Estado</th>
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Vehículo</th>
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400 hidden sm:table-cell">Asignada</th>
              <th className="px-4 py-2" />
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td colSpan={5} className="px-4 py-10 text-center text-sm text-slate-400">Cargando…</td>
              </tr>
            )}
            {data?.items.map((card) => (
              <QrCardRow
                key={card.id}
                card={card}
                onUnassign={() => unassignMut.mutate(card.id)}
                onMarkLost={() => markLostMut.mutate({ id: card.id, lost: true })}
                onUnmarkLost={() => markLostMut.mutate({ id: card.id, lost: false })}
                onRevoke={() => setRevokeCardId(card.id)}
              />
            ))}
            {data?.items.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-10 text-center text-sm text-slate-400">
                  No hay tarjetas.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {data && data.total > data.pageSize && (
        <div className="flex items-center gap-1 text-sm">
          <button
            className="border border-tc-border bg-white px-3 py-1 text-xs disabled:opacity-40 hover:bg-tc-surface"
            disabled={page === 1}
            onClick={() => setPage((p) => p - 1)}
          >
            ← Anterior
          </button>
          <span className="border border-tc-border bg-tc-stub-bg px-3 py-1 text-xs tabular-nums">
            {page} / {totalPages}
          </span>
          <button
            className="border border-tc-border bg-white px-3 py-1 text-xs disabled:opacity-40 hover:bg-tc-surface"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            Siguiente →
          </button>
        </div>
      )}

      {showBatchModal && (
        <Modal title="Generar lote de tarjetas" onClose={() => setShowBatchModal(false)} className="max-w-xs">
          <div className="space-y-4">
            <div>
              <label className={labelCls}>Cantidad</label>
              <input
                type="number"
                min={1}
                max={500}
                value={batchQty}
                onChange={(e) => setBatchQty(Number(e.target.value))}
                className={`${inputFullCls} tabular-nums`}
              />
            </div>
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
            <div>
              <label className={labelCls}>Código de la tarjeta</label>
              <input
                className={`${inputFullCls} font-mono tracking-widest uppercase`}
                placeholder="Ej. ABC12345"
                value={assignCode}
                onChange={(e) => setAssignCode(e.target.value.toUpperCase())}
              />
            </div>
            <div>
              <label className={labelCls}>ID del vehículo</label>
              <input
                className={`${inputFullCls} font-mono text-xs`}
                placeholder="UUID del vehículo"
                value={assignVehicleId}
                onChange={(e) => setAssignVehicleId(e.target.value)}
              />
            </div>
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
            <p className="text-sm text-slate-500">
              Esta acción es irreversible. Ingresá el motivo.
            </p>
            <textarea
              rows={3}
              className={`${inputFullCls}`}
              placeholder="Motivo de revocación"
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
    </div>
  )
}

function QrCardRow({
  card,
  onUnassign,
  onMarkLost,
  onUnmarkLost,
  onRevoke,
}: {
  card: QrCardListDto
  onUnassign: () => void
  onMarkLost: () => void
  onUnmarkLost: () => void
  onRevoke: () => void
}) {
  return (
    <tr className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
      <td className="px-4 py-2.5 font-mono text-sm font-bold tracking-widest text-slate-800">
        {card.code}
      </td>
      <td className="px-4 py-2.5">
        <StatusBadge status={card.status} label={STATUS_LABELS[card.status]} />
      </td>
      <td className="px-4 py-2.5 font-mono tracking-widest text-sm text-slate-600">
        {card.vehiclePlate ?? <span className="text-slate-400 font-sans tracking-normal">—</span>}
      </td>
      <td className="px-4 py-2.5 text-sm text-slate-500 tabular-nums hidden sm:table-cell">
        {card.assignedAt
          ? new Date(card.assignedAt).toLocaleDateString('es-AR')
          : <span className="text-slate-400">—</span>}
      </td>
      <td className="px-4 py-2.5 text-right">
        <div className="flex items-center justify-end gap-3 text-xs">
          {card.status === 'assigned' && (
            <>
              <button className="text-amber-600 hover:opacity-70" onClick={onMarkLost}>
                Marcar perdida
              </button>
              <button className="text-slate-500 hover:opacity-70" onClick={onUnassign}>
                Desasignar
              </button>
              <a
                href={getQrCardPrintUrl(card.id)}
                target="_blank"
                rel="noreferrer"
                className="text-tc-purple hover:opacity-70"
              >
                PDF
              </a>
            </>
          )}
          {card.status === 'lost' && (
            <>
              <button className="text-green-700 hover:opacity-70" onClick={onUnmarkLost}>
                Desmarcar perdida
              </button>
              <button className="text-slate-500 hover:opacity-70" onClick={onUnassign}>
                Desasignar
              </button>
            </>
          )}
          {card.status !== 'revoked' && (
            <button className="text-tc-stamp-red hover:opacity-70" onClick={onRevoke}>
              Revocar
            </button>
          )}
        </div>
      </td>
    </tr>
  )
}
