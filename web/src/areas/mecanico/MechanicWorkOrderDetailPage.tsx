import { useState } from 'react'
import { useParams, Link } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  workOrdersApi,
  type WorkOrderStatus,
  STATUS_LABELS,
  PRIORITY_LABELS,
} from '../../lib/api/workOrders'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { Button } from '../../components/ui/Button'
import { ApiError } from '../../lib/api/client'

const MECHANIC_TRANSITIONS: Partial<Record<WorkOrderStatus, WorkOrderStatus[]>> = {
  pending: ['in_progress'],
  in_progress: ['waiting_parts', 'completed'],
  waiting_parts: ['in_progress'],
}

const inputCls = 'w-full border border-tc-border bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors'

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="border border-tc-border bg-white overflow-hidden">
      <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
        <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">{title}</span>
      </div>
      <div className="p-4">{children}</div>
    </div>
  )
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
      <div className="space-y-2 px-4 pt-4">
        {[...Array(4)].map((_, i) => (
          <div key={i} className="h-20 animate-pulse border border-tc-border bg-tc-surface" />
        ))}
      </div>
    )
  }

  if (error || !order) {
    return (
      <div className="px-4 pt-4">
        <p className="border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
          No se pudo cargar la orden.
        </p>
      </div>
    )
  }

  const transitions = MECHANIC_TRANSITIONS[order.status] ?? []
  const vehicleLabel = [order.vehicleBrand, order.vehicleModel].filter(Boolean).join(' ')

  return (
    <div className="space-y-3 px-4 pb-6">
      <div className="flex items-center justify-between py-2">
        <div className="flex items-center gap-3">
          <span className="font-mono font-bold text-lg text-slate-900">#{order.number}</span>
          <StatusBadge status={order.status} label={STATUS_LABELS[order.status]} />
        </div>
        <Link to="/m" className="text-xs font-bold tracking-wider uppercase text-tc-purple hover:opacity-70">
          ← Volver
        </Link>
      </div>

      <Section title="Vehículo">
        <div className="space-y-1">
          <p className="font-mono font-bold tracking-widest text-slate-800">{order.vehiclePlate}</p>
          {vehicleLabel && <p className="text-sm text-slate-500">{vehicleLabel}</p>}
          <p className="text-sm text-slate-600">
            Odómetro: <strong className="tabular-nums">{order.odometerKm.toLocaleString('es-AR')} km</strong>
          </p>
          {order.fuelLevel != null && (
            <p className="text-sm text-slate-600">
              Combustible: <strong>{order.fuelLevel}%</strong>
            </p>
          )}
          <p className="text-sm text-slate-600">
            Prioridad: <strong>{PRIORITY_LABELS[order.priority]}</strong>
          </p>
        </div>
      </Section>

      {order.reportedIssue && (
        <Section title="Problema reportado">
          <p className="text-sm text-slate-700">{order.reportedIssue}</p>
        </Section>
      )}

      {order.items.length > 0 && (
        <Section title="Trabajos / repuestos">
          <ul className="space-y-1.5">
            {order.items.map(item => (
              <li key={item.id} className="flex items-baseline justify-between text-sm border-b border-tc-border pb-1.5 last:border-0 last:pb-0">
                <span className="text-slate-700">{item.description}</span>
                <span className="text-slate-400 tabular-nums text-xs ml-3 shrink-0">× {item.quantity}</span>
              </li>
            ))}
          </ul>
        </Section>
      )}

      <Section title="Mis notas">
        {editingNotes ? (
          <div className="space-y-2">
            <textarea
              value={mechanicNotes}
              onChange={e => setMechanicNotes(e.target.value)}
              rows={3}
              maxLength={2000}
              className={inputCls}
              placeholder="Notas del trabajo…"
            />
            {notesError && <p className="text-xs text-tc-stamp-red">{notesError}</p>}
            <div className="flex gap-2">
              <Button size="sm" loading={savingNotes} onClick={handleSaveNotes}>Guardar</Button>
              <Button size="sm" variant="secondary" onClick={() => setEditingNotes(false)}>Cancelar</Button>
            </div>
          </div>
        ) : (
          <div className="flex items-start justify-between gap-3">
            {order.mechanicNotes
              ? <p className="text-sm text-slate-700">{order.mechanicNotes}</p>
              : <p className="text-sm text-slate-400">Sin notas</p>
            }
            <button
              onClick={() => { setMechanicNotes(order.mechanicNotes ?? ''); setEditingNotes(true) }}
              className="text-xs font-bold tracking-wider uppercase text-tc-purple hover:opacity-70 shrink-0"
            >
              {order.mechanicNotes ? 'Editar' : 'Agregar'}
            </button>
          </div>
        )}
      </Section>

      {transitions.length > 0 && (
        <Section title="Cambiar estado">
          <div className="flex flex-wrap gap-2">
            {transitions.map(toStatus => (
              <button
                key={toStatus}
                onClick={() => setChangingStatus(toStatus)}
                className="border border-tc-border bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-tc-surface transition-colors"
              >
                → {STATUS_LABELS[toStatus]}
              </button>
            ))}
          </div>
        </Section>
      )}

      {changingStatus && (
        <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 p-4">
          <div className="w-full max-w-sm border border-tc-border bg-white shadow-xl">
            <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
              <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">
                Cambiar a: <span className="text-slate-700">{STATUS_LABELS[changingStatus]}</span>
              </span>
            </div>
            <div className="p-4 space-y-3">
              <textarea
                value={note}
                onChange={e => setNote(e.target.value)}
                rows={2}
                placeholder="Nota (opcional)…"
                className={inputCls}
              />
              {statusError && <p className="text-xs text-tc-stamp-red">{statusError}</p>}
              <div className="flex gap-2">
                <Button size="sm" loading={savingStatus} onClick={handleChangeStatus} className="flex-1">
                  Confirmar
                </Button>
                <Button
                  size="sm"
                  variant="secondary"
                  onClick={() => { setChangingStatus(null); setNote('') }}
                  className="flex-1"
                >
                  Cancelar
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
