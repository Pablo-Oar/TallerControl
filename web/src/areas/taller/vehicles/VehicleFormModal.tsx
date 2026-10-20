import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { vehiclesApi, type FuelType, type VehicleUsageProfile, type VehicleDto, type VehicleUpsertRequest } from '../../../lib/api/vehicles'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect } from '../../../components/ui/FormField'
import { CustomerPicker } from '../../../components/ui/EntityPickers'
import { EditDialog } from '../../../components/ui/EditDialog'
import { ApiError } from '../../../lib/api/client'

const USAGE_PROFILE_OPTIONS: { value: VehicleUsageProfile; label: string }[] = [
  { value: 'regular', label: 'Regular' },
  { value: 'low_usage', label: 'Uso reducido' },
  { value: 'commercial', label: 'Comercial / Alta carga' },
]

const FUEL_OPTIONS: { value: FuelType; label: string }[] = [
  { value: 'gasoline', label: 'Nafta' },
  { value: 'diesel', label: 'Diésel' },
  { value: 'gas', label: 'GNC' },
  { value: 'hybrid', label: 'Híbrido' },
  { value: 'electric', label: 'Eléctrico' },
  { value: 'other', label: 'Otro' },
]


const schema = z.object({
  plate: z.string().trim().min(5, 'Mínimo 5 caracteres').max(10, 'Máximo 10 caracteres'),
  customerId: z.string().min(1, 'Seleccioná un cliente'),
  brand: z.string().trim().max(60).optional(),
  model: z.string().trim().max(60).optional(),
  version: z.string().trim().max(60).optional(),
  color: z.string().trim().max(30).optional(),
  year: z.coerce.number().int().min(1900).max(new Date().getFullYear() + 1).optional().or(z.literal('')),
  vin: z.string().trim().max(17).optional(),
  fuel: z.string().optional(),
  odometerKm: z.coerce.number().int().min(0, 'El km no puede ser negativo'),
  usageProfile: z.string().optional(),
  engineNumber: z.string().trim().max(30).optional(),
})

type FormInput = z.input<typeof schema>
type FormData = z.output<typeof schema>

function toFormData(dto: VehicleDto): FormInput {
  return {
    plate: dto.plate,
    customerId: dto.customerId,
    brand: dto.brand ?? '',
    model: dto.model ?? '',
    version: dto.versionName ?? '',
    color: dto.color ?? '',
    year: dto.year ?? '',
    vin: dto.vin ?? '',
    fuel: dto.fuel ?? '',
    odometerKm: dto.odometerKm,
    usageProfile: dto.usageProfile ?? '',
    engineNumber: dto.engineNumber ?? '',
  }
}

interface Props {
  vehicle?: VehicleDto
  onSuccess: (dto: VehicleDto) => void
  onClose: () => void
  onDelete?: () => void
}

export function VehicleFormModal({ vehicle, onSuccess, onClose, onDelete }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)
  const [deleting, setDeleting] = useState(false)
  const [deleteReason, setDeleteReason] = useState('')
  const [deleteLoading, setDeleteLoading] = useState(false)
  const isEdit = !!vehicle

  const { register, handleSubmit, setValue, watch, formState: { errors, isSubmitting, isDirty } } = useForm<FormInput, unknown, FormData>({
    resolver: zodResolver(schema),
    defaultValues: vehicle ? toFormData(vehicle) : { odometerKm: 0, customerId: '' },
  })

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      const body: VehicleUpsertRequest = {
        plate: data.plate.toUpperCase().replace(/[\s-]/g, ''),
        customerId: data.customerId,
        brand: (data.brand as string) || null,
        model: (data.model as string) || null,
        version: (data.version as string) || null,
        color: (data.color as string) || null,
        year: typeof data.year === 'number' ? data.year : null,
        vin: (data.vin as string) || null,
        fuel: ((data.fuel as string) || null) as FuelType | null,
        odometerKm: data.odometerKm as number,
        usageProfile: ((data.usageProfile as string) || null) as VehicleUsageProfile | null,
        engineNumber: (data.engineNumber as string) || null,
      }
      const result = isEdit
        ? await vehiclesApi.update(vehicle.id, body, vehicle.version)
        : await vehiclesApi.create(body)
      onSuccess(result)
    } catch (e) {
      setServerError(e instanceof ApiError ? e.detail : 'Error inesperado')
    }
  }

  return (
    <EditDialog
      title={isEdit ? 'Editar vehículo' : 'Nuevo vehículo'}
      onClose={onClose}
      onSubmit={handleSubmit(onSubmit)}
      dirty={isDirty}
      saving={isSubmitting}
      error={serverError}
      requireChanges={isEdit}
      className="max-w-lg"
      footerStart={
        isEdit && onDelete && !deleting ? (
          <Button type="button" variant="danger" size="sm" onClick={() => setDeleting(true)}>
            Eliminar
          </Button>
        ) : undefined
      }
    >
        <div className="grid grid-cols-2 gap-4">
          <FormField
            label="Patente"
            requiredMark
            placeholder="ABC123"
            inputClassName="font-mono tracking-widest uppercase"
            error={errors.plate?.message}
            {...register('plate')}
            onChange={e => setValue('plate', e.target.value.toUpperCase().replace(/[\s-]/g, ''))}
          />
          {/* Buscador (no un select con la primera página): un taller puede tener cientos de clientes. */}
          <CustomerPicker
            value={watch('customerId') ?? ''}
            requiredMark
            error={errors.customerId?.message}
            onChange={id => setValue('customerId', id, { shouldValidate: !!id, shouldDirty: true })}
          />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <FormField label="Marca" placeholder="Toyota" {...register('brand')} />
          <FormField label="Modelo" placeholder="Corolla" {...register('model')} />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <FormField label="Versión" placeholder="XEI CVT" {...register('version')} />
          <FormField label="Color" placeholder="Blanco" {...register('color')} />
        </div>

        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
          <FormField
            label="Año"
            type="number"
            placeholder="2020"
            min={1900}
            max={new Date().getFullYear() + 1}
            error={errors.year?.message}
            {...register('year')}
          />
          <FormSelect label="Combustible" {...register('fuel')}>
            <option value="">Sin especificar</option>
            {FUEL_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </FormSelect>
          <FormField
            label="Km actuales"
            requiredMark
            type="number"
            placeholder="0"
            min={0}
            inputClassName="tabular-nums"
            error={errors.odometerKm?.message}
            {...register('odometerKm')}
          />
        </div>

        <FormField
          label="VIN / Nro. de chasis"
          placeholder="1HGBH41JXMN109186"
          maxLength={17}
          inputClassName="font-mono"
          {...register('vin')}
        />

        <div className="grid grid-cols-2 gap-4">
          <FormSelect label="Perfil de uso" {...register('usageProfile')}>
            <option value="">Sin especificar</option>
            {USAGE_PROFILE_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </FormSelect>
          <FormField
            label="Nro. de motor"
            placeholder="ABC123456"
            maxLength={30}
            inputClassName="font-mono"
            {...register('engineNumber')}
          />
        </div>


        {deleting && (
          <div className="space-y-2 border border-red-200 bg-red-50 p-3">
            <p className="text-xs font-bold tracking-wider uppercase text-red-700">Confirmar eliminación</p>
            <FormField
              label="Motivo de eliminación (requerido)"
              value={deleteReason}
              onChange={e => setDeleteReason(e.target.value)}
              autoFocus
            />
            <div className="flex gap-2">
              <Button
                type="button"
                variant="danger"
                size="sm"
                loading={deleteLoading}
                disabled={!deleteReason.trim()}
                onClick={async () => {
                  setDeleteLoading(true)
                  try {
                    await vehiclesApi.delete(vehicle!.id, deleteReason.trim())
                    onDelete?.()
                    onClose()
                  } catch (e) {
                    setServerError(e instanceof ApiError ? e.detail : 'Error al eliminar')
                    setDeleting(false)
                  } finally {
                    setDeleteLoading(false)
                  }
                }}
              >
                Eliminar definitivamente
              </Button>
              <Button type="button" variant="secondary" size="sm" onClick={() => setDeleting(false)}>Cancelar</Button>
            </div>
          </div>
        )}
    </EditDialog>
  )
}
