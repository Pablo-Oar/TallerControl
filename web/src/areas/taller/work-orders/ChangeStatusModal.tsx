import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { workOrdersApi, type WorkOrderStatus, STATUS_LABELS } from '../../../lib/api/workOrders'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../lib/api/client'

const TRANSITIONS: Record<WorkOrderStatus, WorkOrderStatus[]> = {
  pending: ['in_progress', 'cancelled'],
  in_progress: ['waiting_parts', 'completed', 'cancelled'],
  waiting_parts: ['in_progress', 'cancelled'],
  completed: ['delivered', 'cancelled'],
  delivered: [],
  cancelled: [],
}

const schema = z.object({
  toStatus: z.string().min(1, 'Seleccione un estado'),
  note: z.string().trim().max(500).optional(),
  cancelReason: z.string().trim().max(500).optional(),
})

type FormData = z.infer<typeof schema>

interface Props {
  orderId: string
  currentStatus: WorkOrderStatus
  onSuccess: () => void
  onClose: () => void
}

const inputCls = 'w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500'

export function ChangeStatusModal({ orderId, currentStatus, onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)

  const transitions = TRANSITIONS[currentStatus]

  const { register, handleSubmit, watch, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { toStatus: transitions[0] ?? '', note: '', cancelReason: '' },
  })

  const toStatus = watch('toStatus') as WorkOrderStatus

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      await workOrdersApi.changeStatus(orderId, {
        toStatus: data.toStatus as WorkOrderStatus,
        note: data.note || null,
        cancelReason: toStatus === 'cancelled' ? (data.cancelReason || null) : null,
      })
      onSuccess()
    } catch (e) {
      setServerError(e instanceof ApiError ? e.detail : 'Error inesperado')
    }
  }

  if (transitions.length === 0) return null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-md rounded-xl bg-white p-6 shadow-xl">
        <h2 className="mb-4 text-lg font-semibold">Cambiar estado</h2>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">

          <div>
            <label className="mb-1 block text-sm font-medium">Nuevo estado *</label>
            <select {...register('toStatus')} className={inputCls}>
              {transitions.map(s => (
                <option key={s} value={s}>{STATUS_LABELS[s]}</option>
              ))}
            </select>
            {errors.toStatus && <p className="mt-1 text-xs text-red-600">{errors.toStatus.message}</p>}
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">Nota (opcional)</label>
            <textarea {...register('note')} rows={2} className={inputCls} placeholder="Comentario sobre el cambio…" />
          </div>

          {toStatus === 'cancelled' && (
            <div>
              <label className="mb-1 block text-sm font-medium">Motivo de cancelación</label>
              <input {...register('cancelReason')} className={inputCls} placeholder="Motivo…" />
            </div>
          )}

          {serverError && (
            <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
          )}

          <div className="flex justify-end gap-3 pt-2">
            <Button type="button" variant="secondary" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={isSubmitting}>Confirmar</Button>
          </div>
        </form>
      </div>
    </div>
  )
}
