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
import { QrScannerModal } from './QrScannerModal'

const STATUS_LABELS: Record<QrCardStatus, string> = {
  free: 'Libre',
  assigned: 'Asignada',
  lost: 'Perdida',
  revoked: 'Revocada',
}

const STATUS_COLORS: Record<QrCardStatus, string> = {
  free: 'bg-slate-100 text-slate-700',
  assigned: 'bg-green-100 text-green-800',
  lost: 'bg-amber-100 text-amber-800',
  revoked: 'bg-red-100 text-red-700',
}

function StatusBadge({ status }: { status: QrCardStatus }) {
  return (
    <span className={`rounded px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[status]}`}>
      {STATUS_LABELS[status]}
    </span>
  )
}

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

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Tarjetas QR</h1>
        <div className="flex gap-2">
          <Button variant="secondary" onClick={() => setShowScannerModal(true)}>
            Escanear código
          </Button>
          <Button variant="secondary" onClick={() => setShowAssignByCodeModal(true)}>
            Asignar por código
          </Button>
          <Button onClick={() => setShowBatchModal(true)}>Generar lote</Button>
        </div>
      </div>

      {error && (
        <div className="rounded border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
          {error}
          <button className="ml-2 underline" onClick={() => setError(null)}>
            Cerrar
          </button>
        </div>
      )}

      {/* Filtros */}
      <div className="flex flex-wrap gap-2">
        <input
          className="rounded border border-slate-300 px-3 py-1.5 text-sm"
          placeholder="Buscar código..."
          value={q}
          onChange={(e) => { setQ(e.target.value); setPage(1) }}
        />
        <select
          className="rounded border border-slate-300 px-3 py-1.5 text-sm"
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

      {/* Tabla */}
      <div className="overflow-x-auto rounded border border-slate-200 bg-white">
        <table className="min-w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs text-slate-500 uppercase">
            <tr>
              <th className="px-4 py-2">Código</th>
              <th className="px-4 py-2">Estado</th>
              <th className="px-4 py-2">Vehículo</th>
              <th className="px-4 py-2">Asignada</th>
              <th className="px-4 py-2">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading && (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-slate-400">
                  Cargando…
                </td>
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
                <td colSpan={5} className="px-4 py-8 text-center text-slate-400">
                  No hay tarjetas.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Paginación */}
      {data && data.total > data.pageSize && (
        <div className="flex items-center gap-3 text-sm">
          <button
            className="rounded border px-3 py-1 disabled:opacity-50"
            disabled={page === 1}
            onClick={() => setPage((p) => p - 1)}
          >
            ← Anterior
          </button>
          <span className="text-slate-500">
            Página {page} de {Math.ceil(data.total / data.pageSize)}
          </span>
          <button
            className="rounded border px-3 py-1 disabled:opacity-50"
            disabled={page >= Math.ceil(data.total / data.pageSize)}
            onClick={() => setPage((p) => p + 1)}
          >
            Siguiente →
          </button>
        </div>
      )}

      {/* Modal generar lote */}
      {showBatchModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="w-80 rounded-lg bg-white p-6 shadow-xl">
            <h2 className="mb-4 text-lg font-semibold">Generar lote de tarjetas</h2>
            <label className="mb-1 block text-sm font-medium">Cantidad</label>
            <input
              type="number"
              min={1}
              max={500}
              value={batchQty}
              onChange={(e) => setBatchQty(Number(e.target.value))}
              className="mb-4 w-full rounded border border-slate-300 px-3 py-2 text-sm"
            />
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setShowBatchModal(false)}>
                Cancelar
              </Button>
              <Button
                loading={batchMut.isPending}
                onClick={() => batchMut.mutate()}
              >
                Generar
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Modal asignar por código */}
      {showAssignByCodeModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="w-96 rounded-lg bg-white p-6 shadow-xl">
            <h2 className="mb-4 text-lg font-semibold">Asignar tarjeta por código</h2>
            <div className="space-y-3">
              <div>
                <label className="mb-1 block text-sm font-medium">
                  Código de la tarjeta
                </label>
                <input
                  className="w-full rounded border border-slate-300 px-3 py-2 text-sm uppercase"
                  placeholder="Ej. ABC12345"
                  value={assignCode}
                  onChange={(e) => setAssignCode(e.target.value.toUpperCase())}
                />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium">ID del vehículo</label>
                <input
                  className="w-full rounded border border-slate-300 px-3 py-2 text-sm font-mono text-xs"
                  placeholder="UUID del vehículo"
                  value={assignVehicleId}
                  onChange={(e) => setAssignVehicleId(e.target.value)}
                />
              </div>
            </div>
            <div className="mt-4 flex justify-end gap-2">
              <Button
                variant="secondary"
                onClick={() => setShowAssignByCodeModal(false)}
              >
                Cancelar
              </Button>
              <Button
                loading={assignByCodeMut.isPending}
                disabled={!assignCode || !assignVehicleId}
                onClick={() => assignByCodeMut.mutate()}
              >
                Asignar
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Modal escanear código */}
      {showScannerModal && (
        <QrScannerModal onClose={() => setShowScannerModal(false)} />
      )}

      {/* Modal revocar */}
      {revokeCardId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="w-96 rounded-lg bg-white p-6 shadow-xl">
            <h2 className="mb-1 text-lg font-semibold">Revocar tarjeta</h2>
            <p className="mb-4 text-sm text-slate-500">
              Esta acción es irreversible. Ingresá el motivo.
            </p>
            <textarea
              rows={3}
              className="w-full rounded border border-slate-300 px-3 py-2 text-sm"
              placeholder="Motivo de revocación"
              value={revokeReason}
              onChange={(e) => setRevokeReason(e.target.value)}
            />
            <div className="mt-4 flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setRevokeCardId(null)}>
                Cancelar
              </Button>
              <Button
                variant="danger"
                loading={revokeMut.isPending}
                disabled={!revokeReason.trim()}
                onClick={() =>
                  revokeMut.mutate({ id: revokeCardId, reason: revokeReason })
                }
              >
                Revocar
              </Button>
            </div>
          </div>
        </div>
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
    <tr className="hover:bg-slate-50">
      <td className="px-4 py-2 font-mono text-sm font-medium tracking-wider">
        {card.code}
      </td>
      <td className="px-4 py-2">
        <StatusBadge status={card.status} />
      </td>
      <td className="px-4 py-2 text-slate-600">
        {card.vehiclePlate ?? <span className="text-slate-400">—</span>}
      </td>
      <td className="px-4 py-2 text-slate-500">
        {card.assignedAt
          ? new Date(card.assignedAt).toLocaleDateString('es-AR')
          : <span className="text-slate-400">—</span>}
      </td>
      <td className="px-4 py-2">
        <div className="flex items-center gap-2 text-xs">
          {card.status === 'assigned' && (
            <>
              <button
                className="text-amber-600 hover:underline"
                onClick={onMarkLost}
              >
                Marcar perdida
              </button>
              <button
                className="text-slate-500 hover:underline"
                onClick={onUnassign}
              >
                Desasignar
              </button>
              <a
                href={getQrCardPrintUrl(card.id)}
                target="_blank"
                rel="noreferrer"
                className="text-blue-600 hover:underline"
              >
                PDF
              </a>
            </>
          )}
          {card.status === 'lost' && (
            <>
              <button
                className="text-green-600 hover:underline"
                onClick={onUnmarkLost}
              >
                Desmarcar perdida
              </button>
              <button
                className="text-slate-500 hover:underline"
                onClick={onUnassign}
              >
                Desasignar
              </button>
            </>
          )}
          {card.status !== 'revoked' && (
            <button className="text-red-600 hover:underline" onClick={onRevoke}>
              Revocar
            </button>
          )}
        </div>
      </td>
    </tr>
  )
}
