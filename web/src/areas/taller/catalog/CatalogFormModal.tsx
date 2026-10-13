import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { catalogApi, type CatalogItemDto } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../lib/api/client'

const inputCls = 'w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500'

const schema = z.object({
  kind: z.enum(['Service', 'Product']),
  sku: z.string().trim().max(50).optional(),
  name: z.string().trim().min(1, 'Requerido').max(200),
  description: z.string().trim().max(500).optional(),
  netPrice: z.number().min(0, 'El precio no puede ser negativo'),
  vatRate: z.number().min(0).max(100),
  trackStock: z.boolean(),
  minStockQty: z.string().optional(),
  maxStockQty: z.string().optional(),
  isActive: z.boolean(),
})

type FormData = z.infer<typeof schema>

function toFormData(dto: CatalogItemDto): FormData {
  return {
    kind: dto.kind,
    sku: dto.sku ?? '',
    name: dto.name,
    description: dto.description ?? '',
    netPrice: dto.netPrice,
    vatRate: dto.vatRate,
    trackStock: dto.trackStock,
    minStockQty: dto.minStockQty != null ? String(dto.minStockQty) : '',
    maxStockQty: dto.maxStockQty != null ? String(dto.maxStockQty) : '',
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

  const { register, handleSubmit, watch, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: item
      ? toFormData(item)
      : { kind: 'Service', vatRate: 21, trackStock: false, isActive: true },
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
          description: data.description || null,
          netPrice: data.netPrice,
          vatRate: data.vatRate,
          trackStock: data.trackStock,
          minStockQty: numOrNull(data.minStockQty),
          maxStockQty: numOrNull(data.maxStockQty),
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
          trackStock: data.trackStock,
          minStockQty: numOrNull(data.minStockQty),
          maxStockQty: numOrNull(data.maxStockQty),
          isActive: data.isActive,
        })
        onSuccess(result)
      }
    } catch (e) {
      setServerError(e instanceof ApiError ? e.detail : 'Error inesperado')
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-lg rounded-xl bg-white p-6 shadow-xl">
        <h2 className="mb-5 text-lg font-semibold">
          {isEdit ? 'Editar ítem' : 'Nuevo ítem de catálogo'}
        </h2>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">

          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium">Tipo *</label>
              <select {...register('kind')} className={inputCls}>
                <option value="Service">Servicio</option>
                <option value="Product">Producto</option>
              </select>
            </div>
          )}

          <div>
            <label className="mb-1 block text-sm font-medium">Nombre *</label>
            <input {...register('name')} className={inputCls} placeholder="Ej. Cambio de aceite" />
            {errors.name && <p className="mt-1 text-xs text-red-600">{errors.name.message}</p>}
          </div>

          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium">SKU</label>
              <input {...register('sku')} className={inputCls} placeholder="Ej. FIL-001" />
            </div>
          )}

          <div>
            <label className="mb-1 block text-sm font-medium">Descripción</label>
            <textarea
              {...register('description')}
              rows={2}
              className={inputCls}
              placeholder="Descripción opcional…"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium">Precio neto *</label>
              <input
                {...register('netPrice', { valueAsNumber: true })}
                type="number"
                min={0}
                step="0.01"
                className={inputCls}
                placeholder="0.00"
              />
              {errors.netPrice && <p className="mt-1 text-xs text-red-600">{errors.netPrice.message}</p>}
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">IVA %</label>
              <input
                {...register('vatRate', { valueAsNumber: true })}
                type="number"
                min={0}
                max={100}
                step="1"
                className={inputCls}
                placeholder="21"
              />
            </div>
          </div>

          {(kind === 'Product' || isEdit) && (
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" {...register('trackStock')} className="h-4 w-4 rounded" />
              Controlar stock
            </label>
          )}

          {trackStock && (
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="mb-1 block text-sm font-medium">Stock mínimo</label>
                <input
                  {...register('minStockQty')}
                  type="number"
                  min={0}
                  step="0.001"
                  className={inputCls}
                  placeholder="—"
                />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium">Stock máximo</label>
                <input
                  {...register('maxStockQty')}
                  type="number"
                  min={0}
                  step="0.001"
                  className={inputCls}
                  placeholder="—"
                />
              </div>
            </div>
          )}

          {isEdit && (
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" {...register('isActive')} className="h-4 w-4 rounded" />
              Activo
            </label>
          )}

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
