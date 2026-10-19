import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { workOrdersApi, type WorkOrderStatus, STATUS_LABELS } from '../../../lib/api/workOrders'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
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
  toStatus: z.string().min(1, 'Seleccioná un estado'),
  note: z.string().trim().max(500).optional(),
  cancelReason: z.string().trim().max(500).optional(),
})

type FormData = z.infer<typeof schema>

const inputCls = 'w-full border border-tc-border bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors'
const labelCls = 'mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500'

interface Props {
  orderId: string
  currentStatus: WorkOrderStatus
  onSuccess: () => void
  onClose: () => void
}

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
    <Modal title="Cambiar estado" onClose={onClose} className="max-w-md">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <label htmlFor="cs-status" className={labelCls}>Nuevo estado *</label>
          <select id="cs-status" {...register('toStatus')} className={inputCls}>
            {transitions.map(s => (
              <option key={s} value={s}>{STATUS_LABELS[s]}</option>
            ))}
          </select>
          {errors.toStatus && <p className="mt-1 text-xs text-tc-stamp-red">{errors.toStatus.message}</p>}
        </div>

        <div>
          <label htmlFor="cs-note" className={labelCls}>Nota (opcional)</label>
          <textarea id="cs-note" {...register('note')} rows={2} className={inputCls} placeholder="Comentario sobre el cambio…" />
        </div>

        {toStatus === 'cancelled' && (
          <div>
            <label htmlFor="cs-cancel-reason" className={labelCls}>Motivo de cancelación</label>
            <input id="cs-cancel-reason" {...register('cancelReason')} className={inputCls} placeholder="Motivo…" />
          </div>
        )}

        {serverError && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
        )}

        <div className="flex justify-end gap-3 pt-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
          <Button type="submit" size="sm" loading={isSubmitting}>Confirmar</Button>
        </div>
      </form>
    </Modal>
  )
}
