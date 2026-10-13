import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { catalogApi, type CatalogItemListDto } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../lib/api/client'

const inputCls = 'w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500'

const schema = z.object({
  quantity: z.coerce.number().refine(v => v !== 0, 'La cantidad no puede ser cero'),
  note: z.string().trim().min(1, 'La nota es obligatoria').max(500),
})

type FormData = z.infer<typeof schema>

interface Props {
  item: CatalogItemListDto
  onSuccess: () => void
  onClose: () => void
}

export function StockAdjustmentModal({ item, onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { quantity: 0, note: '' },
  })

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      await catalogApi.stockAdjust(item.id, { quantity: data.quantity, note: data.note })
      onSuccess()
    } catch (e) {
      setServerError(e instanceof ApiError ? e.detail : 'Error inesperado')
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-sm rounded-xl bg-white p-6 shadow-xl">
        <h2 className="mb-1 text-lg font-semibold">Ajuste de stock</h2>
        <p className="mb-5 text-sm text-slate-500">{item.name}</p>

        <div className="mb-4 rounded-md bg-slate-50 px-3 py-2 text-sm">
          Stock actual: <span className="font-semibold tabular-nums">{item.stockQty}</span>
          {item.isLowStock && (
            <span className="ml-2 rounded bg-amber-100 px-1.5 py-0.5 text-xs font-medium text-amber-700">
              Stock bajo
            </span>
          )}
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium">
              Cantidad (positivo = entrada, negativo = salida) *
            </label>
            <input
              {...register('quantity')}
              type="number"
              step="0.001"
              className={inputCls}
              placeholder="Ej. 10 o -5"
            />
            {errors.quantity && <p className="mt-1 text-xs text-red-600">{errors.quantity.message}</p>}
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">Nota *</label>
            <input
              {...register('note')}
              className={inputCls}
              placeholder="Motivo del ajuste…"
            />
            {errors.note && <p className="mt-1 text-xs text-red-600">{errors.note.message}</p>}
          </div>

          {serverError && (
            <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
          )}

          <div className="flex justify-end gap-3 pt-2">
            <Button type="button" variant="secondary" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={isSubmitting}>Registrar</Button>
          </div>
        </form>
      </div>
    </div>
  )
}
