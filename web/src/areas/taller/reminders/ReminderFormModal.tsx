import { dateOnlyToUtcIso } from '../../../lib/dates'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { remindersApi } from '../../../lib/api/reminders'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect, FormTextarea } from '../../../components/ui/FormField'
import { VehiclePicker } from '../../../components/ui/EntityPickers'
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
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { kind: 'service_due' },
  })

  const vehicleId = watch('vehicleId')

  useEffect(() => {
    if (existing) {
      reset({
        vehicleId: existing.vehicleId,
        kind: existing.kind,
        title: existing.title,
        notes: existing.notes ?? '',
        dueDate: existing.dueDate
          ? existing.dueDate.slice(0, 10)
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
        dueDate: values.dueDate ? dateOnlyToUtcIso(values.dueDate) : null,
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
        dueDate: values.dueDate ? dateOnlyToUtcIso(values.dueDate) : null,
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
        <VehiclePicker
          value={vehicleId ?? ''}
          requiredMark
          error={errors.vehicleId?.message}
          onChange={id => setValue('vehicleId', id, { shouldValidate: !!id, shouldDirty: true })}
        />

        <FormSelect label="Tipo" {...register('kind')}>
          <option value="service_due">Service / Mantenimiento</option>
          <option value="inspection_due">VTV / Inspección</option>
          <option value="insurance_due">Seguro</option>
          <option value="other_due">Otro vencimiento</option>
        </FormSelect>

        <FormField label="Título" placeholder="Ej. Cambio de aceite" error={errors.title?.message} {...register('title')} />

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Fecha límite (opcional)" type="date" error={errors.dueDate?.message} {...register('dueDate')} />
          <FormField
            label="Km límite (opcional)"
            type="number"
            min={0}
            placeholder="Ej. 50000"
            inputClassName="tabular-nums"
            error={errors.dueKm?.message}
            // vacío -> undefined (valueAsNumber daba NaN y bloqueaba el envío sin mostrar error)
            {...register('dueKm', { setValueAs: v => (v === '' || v == null || Number.isNaN(Number(v)) ? undefined : Number(v)) })}
          />
        </div>

        <FormTextarea label="Notas (opcional)" rows={2} placeholder="Observaciones adicionales" {...register('notes')} />

        <div className="sticky bottom-0 -mx-5 -mb-5 mt-2 flex justify-end gap-2 border-t border-tc-border bg-white px-5 py-3">
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
