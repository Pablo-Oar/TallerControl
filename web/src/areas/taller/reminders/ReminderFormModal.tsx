import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { remindersApi } from '../../../lib/api/reminders'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'

const schema = z
  .object({
    vehicleId: z.string().uuid('ID de vehículo inválido'),
    kind: z.enum(['service_due', 'inspection_due', 'insurance_due', 'other_due'] as const),
    title: z.string().min(1, 'El título es obligatorio').max(200),
    notes: z.string().max(1000).optional(),
    dueDate: z.string().optional(),
    dueKm: z.number().int().min(0).optional(),
  })
  .refine((d) => d.dueDate || d.dueKm != null, {
    message: 'Ingresá una fecha límite, un km límite, o ambos.',
    path: ['dueDate'],
  })

type FormValues = z.infer<typeof schema>

const inputCls =
  'w-full border border-tc-border bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors'
const labelCls = 'mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500'

interface Props {
  id: string | null
  onClose: () => void
  onSaved: () => void
}

export function ReminderFormModal({ id, onClose, onSaved }: Props) {
  const isEdit = id !== null

  const { data: existing } = useQuery({
    queryKey: ['reminder', id],
    queryFn: () => remindersApi.get(id!),
    enabled: isEdit,
  })

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { kind: 'service_due' },
  })

  useEffect(() => {
    if (existing) {
      reset({
        vehicleId: existing.vehicleId,
        kind: existing.kind,
        title: existing.title,
        notes: existing.notes ?? '',
        dueDate: existing.dueDate
          ? new Date(existing.dueDate).toISOString().slice(0, 10)
          : '',
        dueKm: existing.dueKm ?? undefined,
      })
    }
  }, [existing, reset])

  const createMut = useMutation({
    mutationFn: (values: FormValues) =>
      remindersApi.create({
        vehicleId: values.vehicleId,
        kind: values.kind,
        title: values.title,
        notes: values.notes || null,
        dueDate: values.dueDate ? new Date(values.dueDate).toISOString() : null,
        dueKm: values.dueKm ?? null,
      }),
    onSuccess: onSaved,
    onError: (e) => {
      if (e instanceof ApiError) setError('root', { message: e.detail })
    },
  })

  const updateMut = useMutation({
    mutationFn: (values: FormValues) =>
      remindersApi.update(id!, existing!.version, {
        vehicleId: values.vehicleId,
        kind: values.kind,
        title: values.title,
        notes: values.notes || null,
        dueDate: values.dueDate ? new Date(values.dueDate).toISOString() : null,
        dueKm: values.dueKm ?? null,
      }),
    onSuccess: onSaved,
    onError: (e) => {
      if (e instanceof ApiError) setError('root', { message: e.detail })
    },
  })

  const onSubmit = handleSubmit((values) => {
    if (isEdit) updateMut.mutate(values)
    else createMut.mutate(values)
  })

  return (
    <Modal
      title={isEdit ? 'Editar recordatorio' : 'Nuevo recordatorio'}
      onClose={onClose}
      className="max-w-md"
    >
      {errors.root && (
        <div className="mb-3 border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {errors.root.message}
        </div>
      )}

      <form onSubmit={onSubmit} className="space-y-4">
        <div>
          <label className={labelCls}>ID del vehículo</label>
          <input
            {...register('vehicleId')}
            className={`${inputCls} font-mono text-xs`}
            placeholder="UUID del vehículo"
          />
          {errors.vehicleId && (
            <p className="mt-1 text-xs text-tc-stamp-red">{errors.vehicleId.message}</p>
          )}
        </div>

        <div>
          <label className={labelCls}>Tipo</label>
          <select
            {...register('kind')}
            className="w-full border border-tc-border bg-white px-3 py-2 text-sm outline-none focus:border-tc-border-mid transition-colors"
          >
            <option value="service_due">Service / Mantenimiento</option>
            <option value="inspection_due">VTV / Inspección</option>
            <option value="insurance_due">Seguro</option>
            <option value="other_due">Otro vencimiento</option>
          </select>
        </div>

        <div>
          <label className={labelCls}>Título</label>
          <input
            {...register('title')}
            className={inputCls}
            placeholder="Ej. Cambio de aceite"
          />
          {errors.title && (
            <p className="mt-1 text-xs text-tc-stamp-red">{errors.title.message}</p>
          )}
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelCls}>
              Fecha límite <span className="font-normal normal-case text-slate-400">(opcional)</span>
            </label>
            <input
              type="date"
              {...register('dueDate')}
              className={inputCls}
            />
          </div>
          <div>
            <label className={labelCls}>
              Km límite <span className="font-normal normal-case text-slate-400">(opcional)</span>
            </label>
            <input
              type="number"
              min={0}
              {...register('dueKm', { valueAsNumber: true })}
              className={inputCls}
              placeholder="Ej. 50000"
            />
          </div>
        </div>
        {errors.dueDate && (
          <p className="text-xs text-tc-stamp-red">{errors.dueDate.message}</p>
        )}

        <div>
          <label className={labelCls}>
            Notas <span className="font-normal normal-case text-slate-400">(opcional)</span>
          </label>
          <textarea
            rows={2}
            {...register('notes')}
            className={inputCls}
            placeholder="Observaciones adicionales"
          />
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <Button variant="secondary" type="button" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" loading={isSubmitting}>
            {isEdit ? 'Guardar cambios' : 'Crear recordatorio'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
