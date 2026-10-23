import { useState } from 'react'
import { useDefaultVat } from '../../../lib/auth/defaults'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { Resolver } from 'react-hook-form'
import { catalogApi, type CatalogItemDto } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect, FormTextarea } from '../../../components/ui/FormField'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'
import { useCan } from '../../../lib/auth/permissions'


const schema = z.object({
  kind: z.enum(['service', 'product']),
  sku: z.string().trim().max(50).optional(),
  name: z.string().trim().min(1, 'Requerido').max(200),
  description: z.string().trim().max(1000).optional(),
  netPrice: z.number().min(0, 'El precio no puede ser negativo'),
  vatRate: z.number().min(0).max(100),
  cost: z.string().optional(),
  trackStock: z.boolean(),
  initialStockQty: z.string().optional(),
  minStockQty: z.string().optional(),
  maxStockQty: z.string().optional(),
  durationMinutes: z.string().optional(),
  bufferMinutes: z.string().optional(),
  isActive: z.boolean(),
})

type FormData = z.infer<typeof schema>

// @hookform/resolvers v5 only passes registered field values to the Zod resolver.
// In edit mode the `kind` select is not rendered so we inject it from the item prop.
function makeResolver(item: CatalogItemDto | undefined): Resolver<FormData> {
  const base = zodResolver(schema)
  return (values, ctx, opts) => {
    const patched = item ? { ...values, kind: item.kind } : values
    return base(patched as FormData, ctx, opts)
  }
}

function toFormData(dto: CatalogItemDto): FormData {
  return {
    kind: dto.kind,
    sku: dto.sku ?? '',
    name: dto.name,
    description: dto.description ?? '',
    netPrice: dto.netPrice,
    vatRate: dto.vatRate,
    cost: dto.cost != null ? String(dto.cost) : '',
    trackStock: dto.trackStock,
    minStockQty: dto.stockMin != null ? String(dto.stockMin) : '',
    maxStockQty: dto.stockMax != null ? String(dto.stockMax) : '',
    durationMinutes: dto.durationMinutes != null ? String(dto.durationMinutes) : '',
    bufferMinutes: dto.bufferMinutes != null ? String(dto.bufferMinutes) : '',
    isActive: dto.isActive,
  }
}

interface Props {
  item?: CatalogItemDto
  onSuccess: (dto: CatalogItemDto) => void
  onClose: () => void
}

export function CatalogFormModal({ item, onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)
  const isEdit = !!item
  const canCost = useCan()('costs.view')
  const defaultVat = useDefaultVat()

  const { register, handleSubmit, watch, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: makeResolver(item),
    defaultValues: item
      ? toFormData(item)
      : { kind: 'service' as const, vatRate: defaultVat, trackStock: false, isActive: true },
  })

  const kind = watch('kind')
  const trackStock = watch('trackStock')

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      const numOrNull = (v: string | undefined) =>
        !v || v.trim() === '' ? null : Number(v)

      if (isEdit) {
        const result = await catalogApi.update(item.id, {
          name: data.name,
          sku: item.sku,
          cost: canCost ? numOrNull(data.cost) : item.cost ?? null,
          description: data.description || null,
          netPrice: data.netPrice,
          vatRate: data.vatRate,
          // Solo se manda si cambió: activar el control de stock puede traer existencias de partida.
          trackStock: item.kind === 'product' && data.trackStock !== item.trackStock ? data.trackStock : undefined,
          initialStockQty: !item.trackStock && data.trackStock ? numOrNull(data.initialStockQty) : null,
          stockMin: numOrNull(data.minStockQty),
          stockMax: numOrNull(data.maxStockQty),
          durationMinutes: item.kind === 'service' ? numOrNull(data.durationMinutes) : null,
          bufferMinutes: item.kind === 'service' ? numOrNull(data.bufferMinutes) : null,
          isActive: data.isActive,
        }, item.version)
        onSuccess(result)
      } else {
        const result = await catalogApi.create({
          kind: data.kind,
          sku: data.sku || null,
          name: data.name,
          description: data.description || null,
          netPrice: data.netPrice,
          vatRate: data.vatRate,
          cost: canCost ? numOrNull(data.cost) : null,
          trackStock: data.trackStock,
          initialStockQty: data.kind === 'product' && data.trackStock ? numOrNull(data.initialStockQty) : null,
          stockMin: numOrNull(data.minStockQty),
          stockMax: numOrNull(data.maxStockQty),
          durationMinutes: data.kind === 'service' ? numOrNull(data.durationMinutes) : null,
          bufferMinutes: data.kind === 'service' ? numOrNull(data.bufferMinutes) : null,
        })
        onSuccess(result)
      }
    } catch (e) {
      setServerError(e instanceof ApiError ? e.detail : 'Error inesperado')
    }
  }

  return (
    <Modal title={isEdit ? 'Editar ítem' : 'Nuevo ítem de catálogo'} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">

        {!isEdit && (
          <FormSelect label="Tipo" requiredMark {...register('kind')}>
            <option value="service">Servicio</option>
            <option value="product">Producto</option>
          </FormSelect>
        )}

        <FormField
          label="Nombre"
          requiredMark
          placeholder="Ej. Cambio de aceite"
          error={errors.name?.message}
          {...register('name')}
        />

        {!isEdit && <FormField label="SKU" placeholder="Ej. FIL-001" {...register('sku')} />}

        <FormTextarea label="Descripción" rows={2} placeholder="Descripción opcional…" {...register('description')} />

        <div className="grid grid-cols-2 gap-4">
          <FormField
            label="Precio neto"
            requiredMark
            type="number"
            min={0}
            step="1"
            placeholder="0.00"
            inputClassName="tabular-nums"
            error={errors.netPrice?.message}
            {...register('netPrice', { valueAsNumber: true })}
          />
          <FormField
            label="IVA %"
            type="number"
            min={0}
            max={100}
            step="1"
            placeholder="21"
            inputClassName="tabular-nums"
            {...register('vatRate', { valueAsNumber: true })}
          />
        </div>

        {(isEdit ? item.kind : kind) === 'service' && (
          <div className="grid grid-cols-2 gap-4">
            <FormField
              label="Duración (min)"
              type="number"
              min={1}
              step="5"
              inputMode="numeric"
              placeholder="45"
              hint="La agenda reserva este tiempo."
              inputClassName="tabular-nums"
              {...register('durationMinutes')}
            />
            <FormField
              label="Margen (min)"
              type="number"
              min={0}
              step="5"
              inputMode="numeric"
              placeholder="15"
              inputClassName="tabular-nums"
              {...register('bufferMinutes')}
            />
          </div>
        )}

        {canCost && (
          <FormField
            label="Costo (sin IVA)"
            type="number"
            min={0}
            step="0.01"
            placeholder="—"
            hint="Lo que te cuesta a vos. Solo lo ven quienes tienen permiso de costos."
            inputClassName="tabular-nums"
            {...register('cost')}
          />
        )}

        {(kind === 'product' || (isEdit && item.kind === 'product')) && (
          <label className="flex items-center gap-2 text-sm text-tc-text-secondary">
            <input type="checkbox" {...register('trackStock')} className="h-4 w-4" />
            Controlar stock
          </label>
        )}

        {trackStock && !(isEdit && item.trackStock) && (
          <FormField
            label="Stock inicial"
            type="number"
            step="1"
            placeholder="0"
            hint="Las existencias de partida: quedan registradas como un movimiento."
            inputClassName="tabular-nums"
            {...register('initialStockQty')}
          />
        )}

        {trackStock && (
          <div className="grid grid-cols-2 gap-4">
            <FormField label="Stock mínimo" type="number" min={0} step="1" placeholder="—" inputClassName="tabular-nums" {...register('minStockQty')} />
            <FormField label="Stock máximo" type="number" min={0} step="1" placeholder="—" inputClassName="tabular-nums" {...register('maxStockQty')} />
          </div>
        )}

        {isEdit && (
          <label className="flex items-center gap-2 text-sm text-tc-text-secondary">
            <input type="checkbox" {...register('isActive')} className="h-4 w-4" />
            Activo
          </label>
        )}

        {serverError && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
        )}

        <div className="flex justify-end gap-3 pt-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
          <Button type="submit" size="sm" loading={isSubmitting}>Guardar</Button>
        </div>
      </form>
    </Modal>
  )
}
