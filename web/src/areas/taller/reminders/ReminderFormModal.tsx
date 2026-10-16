import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { remindersApi } from '../../../lib/api/reminders'
import { Button } from '../../../components/ui/Button'
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
      if (e instanceof ApiError)
        setError('root', { message: e.detail })
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
      if (e instanceof ApiError)
        setError('root', { message: e.detail })
    },
  })

  const onSubmit = handleSubmit((values) => {
    if (isEdit) updateMut.mutate(values)
    else createMut.mutate(values)
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
        <h2 className="mb-4 text-lg font-semibold">
          {isEdit ? 'Editar recordatorio' : 'Nuevo recordatorio'}
        </h2>

        {errors.root && (
          <div className="mb-3 rounded border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {errors.root.message}
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium">ID del vehículo</label>
            <input
              {...register('vehicleId')}
              className="w-full rounded border border-slate-300 px-3 py-2 font-mono text-xs"
              placeholder="UUID del vehículo"
            />
            {errors.vehicleId && (
              <p className="mt-1 text-xs text-red-600">{errors.vehicleId.message}</p>
            )}
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">Tipo</label>
            <select {...register('kind')} className="w-full rounded border border-slate-300 px-3 py-2 text-sm">
              <option value="service_due">Service / Mantenimiento</option>
              <option value="inspection_due">VTV / Inspección</option>
              <option value="insurance_due">Seguro</option>
              <option value="other_due">Otro vencimiento</option>
            </select>
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">Título</label>
            <input
              {...register('title')}
              className="w-full rounded border border-slate-300 px-3 py-2 text-sm"
              placeholder="Ej. Cambio de aceite"
            />
            {errors.title && (
              <p className="mt-1 text-xs text-red-600">{errors.title.message}</p>
            )}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium">
                Fecha límite <span className="font-normal text-slate-400">(opcional)</span>
              </label>
              <input
                type="date"
                {...register('dueDate')}
                className="w-full rounded border border-slate-300 px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">
                Km límite <span className="font-normal text-slate-400">(opcional)</span>
              </label>
              <input
                type="number"
                min={0}
                {...register('dueKm', { valueAsNumber: true })}
                className="w-full rounded border border-slate-300 px-3 py-2 text-sm"
                placeholder="Ej. 50000"
              />
            </div>
          </div>
          {errors.dueDate && (
            <p className="text-xs text-red-600">{errors.dueDate.message}</p>
          )}

          <div>
            <label className="mb-1 block text-sm font-medium">
              Notas <span className="font-normal text-slate-400">(opcional)</span>
            </label>
            <textarea
              rows={2}
              {...register('notes')}
              className="w-full rounded border border-slate-300 px-3 py-2 text-sm"
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
      </div>
    </div>
  )
}
