import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  appointmentsApi,
  getAppointmentStatusLabel,
  type AppointmentListDto,
  type AppointmentStatus,
} from '../../../lib/api/appointments'
import { Button } from '../../../components/ui/Button'
import { AppointmentFormModal } from './AppointmentFormModal'

const STATUS_OPTIONS: { value: AppointmentStatus | ''; label: string }[] = [
  { value: '', label: 'Todos' },
  { value: 'pending', label: 'Pendientes' },
  { value: 'confirmed', label: 'Confirmados' },
  { value: 'completed', label: 'Completados' },
  { value: 'cancelled', label: 'Cancelados' },
]

const STATUS_BADGE: Record<AppointmentStatus, string> = {
  pending: 'bg-yellow-100 text-yellow-800',
  confirmed: 'bg-blue-100 text-blue-800',
  completed: 'bg-green-100 text-green-800',
  cancelled: 'bg-red-100 text-red-800',
}

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
        <h1 className="text-2xl font-semibold">Agenda</h1>
        <Button onClick={() => setShowModal(true)}>Nuevo turno</Button>
      </div>

      {error && (
        <div className="rounded border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
          {error}
          <button className="ml-2 underline" onClick={() => setError(null)}>
            Cerrar
          </button>
        </div>
      )}

      <div className="flex flex-wrap gap-3">
        <input
          type="date"
          value={date}
          onChange={e => { setDate(e.target.value); setPage(1) }}
          className="rounded border px-3 py-1.5 text-sm"
        />
        <select
          value={status}
          onChange={e => { setStatus(e.target.value as AppointmentStatus | ''); setPage(1) }}
          className="rounded border px-3 py-1.5 text-sm"
        >
          {STATUS_OPTIONS.map(o => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </div>

      {isLoading ? (
        <p className="text-sm text-slate-500">Cargando…</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3">Fecha / hora</th>
                <th className="px-4 py-3">Cliente</th>
                <th className="px-4 py-3">Vehículo</th>
                <th className="px-4 py-3">Mecánico</th>
                <th className="px-4 py-3">Duración</th>
                <th className="px-4 py-3">Estado</th>
                <th className="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {items.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-4 py-8 text-center text-slate-400">
                    No hay turnos para los filtros seleccionados.
                  </td>
                </tr>
              ) : (
                items.map(appt => (
                  <tr key={appt.id} className="hover:bg-slate-50">
                    <td className="px-4 py-3 font-medium">
                      {new Date(appt.scheduledAt).toLocaleString('es-AR', {
                        dateStyle: 'short',
                        timeStyle: 'short',
                      })}
                    </td>
                    <td className="px-4 py-3">{appt.customerName}</td>
                    <td className="px-4 py-3">{appt.vehiclePlate}</td>
                    <td className="px-4 py-3">{appt.mechanicName ?? <span className="text-slate-400">Sin asignar</span>}</td>
                    <td className="px-4 py-3">{appt.totalDurationMinutes} min</td>
                    <td className="px-4 py-3">
                      <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_BADGE[appt.status]}`}>
                        {getAppointmentStatusLabel(appt.status)}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      {(appt.status === 'pending' || appt.status === 'confirmed') && (
                        <button
                          onClick={() => { setCancellingId(appt.id); setCancelReason('') }}
                          className="text-xs text-red-600 hover:underline"
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
        <div className="flex items-center justify-between text-sm text-slate-500">
          <span>{total} turnos en total</span>
          <div className="flex gap-2">
            <button
              disabled={page <= 1}
              onClick={() => setPage(p => p - 1)}
              className="rounded border px-3 py-1 disabled:opacity-40"
            >
              Anterior
            </button>
            <span className="px-2 py-1">{page} / {totalPages}</span>
            <button
              disabled={page >= totalPages}
              onClick={() => setPage(p => p + 1)}
              className="rounded border px-3 py-1 disabled:opacity-40"
            >
              Siguiente
            </button>
          </div>
        </div>
      )}

      {/* Modal de nuevo turno */}
      {showModal && (
        <AppointmentFormModal onClose={() => setShowModal(false)} />
      )}

      {/* Dialog de cancelación */}
      {cancellingId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="w-full max-w-sm rounded-xl bg-white p-6 shadow-xl">
            <h3 className="mb-3 font-semibold">Cancelar turno</h3>
            <textarea
              value={cancelReason}
              onChange={e => setCancelReason(e.target.value)}
              placeholder="Motivo de cancelación (obligatorio)"
              rows={3}
              className="w-full rounded border px-3 py-2 text-sm"
            />
            <div className="mt-4 flex justify-end gap-2">
              <button
                onClick={() => setCancellingId(null)}
                className="rounded border px-4 py-2 text-sm"
              >
                Volver
              </button>
              <button
                disabled={!cancelReason.trim() || cancelMut.isPending}
                onClick={() => cancelMut.mutate({ id: cancellingId, reason: cancelReason })}
                className="rounded bg-red-600 px-4 py-2 text-sm text-white hover:bg-red-700 disabled:opacity-50"
              >
                {cancelMut.isPending ? 'Cancelando…' : 'Confirmar cancelación'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
