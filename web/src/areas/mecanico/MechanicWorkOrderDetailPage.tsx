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
import { LoadError } from '../../components/LoadError'
import { FormTextarea } from '../../components/ui/FormField'
import { Button } from '../../components/ui/Button'
import { Modal } from '../../components/ui/Modal'
import { WorkOrderPhotos } from '../../components/WorkOrderPhotos'
import { Timeline } from '../../components/ui/Timeline'
import { vehiclesApi } from '../../lib/api/vehicles'
import { useAuth } from '../../lib/auth/AuthContext'
import { actionErrorMessage } from '../../lib/api/errors'

const MECHANIC_TRANSITIONS: Partial<Record<WorkOrderStatus, WorkOrderStatus[]>> = {
  pending: ['in_progress'],
  in_progress: ['waiting_parts', 'completed'],
  waiting_parts: ['in_progress'],
}


function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="border border-tc-border bg-white overflow-hidden">
      <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
        <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">{title}</span>
      </div>
      <div className="p-4">{children}</div>
    </div>
  )
}

export function MechanicWorkOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()
  const { user } = useAuth()
  const [changingStatus, setChangingStatus] = useState<WorkOrderStatus | null>(null)
  const [note, setNote] = useState('')
  const [statusError, setStatusError] = useState<string | null>(null)
  const [savingStatus, setSavingStatus] = useState(false)
  const [editingNotes, setEditingNotes] = useState(false)
  const [mechanicNotes, setMechanicNotes] = useState('')
  const [savingNotes, setSavingNotes] = useState(false)
  const [notesError, setNotesError] = useState<string | null>(null)

  const { data: order, isLoading, error, refetch } = useQuery({
    queryKey: ['work-order', id],
    queryFn: () => workOrdersApi.get(id!),
    enabled: !!id,
  })

  // Historial de la orden y servicios anteriores del vehículo (sin importes: el mecánico no ve precios).
  const { data: history } = useQuery({
    queryKey: ['work-order-history', id],
    queryFn: () => workOrdersApi.getHistory(id!),
    enabled: !!id,
  })
  const { data: vehicleHistory } = useQuery({
    queryKey: ['vehicle-history', order?.vehicleId],
    queryFn: () => vehiclesApi.history(order!.vehicleId, 1, 10),
    enabled: !!order?.vehicleId,
  })
  const previousOrders = (vehicleHistory?.items ?? []).filter(e => e.type === 'work_order' && e.id !== id)

  const [assigning, setAssigning] = useState(false)
  const [assignError, setAssignError] = useState<string | null>(null)

  // Doc 06 /m/ordenes/:id: "Asignarme" (solo órdenes sin asignar; el servidor resuelve que es el propio mecánico).
  async function handleAssignSelf() {
    if (!id) return
    setAssigning(true)
    setAssignError(null)
    try {
      await workOrdersApi.assign(id)
      queryClient.invalidateQueries({ queryKey: ['work-order', id] })
      queryClient.invalidateQueries({ queryKey: ['work-order-history', id] })
      queryClient.invalidateQueries({ queryKey: ['mechanic-work-orders'] })
    } catch (err) {
      setAssignError(actionErrorMessage(err, 'No se pudo asignar la orden.'))
    } finally {
      setAssigning(false)
    }
  }

  async function handleChangeStatus() {
    if (!changingStatus || !id) return
    setSavingStatus(true)
    setStatusError(null)
    try {
      await workOrdersApi.changeStatus(id, { toStatus: changingStatus, note: note || null })
      queryClient.invalidateQueries({ queryKey: ['work-order', id] })
      queryClient.invalidateQueries({ queryKey: ['work-order-history', id] })
      queryClient.invalidateQueries({ queryKey: ['mechanic-work-orders'] })
      setChangingStatus(null)
      setNote('')
    } catch (e) {
      setStatusError(actionErrorMessage(e, 'No se pudo cambiar el estado.'))
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
      setNotesError(actionErrorMessage(e, 'No se pudo guardar la nota.'))
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
        <LoadError what="la orden" onRetry={() => refetch()} />
      </div>
    )
  }

  // Se ven todas las órdenes del taller, pero solo se modifican las propias (o las libres).
  const isOthers = order.assignedMechanicId != null && order.assignedMechanicId !== user?.membershipId
  const transitions = isOthers ? [] : (MECHANIC_TRANSITIONS[order.status] ?? [])
  const vehicleLabel = [order.vehicleBrand, order.vehicleModel].filter(Boolean).join(' ')

  return (
    <div className="space-y-3 px-4 pb-6">
      <div className="flex items-center justify-between py-2">
        <div className="flex items-center gap-3">
          <span className="font-mono font-bold text-lg text-tc-text">#{order.number}</span>
          <StatusBadge status={order.status} label={STATUS_LABELS[order.status]} />
        </div>
        <Link to="/m" className="text-xs font-bold tracking-wider uppercase text-tc-purple hover:opacity-70">
          ← Volver
        </Link>
      </div>

      {isOthers && (
        <div role="note" className="border border-tc-border bg-tc-stub-bg px-4 py-3 text-sm text-tc-text-secondary">
          Esta orden es de <strong>{order.assignedMechanicName ?? 'otro mecánico'}</strong>. Podés verla, pero solo él o ella puede cambiarle el estado, anotar o subir fotos.
        </div>
      )}

      {order.assignedMechanicId == null && order.status !== 'delivered' && order.status !== 'cancelled' && (
        <Section title="Asignación">
          <div className="flex items-center justify-between gap-3">
            <p className="text-sm text-amber-700">Esta orden no tiene mecánico asignado.</p>
            <Button size="sm" loading={assigning} onClick={handleAssignSelf}>Asignarme</Button>
          </div>
          {assignError && <p role="alert" className="mt-2 text-xs text-tc-stamp-red">{assignError}</p>}
        </Section>
      )}

      <Section title="Vehículo">
        <div className="space-y-1">
          <p className="font-mono font-bold tracking-widest text-tc-text">{order.vehiclePlate}</p>
          {vehicleLabel && <p className="text-sm text-tc-text-secondary">{vehicleLabel}</p>}
          <p className="text-sm text-tc-text-secondary">
            Odómetro:{' '}
            {order.odometerKm > 0 ? (
              <strong className="tabular-nums">{order.odometerKm.toLocaleString('es-AR')} km</strong>
            ) : (
              <span className="text-tc-text-muted">sin registrar</span>
            )}
          </p>
          {order.fuelLevel != null && (
            <p className="text-sm text-tc-text-secondary">
              Combustible: <strong>{order.fuelLevel}%</strong>
            </p>
          )}
          <p className="text-sm text-tc-text-secondary">
            Prioridad: <strong>{PRIORITY_LABELS[order.priority]}</strong>
          </p>
        </div>
      </Section>

      {order.reportedIssue && (
        <Section title="Problema reportado">
          <p className="text-sm text-tc-text-secondary">{order.reportedIssue}</p>
        </Section>
      )}

      {order.items.length > 0 && (
        <Section title="Trabajos / repuestos">
          <ul className="space-y-1.5">
            {order.items.map(item => (
              <li key={item.id} className="flex items-baseline justify-between text-sm border-b border-tc-border pb-1.5 last:border-0 last:pb-0">
                <span className="text-tc-text-secondary">{item.description}</span>
                <span className="text-tc-text-muted tabular-nums text-xs ml-3 shrink-0">× {item.quantity}</span>
              </li>
            ))}
          </ul>
        </Section>
      )}

      <Section title="Fotos">
        <WorkOrderPhotos workOrderId={order.id} readOnly={isOthers} canDelete={order.assignedMechanicId != null && !isOthers} />
      </Section>

      <Section title={isOthers ? 'Notas del mecánico' : 'Mis notas'}>
        {editingNotes ? (
          <div className="space-y-2">
            <FormTextarea
              label="Notas del trabajo"
              value={mechanicNotes}
              onChange={e => setMechanicNotes(e.target.value)}
              rows={3}
              maxLength={2000}
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
              ? <p className="text-sm text-tc-text-secondary">{order.mechanicNotes}</p>
              : <p className="text-sm text-tc-text-muted">Sin notas</p>
            }
            {!isOthers && <button
              onClick={() => { setMechanicNotes(order.mechanicNotes ?? ''); setEditingNotes(true) }}
              className="text-xs font-bold tracking-wider uppercase text-tc-purple hover:opacity-70 shrink-0"
            >
              {order.mechanicNotes ? 'Editar' : 'Agregar'}
            </button>}
          </div>
        )}
      </Section>

      {history && history.length > 0 && (
        <Section title="Historial de la orden">
          <Timeline
            label="Historial de la orden"
            showTime
            items={history.map(change => ({
              id: change.id,
              date: change.changedAt,
              kind: 'status' as const,
              title: (
                <>
                  {change.fromStatus && <>{STATUS_LABELS[change.fromStatus as WorkOrderStatus]} → </>}
                  {STATUS_LABELS[change.toStatus as WorkOrderStatus]}
                </>
              ),
              detail: change.note,
              author: change.changedByName,
            }))}
          />
        </Section>
      )}

      {previousOrders.length > 0 && (
        <Section title="Servicios anteriores de este vehículo">
          <Timeline
            label="Servicios anteriores"
            showTime={false}
            items={previousOrders.map(e => ({
              id: e.id,
              date: e.date,
              kind: 'order' as const,
              title: `Orden ${e.number}`,
              detail: e.odometerKm != null ? `${e.odometerKm.toLocaleString('es-AR')} km` : null,
              badge: <StatusBadge status={e.status} label={STATUS_LABELS[e.status as WorkOrderStatus] ?? e.status} />,
            }))}
          />
        </Section>
      )}

      {transitions.length > 0 && (
        <Section title="Cambiar estado">
          <div className="flex flex-wrap gap-2">
            {transitions.map(toStatus => (
              <button
                key={toStatus}
                onClick={() => setChangingStatus(toStatus)}
                className="border border-tc-border bg-white px-4 py-2 text-sm font-medium text-tc-text-secondary hover:bg-tc-surface transition-colors"
              >
                → {STATUS_LABELS[toStatus]}
              </button>
            ))}
          </div>
        </Section>
      )}

      {changingStatus && (
        <Modal
          title={`Cambiar a: ${STATUS_LABELS[changingStatus]}`}
          onClose={() => { setChangingStatus(null); setNote('') }}
          className="max-w-sm"
        >
          <div className="space-y-3">
            <FormTextarea
              label="Nota para el taller (opcional)"
              value={note}
              onChange={e => setNote(e.target.value)}
              rows={2}
              placeholder="Queda en el historial de la orden"
            />
            {statusError && <p role="alert" className="text-xs text-tc-stamp-red">{statusError}</p>}
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
        </Modal>
      )}
    </div>
  )
}
