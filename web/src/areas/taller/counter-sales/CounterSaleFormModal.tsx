import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { counterSalesApi, type CounterSaleDto } from '../../../lib/api/counterSales'
import { customersApi } from '../../../lib/api/customers'
import { catalogApi } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'

const inputCls = 'w-full border border-tc-border bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors'
const labelCls = 'mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500'

const itemSchema = z.object({
  catalogItemId: z.string().optional(),
  description: z.string().trim().min(1, 'Requerido'),
  quantity: z.coerce.number().positive('Debe ser > 0'),
  unitPrice: z.coerce.number().min(0),
  vatRate: z.coerce.number().min(0).max(100),
})

const schema = z.object({
  customerId: z.string().optional(),
  items: z.array(itemSchema).min(1, 'Agregá al menos un ítem'),
})

type FormInput = z.input<typeof schema>
type FormData = z.output<typeof schema>

interface Props {
  onSuccess: (dto: CounterSaleDto) => void
  onClose: () => void
}

export function CounterSaleFormModal({ onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)

  const { data: customersPage } = useQuery({
    queryKey: ['customers-picker'],
    queryFn: () => customersApi.search(null, 1, 100),
  })

  const { data: catalogPage } = useQuery({
    queryKey: ['catalog-picker-products-services'],
    queryFn: () => catalogApi.search(null, undefined, true, 1, 100),
  })

  const { register, handleSubmit, control, setValue, formState: { errors, isSubmitting } } = useForm<FormInput, unknown, FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      customerId: '',
      items: [{ catalogItemId: '', description: '', quantity: 1, unitPrice: 0, vatRate: 21 }],
    },
  })

  const { fields, append, remove } = useFieldArray({ control, name: 'items' })

  function handleCatalogSelect(index: number, catalogId: string) {
    const item = catalogPage?.items.find(i => i.id === catalogId)
    if (item) {
      setValue(`items.${index}.description`, item.name)
      setValue(`items.${index}.unitPrice`, String(item.netPrice) as unknown as number)
      setValue(`items.${index}.vatRate`, String(item.vatRate * 100) as unknown as number)
    }
  }

  async function onSubmit(values: FormData) {
    setServerError(null)
    try {
      const dto = await counterSalesApi.create({
        customerId: values.customerId || null,
        items: values.items.map(i => ({
          catalogItemId: i.catalogItemId || null,
          description: i.description,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          vatRate: i.vatRate,
        })),
      })
      onSuccess(dto)
    } catch (err) {
      setServerError(err instanceof ApiError ? err.detail : 'Error al crear la venta')
    }
  }

  return (
    <Modal title="Nueva venta de mostrador" onClose={onClose} className="max-w-2xl">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
        {serverError && (
          <p className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
        )}

        <div>
          <label htmlFor="cs-customer" className={labelCls}>Cliente (opcional)</label>
          <select id="cs-customer" {...register('customerId')} className={inputCls}>
            <option value="">— Sin cliente —</option>
            {customersPage?.items.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </div>

        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <span className={labelCls}>Ítems</span>
            <button
              type="button"
              onClick={() => append({ catalogItemId: '', description: '', quantity: 1, unitPrice: 0, vatRate: 21 })}
              className="text-xs text-tc-purple hover:opacity-70"
            >
              + Agregar ítem
            </button>
          </div>

          {errors.items && typeof errors.items === 'object' && 'message' in errors.items && (
            <p className="text-xs text-red-600">{errors.items.message as string}</p>
          )}

          {fields.map((field, idx) => (
            <div key={field.id} className="border border-tc-border bg-tc-surface p-3 space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-[10px] font-bold tracking-wider uppercase text-slate-400">Ítem {idx + 1}</span>
                {fields.length > 1 && (
                  <button type="button" onClick={() => remove(idx)} className="text-xs text-tc-stamp-red hover:opacity-70">
                    Quitar
                  </button>
                )}
              </div>

              {catalogPage && catalogPage.items.length > 0 && (
                <div>
                  <label htmlFor={`csal-item-${idx}-cat`} className={labelCls}>Del catálogo</label>
                  <select
                    id={`csal-item-${idx}-cat`}
                    {...register(`items.${idx}.catalogItemId`)}
                    className={inputCls}
                    onChange={e => {
                      register(`items.${idx}.catalogItemId`).onChange(e)
                      if (e.target.value) handleCatalogSelect(idx, e.target.value)
                    }}
                  >
                    <option value="">— Ítem libre —</option>
                    {catalogPage.items.map(c => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </div>
              )}

              <div className="grid grid-cols-2 gap-2">
                <div className="col-span-2">
                  <label htmlFor={`csal-item-${idx}-desc`} className={labelCls}>Descripción *</label>
                  <input id={`csal-item-${idx}-desc`} {...register(`items.${idx}.description`)} className={inputCls} placeholder="Descripción del producto o servicio" />
                  {errors.items?.[idx]?.description && (
                    <p className="mt-1 text-xs text-red-600">{errors.items[idx].description?.message}</p>
                  )}
                </div>
                <div>
                  <input aria-label="Cantidad" type="number" step="0.001" {...register(`items.${idx}.quantity`)} className={inputCls} />
                </div>
                <div>
                  <input aria-label="Precio unitario neto" type="number" step="0.01" {...register(`items.${idx}.unitPrice`)} className={inputCls} />
                </div>
                <div>
                  <input aria-label="IVA %" type="number" step="1" {...register(`items.${idx}.vatRate`)} className={inputCls} />
                </div>
              </div>
            </div>
          ))}
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
          <Button type="submit" size="sm" loading={isSubmitting}>Crear venta</Button>
        </div>
      </form>
    </Modal>
  )
}
