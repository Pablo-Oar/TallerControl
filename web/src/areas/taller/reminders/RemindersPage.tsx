import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { remindersApi, getReminderKindLabel, type ReminderListDto } from '../../../lib/api/reminders'
import { Button } from '../../../components/ui/Button'
import { ReminderFormModal } from './ReminderFormModal'

export function RemindersPage() {
  const qc = useQueryClient()
  const [page, setPage] = useState(1)
  const [showModal, setShowModal] = useState(false)
  const [editId, setEditId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['reminders', page],
    queryFn: () => remindersApi.search({ page, pageSize: 25 }),
  })

  const invalidate = () => qc.invalidateQueries({ queryKey: ['reminders'] })

  const deleteMut = useMutation({
    mutationFn: (id: string) => remindersApi.delete(id),
    onSuccess: invalidate,
    onError: (e: Error) => setError(e.message),
  })

  const evaluateMut = useMutation({
    mutationFn: () => remindersApi.evaluate(),
    onSuccess: (result) => {
      invalidate()
      setError(null)
      alert(`Evaluación completada: ${result.notificationsCreated} notificaciones creadas.`)
    },
    onError: (e: Error) => setError(e.message),
  })

  const totalPages = data ? Math.ceil(data.total / data.pageSize) : 1

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold tracking-widest text-tc-text uppercase">Recordatorios</h1>
        <div className="flex gap-2">
          <Button
            variant="secondary"
            size="sm"
            loading={evaluateMut.isPending}
            onClick={() => evaluateMut.mutate()}
          >
            Evaluar vencidos
          </Button>
          <Button size="sm" onClick={() => { setEditId(null); setShowModal(true) }}>
            Nuevo recordatorio
          </Button>
        </div>
      </div>

      {error && (
        <div className="border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700 flex items-center justify-between">
          <span>{error}</span>
          <button className="text-xs underline" onClick={() => setError(null)}>Cerrar</button>
        </div>
      )}

      <div className="border border-tc-border bg-white overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead>
            <tr className="bg-tc-stub-bg border-b border-tc-border">
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Vehículo</th>
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted hidden sm:table-cell">Tipo</th>
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Título</th>
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted hidden sm:table-cell">Fecha límite</th>
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted hidden md:table-cell">Km límite</th>
              <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted hidden md:table-cell">Último aviso</th>
              <th className="px-4 py-2" />
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td colSpan={7} className="px-4 py-10 text-center text-sm text-tc-text-muted">Cargando…</td>
              </tr>
            )}
            {data?.items.map((r) => (
              <ReminderRow
                key={r.id}
                reminder={r}
                onEdit={() => { setEditId(r.id); setShowModal(true) }}
                onDelete={() => deleteMut.mutate(r.id)}
              />
            ))}
            {data?.items.length === 0 && (
              <tr>
                <td colSpan={7} className="px-4 py-10 text-center text-sm text-tc-text-muted">
                  No hay recordatorios.
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

      {showModal && (
        <ReminderFormModal
          id={editId}
          onClose={() => setShowModal(false)}
          onSaved={() => { setShowModal(false); invalidate() }}
        />
      )}
    </div>
  )
}

function ReminderRow({
  reminder,
  onEdit,
  onDelete,
}: {
  reminder: ReminderListDto
  onEdit: () => void
  onDelete: () => void
}) {
  const isDueSoon = reminder.dueDate
    ? new Date(reminder.dueDate) <= new Date(Date.now() + 7 * 86400000)
    : false

  return (
    <tr className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
      <td className="px-4 py-2.5 font-mono tracking-widest text-sm text-tc-text">{reminder.vehiclePlate}</td>
      <td className="px-4 py-2.5 text-tc-text-secondary text-xs hidden sm:table-cell">{getReminderKindLabel(reminder.kind)}</td>
      <td className="px-4 py-2.5">
        {isDueSoon && (
          <span className="mr-1.5 inline-block border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-[10px] font-bold tracking-wider uppercase text-amber-700">
            Próximo
          </span>
        )}
        <span className="text-sm text-tc-text">{reminder.title}</span>
      </td>
      <td className="px-4 py-2.5 text-sm text-tc-text-secondary tabular-nums hidden sm:table-cell">
        {reminder.dueDate
          ? new Date(reminder.dueDate).toLocaleDateString('es-AR')
          : <span className="text-tc-text-muted">—</span>}
      </td>
      <td className="px-4 py-2.5 text-sm text-tc-text-secondary tabular-nums hidden md:table-cell">
        {reminder.dueKm != null
          ? `${reminder.dueKm.toLocaleString('es-AR')} km`
          : <span className="text-tc-text-muted">—</span>}
      </td>
      <td className="px-4 py-2.5 text-sm text-tc-text-secondary tabular-nums hidden md:table-cell">
        {reminder.lastNotifiedAt
          ? new Date(reminder.lastNotifiedAt).toLocaleDateString('es-AR')
          : <span className="text-tc-text-muted">—</span>}
      </td>
      <td className="px-4 py-2.5 text-right">
        <div className="flex items-center justify-end gap-3 text-xs">
          <button className="text-tc-purple hover:opacity-70" onClick={onEdit}>Editar</button>
          <button className="text-tc-stamp-red hover:opacity-70" onClick={onDelete}>Eliminar</button>
        </div>
      </td>
    </tr>
  )
}
