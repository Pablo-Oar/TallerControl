import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { workOrdersApi, type WorkOrderStatus, STATUS_LABELS, WORK_ORDER_TRANSITIONS } from '../../../lib/api/workOrders'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect, FormTextarea } from '../../../components/ui/FormField'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'

const schema = z.object({
  toStatus: z.string().min(1, 'Seleccioná un estado'),
  note: z.string().trim().max(500).optional(),
  cancelReason: z.string().trim().max(500).optional(),
})

type FormData = z.infer<typeof schema>


interface Props {
  orderId: string
  currentStatus: WorkOrderStatus
  /** Estado preseleccionado (p. ej. "cancelled" cuando se delega desde el badge editable). */
  initialStatus?: WorkOrderStatus
  onSuccess: () => void
  onClose: () => void
}

export function ChangeStatusModal({ orderId, currentStatus, initialStatus, onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)
  const transitions = WORK_ORDER_TRANSITIONS[currentStatus]

  const { register, handleSubmit, watch, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      toStatus: initialStatus && transitions.includes(initialStatus) ? initialStatus : (transitions[0] ?? ''),
      note: '',
      cancelReason: '',
    },
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
        <FormSelect label="Nuevo estado" requiredMark error={errors.toStatus?.message} {...register('toStatus')}>
          {transitions.map(s => (
            <option key={s} value={s}>{STATUS_LABELS[s]}</option>
          ))}
        </FormSelect>

        <FormTextarea label="Nota (opcional)" rows={2} placeholder="Comentario sobre el cambio…" {...register('note')} />

        {toStatus === 'cancelled' && (
          <FormField label="Motivo de cancelación" placeholder="Motivo…" {...register('cancelReason')} />
        )}

        {serverError && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
        )}

        <div className="sticky bottom-0 -mx-5 -mb-5 mt-2 flex justify-end gap-3 border-t border-tc-border bg-white px-5 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] shadow-[0_-6px_8px_-6px_rgb(0_0_0/0.12)]">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
          <Button type="submit" size="sm" loading={isSubmitting}>Confirmar</Button>
        </div>
      </form>
    </Modal>
  )
}
