import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { catalogApi, type CatalogItemListDto } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'

const inputCls = 'w-full border border-tc-border bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors'
const labelCls = 'mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500'

const schema = z.object({
  quantity: z.number().refine(v => v !== 0, 'La cantidad no puede ser cero'),
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
    <Modal title="Ajuste de stock" onClose={onClose} className="max-w-sm">
      <p className="mb-4 text-sm text-slate-500">{item.name}</p>

      <div className="mb-4 border border-tc-border bg-tc-surface px-3 py-2 text-sm text-slate-700">
        Stock actual: <span className="font-semibold tabular-nums text-slate-800">{item.stockQty}</span>
        {item.isLowStock && (
          <span className="ml-2 bg-amber-100 px-1.5 py-0.5 text-xs font-bold uppercase tracking-wider text-amber-700">
            Stock bajo
          </span>
        )}
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <label htmlFor="stock-qty" className={labelCls}>
            Cantidad (+ entrada / − salida) *
          </label>
          <input
            id="stock-qty"
            {...register('quantity', { valueAsNumber: true })}
            type="number"
            step="0.001"
            className={inputCls}
            placeholder="Ej. 10 o -5"
          />
          {errors.quantity && <p className="mt-1 text-xs text-red-600">{errors.quantity.message}</p>}
        </div>

        <div>
          <label htmlFor="stock-note" className={labelCls}>Nota *</label>
          <input
            id="stock-note"
            {...register('note')}
            className={inputCls}
            placeholder="Motivo del ajuste…"
          />
          {errors.note && <p className="mt-1 text-xs text-red-600">{errors.note.message}</p>}
        </div>

        {serverError && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
        )}

        <div className="flex justify-end gap-3 pt-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
          <Button type="submit" size="sm" loading={isSubmitting}>Registrar</Button>
        </div>
      </form>
    </Modal>
  )
}
