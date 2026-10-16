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

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Recordatorios</h1>
        <div className="flex gap-2">
          <Button
            variant="secondary"
            loading={evaluateMut.isPending}
            onClick={() => evaluateMut.mutate()}
          >
            Evaluar vencidos
          </Button>
          <Button onClick={() => { setEditId(null); setShowModal(true) }}>
            Nuevo recordatorio
          </Button>
        </div>
      </div>

      {error && (
        <div className="rounded border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
          {error}
          <button className="ml-2 underline" onClick={() => setError(null)}>Cerrar</button>
        </div>
      )}

      <div className="overflow-x-auto rounded border border-slate-200 bg-white">
        <table className="min-w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs text-slate-500 uppercase">
            <tr>
              <th className="px-4 py-2">Vehículo</th>
              <th className="px-4 py-2">Tipo</th>
              <th className="px-4 py-2">Título</th>
              <th className="px-4 py-2">Fecha límite</th>
              <th className="px-4 py-2">Km límite</th>
              <th className="px-4 py-2">Último aviso</th>
              <th className="px-4 py-2">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading && (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-slate-400">Cargando…</td>
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
                <td colSpan={7} className="px-4 py-8 text-center text-slate-400">
                  No hay recordatorios.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

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
    <tr className="hover:bg-slate-50">
      <td className="px-4 py-2 font-mono text-sm">{reminder.vehiclePlate}</td>
      <td className="px-4 py-2 text-slate-600">{getReminderKindLabel(reminder.kind)}</td>
      <td className="px-4 py-2">
        {isDueSoon && (
          <span className="mr-1 inline-block rounded bg-amber-100 px-1.5 py-0.5 text-xs text-amber-700">
            Próximo
          </span>
        )}
        {reminder.title}
      </td>
      <td className="px-4 py-2 text-slate-600">
        {reminder.dueDate
          ? new Date(reminder.dueDate).toLocaleDateString('es-AR')
          : <span className="text-slate-400">—</span>}
      </td>
      <td className="px-4 py-2 text-slate-600">
        {reminder.dueKm != null
          ? `${reminder.dueKm.toLocaleString('es-AR')} km`
          : <span className="text-slate-400">—</span>}
      </td>
      <td className="px-4 py-2 text-slate-500">
        {reminder.lastNotifiedAt
          ? new Date(reminder.lastNotifiedAt).toLocaleDateString('es-AR')
          : <span className="text-slate-400">—</span>}
      </td>
      <td className="px-4 py-2">
        <div className="flex items-center gap-2 text-xs">
          <button className="text-blue-600 hover:underline" onClick={onEdit}>Editar</button>
          <button className="text-red-600 hover:underline" onClick={onDelete}>Eliminar</button>
        </div>
      </td>
    </tr>
  )
}
