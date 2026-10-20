import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  appointmentsApi,
  getAppointmentStatusLabel,
  type AppointmentListDto,
  type AppointmentStatus,
} from '../../../lib/api/appointments'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { AppointmentFormModal } from './AppointmentFormModal'
import { inputCls } from '../../../lib/styles'

const STATUS_OPTIONS: { value: AppointmentStatus | ''; label: string }[] = [
  { value: '', label: 'Todos' },
  { value: 'pending', label: 'Pendientes' },
  { value: 'confirmed', label: 'Confirmados' },
  { value: 'completed', label: 'Completados' },
  { value: 'cancelled', label: 'Cancelados' },
]


export function AppointmentsPage() {
  const qc = useQueryClient()
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState<AppointmentStatus | ''>('')
  const [date, setDate] = useState('')
  const [showModal, setShowModal] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [cancellingId, setCancellingId] = useState<string | null>(null)
  const [cancelReason, setCancelReason] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['appointments', page, status, date],
    queryFn: () =>
      appointmentsApi.search({
        page,
        pageSize: 25,
        status: status || undefined,
        date: date || undefined,
      }),
  })

  const invalidate = () => qc.invalidateQueries({ queryKey: ['appointments'] })

  const cancelMut = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      appointmentsApi.cancel(id, reason),
    onSuccess: () => {
      invalidate()
      setCancellingId(null)
      setCancelReason('')
    },
    onError: (e: Error) => setError(e.message),
  })

  const items: AppointmentListDto[] = data?.items ?? []
  const total = data?.total ?? 0
  const totalPages = Math.ceil(total / 25) || 1

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold tracking-widest text-tc-text uppercase">Agenda</h1>
        <Button size="sm" onClick={() => setShowModal(true)}>Nuevo turno</Button>
      </div>

      {error && (
        <div className="border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700 flex items-center justify-between">
          <span>{error}</span>
          <button className="text-xs underline" onClick={() => setError(null)}>Cerrar</button>
        </div>
      )}

      <div className="flex flex-wrap gap-2">
        <input
          type="date"
          value={date}
          onChange={e => { setDate(e.target.value); setPage(1) }}
          className={inputCls}
        />
        <select
          value={status}
          onChange={e => { setStatus(e.target.value as AppointmentStatus | ''); setPage(1) }}
          className={inputCls}
        >
          {STATUS_OPTIONS.map(o => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
      </div>

      {isLoading ? (
        <div className="border border-tc-border bg-white">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="h-10 animate-pulse border-b border-tc-border bg-tc-surface last:border-0" />
          ))}
        </div>
      ) : (
        <div className="border border-tc-border bg-white overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-tc-stub-bg border-b border-tc-border">
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Fecha / hora</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted hidden sm:table-cell">Cliente</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Vehículo</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted hidden md:table-cell">Mecánico</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted hidden md:table-cell">Duración</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Estado</th>
                <th className="px-4 py-2" />
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-4 py-10 text-center text-sm text-tc-text-muted">
                    No hay turnos para los filtros seleccionados.
                  </td>
                </tr>
              ) : (
                items.map((appt, idx) => (
                  <tr key={appt.id} className={`border-b border-tc-border last:border-0 hover:bg-tc-surface ${idx % 2 === 0 ? '' : ''}`}>
                    <td className="px-4 py-2.5 tabular-nums">
                      <span className="font-medium text-tc-text whitespace-nowrap">
                        {new Date(appt.scheduledAt).toLocaleString('es-AR', {
                          dateStyle: 'short',
                          timeStyle: 'short',
                        })}
                      </span>
                      <p className="mt-0.5 text-xs text-tc-text-secondary sm:hidden">{appt.customerName}</p>
                    </td>
                    <td className="px-4 py-2.5 text-tc-text-secondary hidden sm:table-cell">{appt.customerName}</td>
                    <td className="px-4 py-2.5 font-mono tracking-widest text-sm text-tc-text-secondary">{appt.vehiclePlate}</td>
                    <td className="px-4 py-2.5 text-tc-text-secondary hidden md:table-cell">
                      {appt.mechanicName ?? <span className="text-tc-text-muted">—</span>}
                    </td>
                    <td className="px-4 py-2.5 text-tc-text-secondary tabular-nums hidden md:table-cell">{appt.totalDurationMinutes} min</td>
                    <td className="px-4 py-2.5">
                      <StatusBadge status={appt.status} label={getAppointmentStatusLabel(appt.status)} />
                    </td>
                    <td className="px-4 py-2.5 text-right">
                      {(appt.status === 'pending' || appt.status === 'confirmed') && (
                        <button
                          onClick={() => { setCancellingId(appt.id); setCancelReason('') }}
                          className="text-xs text-tc-stamp-red hover:opacity-70"
                        >
                          Cancelar
                        </button>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-tc-text-secondary">
          <span className="text-xs text-tc-text-muted">{total} turnos en total</span>
          <div className="flex gap-1">
            <button
              disabled={page <= 1}
              onClick={() => setPage(p => p - 1)}
              className="border border-tc-border bg-white px-3 py-1 text-xs disabled:opacity-40 hover:bg-tc-surface"
            >
              ← Anterior
            </button>
            <span className="border border-tc-border bg-tc-stub-bg px-3 py-1 text-xs tabular-nums">
              {page} / {totalPages}
            </span>
            <button
              disabled={page >= totalPages}
              onClick={() => setPage(p => p + 1)}
              className="border border-tc-border bg-white px-3 py-1 text-xs disabled:opacity-40 hover:bg-tc-surface"
            >
              Siguiente →
            </button>
          </div>
        </div>
      )}

      {showModal && (
        <AppointmentFormModal onClose={() => { setShowModal(false); invalidate() }} />
      )}

      {cancellingId && (
        <Modal title="Cancelar turno" onClose={() => setCancellingId(null)} className="max-w-sm">
          <div className="space-y-4">
            <textarea
              value={cancelReason}
              onChange={e => setCancelReason(e.target.value)}
              placeholder="Motivo de cancelación (obligatorio)"
              rows={3}
              className="w-full border border-tc-border bg-white px-3 py-2 text-sm outline-none focus:border-tc-border-mid transition-colors"
            />
            <div className="flex justify-end gap-2">
              <Button type="button" variant="secondary" size="sm" onClick={() => setCancellingId(null)}>
                Volver
              </Button>
              <Button
                variant="danger"
                size="sm"
                disabled={!cancelReason.trim() || cancelMut.isPending}
                loading={cancelMut.isPending}
                onClick={() => cancelMut.mutate({ id: cancellingId, reason: cancelReason })}
              >
                Confirmar cancelación
              </Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  )
}
