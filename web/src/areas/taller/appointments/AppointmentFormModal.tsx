import { useEffect } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { appointmentsApi, type AppointmentDto } from '../../../lib/api/appointments'
import { customersApi } from '../../../lib/api/customers'
import { catalogApi } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect, FormTextarea } from '../../../components/ui/FormField'
import { CustomerPicker } from '../../../components/ui/EntityPickers'
import { Modal } from '../../../components/ui/Modal'
import { labelCls } from '../../../lib/styles'


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

  const { register, handleSubmit, control, reset, watch, setValue, formState: { errors, isSubmitting } } = useForm<FormValues>({
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

  const customerId = watch('customerId')

  const { data: customerDetail } = useQuery({
    queryKey: ['customer', customerId],
    queryFn: () => customersApi.get(customerId),
    enabled: !isEdit && !!customerId,
  })
  const { data: catalogPage } = useQuery({
    queryKey: ['catalog-services'],
    queryFn: () => catalogApi.search(null, 'service', true, 1, 100),
    enabled: !isEdit,
  })
  const vehicles = customerDetail?.vehicles ?? []

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
    <Modal title={isEdit ? 'Editar turno' : 'Nuevo turno'} onClose={onClose} className="max-w-lg">
      <form onSubmit={handleSubmit(v => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <>
            <CustomerPicker
              value={customerId}
              requiredMark
              error={errors.customerId?.message}
              onChange={id => {
                setValue('customerId', id, { shouldValidate: !!id })
                setValue('vehicleId', '')
              }}
            />
            {customerId && (
              vehicles.length === 0 && customerDetail ? (
                <p className="text-sm text-tc-text-muted">El cliente no tiene vehículos registrados.</p>
              ) : (
                <FormSelect
                  key={customerDetail?.id ?? 'loading'}
                  label="Vehículo"
                  requiredMark
                  error={errors.vehicleId?.message}
                  {...register('vehicleId')}
                >
                  <option value="">Seleccionar vehículo</option>
                  {vehicles.map(v => (
                    <option key={v.id} value={v.id}>
                      {[v.plate, v.brand, v.model].filter(Boolean).join(' · ')}
                    </option>
                  ))}
                </FormSelect>
              )
            )}
          </>
        )}

        <FormField
          label="Fecha y hora"
          requiredMark
          type="datetime-local"
          error={errors.scheduledAt?.message}
          {...register('scheduledAt')}
        />

        <FormTextarea label="Notas (opcional)" rows={2} {...register('notes')} />

        {!isEdit && (
          <div>
            <div className="mb-2 flex items-center justify-between">
              <span className={labelCls.replace('mb-1 ', '')}>Servicios</span>
              <button
                type="button"
                onClick={() => append({ catalogItemId: '', durationMinutes: 45, bufferMinutes: 15 })}
                className="text-xs font-bold tracking-wider uppercase text-tc-purple hover:opacity-70"
              >
                + Agregar
              </button>
            </div>
            <div className="space-y-2">
              {fields.map((field, index) => (
                <div key={field.id} className="border border-tc-border">
                  <div className="bg-tc-stub-bg border-b border-tc-border px-3 py-1 flex items-center justify-between">
                    <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">
                      Servicio {index + 1}
                    </span>
                    {fields.length > 1 && (
                      <button type="button" onClick={() => remove(index)} className="text-xs text-tc-stamp-red hover:opacity-70">
                        ×
                      </button>
                    )}
                  </div>
                  <div className="space-y-2 p-2">
                    <FormSelect
                      key={catalogPage ? 'loaded' : 'loading'}
                      label="Servicio del catálogo"
                      requiredMark
                      size="sm"
                      error={errors.services?.[index]?.catalogItemId?.message}
                      {...register(`services.${index}.catalogItemId`)}
                    >
                      <option value="">Seleccionar servicio…</option>
                      {catalogPage?.items.map(c => (
                        <option key={c.id} value={c.id}>{c.name}</option>
                      ))}
                    </FormSelect>
                    <div className="grid grid-cols-2 gap-2">
                      <FormField
                        label="Duración (minutos)"
                        size="sm"
                        type="number"
                        inputClassName="tabular-nums"
                        {...register(`services.${index}.durationMinutes`, { valueAsNumber: true })}
                      />
                      <FormField
                        label="Buffer (minutos)"
                        size="sm"
                        type="number"
                        inputClassName="tabular-nums"
                        {...register(`services.${index}.bufferMinutes`, { valueAsNumber: true })}
                      />
                    </div>
                  </div>
                </div>
              ))}
            </div>
            {errors.services && (
              <p className="mt-1 text-xs text-tc-stamp-red">
                {errors.services.message ?? errors.services.root?.message}
              </p>
            )}
          </div>
        )}

        {mutation.error && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {(mutation.error as Error).message}
          </p>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose} disabled={isSubmitting}>
            Cancelar
          </Button>
          <Button type="submit" size="sm" loading={isSubmitting || mutation.isPending}>
            {isEdit ? 'Guardar cambios' : 'Crear turno'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
