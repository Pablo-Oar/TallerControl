import { useState } from 'react'
import { useParams, Link } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  workOrdersApi,
  type WorkOrderStatus,
  STATUS_LABELS,
  STATUS_COLORS,
  PRIORITY_LABELS,
} from '../../lib/api/workOrders'
import { ApiError } from '../../lib/api/client'

// Estados a los que un mecánico puede transicionar
const MECHANIC_TRANSITIONS: Partial<Record<WorkOrderStatus, WorkOrderStatus[]>> = {
  pending: ['in_progress'],
  in_progress: ['waiting_parts', 'completed'],
  waiting_parts: ['in_progress'],
}

export function MechanicWorkOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()
  const [changingStatus, setChangingStatus] = useState<WorkOrderStatus | null>(null)
  const [note, setNote] = useState('')
  const [statusError, setStatusError] = useState<string | null>(null)
  const [savingStatus, setSavingStatus] = useState(false)
  const [editingNotes, setEditingNotes] = useState(false)
  const [mechanicNotes, setMechanicNotes] = useState('')
  const [savingNotes, setSavingNotes] = useState(false)
  const [notesError, setNotesError] = useState<string | null>(null)

  const { data: order, isLoading, error } = useQuery({
    queryKey: ['work-order', id],
    queryFn: () => workOrdersApi.get(id!),
    enabled: !!id,
  })

  async function handleChangeStatus() {
    if (!changingStatus || !id) return
    setSavingStatus(true)
    setStatusError(null)
    try {
      await workOrdersApi.changeStatus(id, { toStatus: changingStatus, note: note || null })
      queryClient.invalidateQueries({ queryKey: ['work-order', id] })
      queryClient.invalidateQueries({ queryKey: ['mechanic-work-orders'] })
      setChangingStatus(null)
      setNote('')
    } catch (e) {
      setStatusError(e instanceof ApiError ? e.detail : 'Error inesperado')
    } finally {
      setSavingStatus(false)
    }
  }

  async function handleSaveNotes() {
    if (!id) return
    setSavingNotes(true)
    setNotesError(null)
    try {
      await workOrdersApi.updateMechanicNotes(id, mechanicNotes || null)
      queryClient.invalidateQueries({ queryKey: ['work-order', id] })
      setEditingNotes(false)
    } catch (e) {
      setNotesError(e instanceof ApiError ? e.detail : 'Error inesperado')
    } finally {
      setSavingNotes(false)
    }
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-8">
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
      </div>
    )
  }

  if (error || !order) {
    return <p className="text-sm text-red-600">No se pudo cargar la orden.</p>
  }

  const transitions = MECHANIC_TRANSITIONS[order.status] ?? []
  const vehicleLabel = [order.vehicleBrand, order.vehicleModel].filter(Boolean).join(' ')

  return (
    <div className="space-y-4">
      {/* Encabezado */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Orden #{order.number}</h1>
          <span className={`mt-1 inline-block rounded-full px-2.5 py-0.5 text-sm font-medium ${STATUS_COLORS[order.status]}`}>
            {STATUS_LABELS[order.status]}
          </span>
        </div>
        <Link to="/m" className="text-sm text-blue-600 hover:underline">← Volver</Link>
      </div>

      {/* Info del vehículo */}
      <div className="rounded-lg border border-slate-200 bg-white p-4">
        <h2 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">Vehículo</h2>
        <p className="font-mono font-medium">{order.vehiclePlate}</p>
        {vehicleLabel && <p className="text-sm text-slate-500">{vehicleLabel}</p>}
        <p className="mt-1 text-sm">Odómetro: <strong>{order.odometerKm.toLocaleString('es-AR')} km</strong></p>
        {order.fuelLevel != null && (
          <p className="text-sm">Combustible: <strong>{order.fuelLevel}%</strong></p>
        )}
      </div>

      {/* Prioridad */}
      <div className="rounded-lg border border-slate-200 bg-white p-4">
        <p className="text-sm">
          <span className="text-slate-500">Prioridad: </span>
          <strong>{PRIORITY_LABELS[order.priority]}</strong>
        </p>
      </div>

      {/* Problema reportado */}
      {order.reportedIssue && (
        <div className="rounded-lg border border-slate-200 bg-white p-4">
          <h2 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">Problema reportado</h2>
          <p className="text-sm">{order.reportedIssue}</p>
        </div>
      )}

      {/* Renglones */}
      {order.items.length > 0 && (
        <div className="rounded-lg border border-slate-200 bg-white p-4">
          <h2 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">Trabajos / repuestos</h2>
          <ul className="space-y-1">
            {order.items.map(item => (
              <li key={item.id} className="flex items-baseline justify-between text-sm">
                <span>{item.description}</span>
                <span className="text-slate-400 tabular-nums">× {item.quantity}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Notas del mecánico */}
      <div className="rounded-lg border border-slate-200 bg-white p-4">
        <div className="mb-2 flex items-center justify-between">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-slate-400">Mis notas</h2>
          {!editingNotes && (
            <button
              onClick={() => { setMechanicNotes(order.mechanicNotes ?? ''); setEditingNotes(true) }}
              className="text-xs text-blue-600 hover:underline"
            >
              {order.mechanicNotes ? 'Editar' : 'Agregar'}
            </button>
          )}
        </div>
        {editingNotes ? (
          <div className="space-y-2">
            <textarea
              value={mechanicNotes}
              onChange={e => setMechanicNotes(e.target.value)}
              rows={3}
              maxLength={2000}
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
              placeholder="Notas del trabajo…"
            />
            {notesError && <p className="text-xs text-red-600">{notesError}</p>}
            <div className="flex gap-2">
              <button
                onClick={handleSaveNotes}
                disabled={savingNotes}
                className="rounded-md bg-blue-600 px-3 py-1.5 text-sm text-white disabled:opacity-60"
              >
                {savingNotes ? 'Guardando…' : 'Guardar'}
              </button>
              <button
                onClick={() => setEditingNotes(false)}
                className="rounded-md border border-slate-300 px-3 py-1.5 text-sm"
              >
                Cancelar
              </button>
            </div>
          </div>
        ) : (
          order.mechanicNotes
            ? <p className="text-sm">{order.mechanicNotes}</p>
            : <p className="text-sm text-slate-400">Sin notas</p>
        )}
      </div>

      {/* Cambio de estado */}
      {transitions.length > 0 && (
        <div className="rounded-lg border border-slate-200 bg-white p-4">
          <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Cambiar estado</h2>
          <div className="flex flex-wrap gap-2">
            {transitions.map(toStatus => (
              <button
                key={toStatus}
                onClick={() => setChangingStatus(toStatus)}
                className="rounded-md border border-blue-200 bg-blue-50 px-3 py-1.5 text-sm text-blue-700 hover:bg-blue-100"
              >
                → {STATUS_LABELS[toStatus]}
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Confirmación de cambio de estado */}
      {changingStatus && (
        <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 p-4">
          <div className="w-full max-w-sm rounded-xl bg-white p-5 shadow-xl">
            <p className="mb-3 font-medium">
              Cambiar a: <strong>{STATUS_LABELS[changingStatus]}</strong>
            </p>
            <textarea
              value={note}
              onChange={e => setNote(e.target.value)}
              rows={2}
              placeholder="Nota (opcional)…"
              className="mb-3 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none"
            />
            {statusError && <p className="mb-2 text-xs text-red-600">{statusError}</p>}
            <div className="flex gap-2">
              <button
                onClick={handleChangeStatus}
                disabled={savingStatus}
                className="flex-1 rounded-md bg-blue-600 py-2 text-sm font-medium text-white disabled:opacity-60"
              >
                {savingStatus ? 'Guardando…' : 'Confirmar'}
              </button>
              <button
                onClick={() => { setChangingStatus(null); setNote('') }}
                className="flex-1 rounded-md border border-slate-300 py-2 text-sm"
              >
                Cancelar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
