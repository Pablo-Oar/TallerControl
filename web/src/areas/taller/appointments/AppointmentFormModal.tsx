import { useEffect } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { appointmentsApi, type AppointmentDto } from '../../../lib/api/appointments'

const serviceSchema = z.object({
  catalogItemId: z.string().min(1, 'Requerido'),
  durationMinutes: z.number().int().min(1, 'Debe ser > 0'),
  bufferMinutes: z.number().int().min(0, 'No puede ser negativo'),
})

const schema = z.object({
  customerId: z.string().uuid('Cliente requerido'),
  vehicleId: z.string().uuid('Vehículo requerido'),
  scheduledAt: z.string().min(1, 'Fecha y hora requeridas'),
  mechanicMembershipId: z.string().uuid().nullable().optional(),
  notes: z.string().max(1000).nullable().optional(),
  services: z.array(serviceSchema).min(1, 'Agregá al menos un servicio'),
})

type FormValues = z.infer<typeof schema>

interface Props {
  editData?: AppointmentDto | null
  onClose: () => void
}

export function AppointmentFormModal({ editData, onClose }: Props) {
  const qc = useQueryClient()
  const isEdit = !!editData

  const { register, handleSubmit, control, reset, formState: { errors } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      customerId: '',
      vehicleId: '',
      scheduledAt: '',
      mechanicMembershipId: null,
      notes: null,
      services: [{ catalogItemId: '', durationMinutes: 45, bufferMinutes: 15 }],
    },
  })

  const { fields, append, remove } = useFieldArray({ control, name: 'services' })

  useEffect(() => {
    if (editData) {
      reset({
        customerId: editData.customerId,
        vehicleId: editData.vehicleId,
        scheduledAt: editData.scheduledAt.slice(0, 16),
        mechanicMembershipId: editData.mechanicMembershipId ?? null,
        notes: editData.notes ?? null,
        services: editData.services.map(s => ({
          catalogItemId: s.catalogItemId,
          durationMinutes: s.durationMinutes,
          bufferMinutes: s.bufferMinutes,
        })),
      })
    }
  }, [editData, reset])

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      if (isEdit && editData) {
        return appointmentsApi.update(editData.id, editData.version, {
          scheduledAt: new Date(values.scheduledAt).toISOString(),
          mechanicMembershipId: values.mechanicMembershipId ?? null,
          notes: values.notes ?? null,
        })
      }
      const resp = await appointmentsApi.create({
        customerId: values.customerId,
        vehicleId: values.vehicleId,
        mechanicMembershipId: values.mechanicMembershipId ?? null,
        scheduledAt: new Date(values.scheduledAt).toISOString(),
        notes: values.notes ?? null,
        services: values.services,
      })
      return resp.appointment
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['appointments'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-lg rounded-xl bg-white p-6 shadow-xl">
        <h2 className="mb-4 text-lg font-semibold">
          {isEdit ? 'Editar turno' : 'Nuevo turno'}
        </h2>

        <form onSubmit={handleSubmit(v => mutation.mutate(v))} className="space-y-4">
          {!isEdit && (
            <>
              <div>
                <label className="block text-sm font-medium">ID Cliente</label>
                <input {...register('customerId')} className="mt-1 w-full rounded border px-3 py-2 text-sm" placeholder="UUID del cliente" />
                {errors.customerId && <p className="mt-1 text-xs text-red-600">{errors.customerId.message}</p>}
              </div>
              <div>
                <label className="block text-sm font-medium">ID Vehículo</label>
                <input {...register('vehicleId')} className="mt-1 w-full rounded border px-3 py-2 text-sm" placeholder="UUID del vehículo" />
                {errors.vehicleId && <p className="mt-1 text-xs text-red-600">{errors.vehicleId.message}</p>}
              </div>
            </>
          )}

          <div>
            <label className="block text-sm font-medium">Fecha y hora</label>
            <input
              type="datetime-local"
              {...register('scheduledAt')}
              className="mt-1 w-full rounded border px-3 py-2 text-sm"
            />
            {errors.scheduledAt && <p className="mt-1 text-xs text-red-600">{errors.scheduledAt.message}</p>}
          </div>

          <div>
            <label className="block text-sm font-medium">Notas (opcional)</label>
            <textarea {...register('notes')} rows={2} className="mt-1 w-full rounded border px-3 py-2 text-sm" />
          </div>

          {!isEdit && (
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <label className="text-sm font-medium">Servicios</label>
                <button
                  type="button"
                  onClick={() => append({ catalogItemId: '', durationMinutes: 45, bufferMinutes: 15 })}
                  className="text-xs text-blue-600 underline"
                >
                  + Agregar
                </button>
              </div>
              {fields.map((field, index) => (
                <div key={field.id} className="flex items-start gap-2 rounded border p-2">
                  <div className="flex-1 space-y-1">
                    <input
                      {...register(`services.${index}.catalogItemId`)}
                      placeholder="UUID ítem catálogo"
                      className="w-full rounded border px-2 py-1 text-xs"
                    />
                    <div className="flex gap-2">
                      <input
                        type="number"
                        {...register(`services.${index}.durationMinutes`, { valueAsNumber: true })}
                        placeholder="Duración (min)"
                        className="w-1/2 rounded border px-2 py-1 text-xs"
                      />
                      <input
                        type="number"
                        {...register(`services.${index}.bufferMinutes`, { valueAsNumber: true })}
                        placeholder="Buffer (min)"
                        className="w-1/2 rounded border px-2 py-1 text-xs"
                      />
                    </div>
                  </div>
                  {fields.length > 1 && (
                    <button type="button" onClick={() => remove(index)} className="text-red-500 text-xs">×</button>
                  )}
                </div>
              ))}
              {errors.services && <p className="mt-1 text-xs text-red-600">{errors.services.message ?? errors.services.root?.message}</p>}
            </div>
          )}

          {mutation.error && (
            <p className="text-sm text-red-600">{(mutation.error as Error).message}</p>
          )}

          <div className="flex justify-end gap-2">
            <button type="button" onClick={onClose} className="rounded border px-4 py-2 text-sm">
              Cancelar
            </button>
            <button
              type="submit"
              disabled={mutation.isPending}
              className="rounded bg-blue-600 px-4 py-2 text-sm text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {mutation.isPending ? 'Guardando…' : 'Guardar'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
