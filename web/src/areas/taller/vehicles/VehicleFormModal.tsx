import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery } from '@tanstack/react-query'
import { vehiclesApi, type FuelType, type VehicleUsageProfile, type VehicleDto, type VehicleUpsertRequest } from '../../../lib/api/vehicles'
import { customersApi } from '../../../lib/api/customers'
import { Button } from '../../../components/ui/Button'
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

const inputCls = 'w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500'

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
}

export function VehicleFormModal({ vehicle, onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)
  const isEdit = !!vehicle

  const { data: customersPage } = useQuery({
    queryKey: ['customers-picker'],
    queryFn: () => customersApi.search(null, 1, 100),
  })

  const { register, handleSubmit, setValue, formState: { errors, isSubmitting } } = useForm<FormInput, unknown, FormData>({
    resolver: zodResolver(schema),
    defaultValues: vehicle
      ? toFormData(vehicle)
      : { odometerKm: 0, customerId: '' },
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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-lg overflow-y-auto rounded-xl bg-white p-6 shadow-xl">
        <h2 className="mb-5 text-lg font-semibold">
          {isEdit ? 'Editar vehículo' : 'Nuevo vehículo'}
        </h2>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium">Patente *</label>
              <input
                {...register('plate')}
                className={inputCls}
                placeholder="ABC123"
                style={{ textTransform: 'uppercase' }}
                onChange={e => setValue('plate', e.target.value.toUpperCase().replace(/[\s-]/g, ''))}
              />
              {errors.plate && <p className="mt-1 text-xs text-red-600">{errors.plate.message}</p>}
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">Cliente *</label>
              <select {...register('customerId')} className={inputCls}>
                <option value="">Seleccioná un cliente</option>
                {customersPage?.items.map(c => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
              {errors.customerId && <p className="mt-1 text-xs text-red-600">{errors.customerId.message}</p>}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium">Marca</label>
              <input {...register('brand')} className={inputCls} placeholder="Toyota" />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">Modelo</label>
              <input {...register('model')} className={inputCls} placeholder="Corolla" />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium">Versión</label>
              <input {...register('version')} className={inputCls} placeholder="XEI CVT" />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">Color</label>
              <input {...register('color')} className={inputCls} placeholder="Blanco" />
            </div>
          </div>

          <div className="grid grid-cols-3 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium">Año</label>
              <input type="number" {...register('year')} className={inputCls} placeholder="2020" min={1900} max={new Date().getFullYear() + 1} />
              {errors.year && <p className="mt-1 text-xs text-red-600">{errors.year.message}</p>}
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">Combustible</label>
              <select {...register('fuel')} className={inputCls}>
                <option value="">Sin especificar</option>
                {FUEL_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">Km actuales *</label>
              <input type="number" {...register('odometerKm')} className={inputCls} placeholder="0" min={0} />
              {errors.odometerKm && <p className="mt-1 text-xs text-red-600">{errors.odometerKm.message}</p>}
            </div>
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">VIN / Nro. de chasis</label>
            <input {...register('vin')} className={inputCls} placeholder="1HGBH41JXMN109186" maxLength={17} />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium">Perfil de uso</label>
              <select {...register('usageProfile')} className={inputCls}>
                <option value="">Sin especificar</option>
                {USAGE_PROFILE_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">Nro. de motor</label>
              <input {...register('engineNumber')} className={inputCls} placeholder="ABC123456" maxLength={30} />
            </div>
          </div>

          {serverError && (
            <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
          )}

          <div className="flex justify-end gap-3 pt-2">
            <Button type="button" variant="secondary" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={isSubmitting}>Guardar</Button>
          </div>
        </form>
      </div>
    </div>
  )
}
