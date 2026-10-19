import { useState } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery } from '@tanstack/react-query'
import { purchasesApi, type PurchaseDto } from '../../../lib/api/purchases'
import { suppliersApi } from '../../../lib/api/suppliers'
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
  unitCost: z.coerce.number().min(0),
  vatRate: z.coerce.number().min(0).max(100),
})

const schema = z.object({
  supplierId: z.string().min(1, 'Seleccioná un proveedor'),
  invoiceDate: z.string().min(1, 'Requerida'),
  pointOfSale: z.coerce.number().int().positive().optional().or(z.literal('')),
  invoiceNumber: z.coerce.number().int().positive().optional().or(z.literal('')),
  perceptions: z.coerce.number().min(0).default(0),
  retentions: z.coerce.number().min(0).default(0),
  isExcludedFromCiti: z.boolean().default(false),
  items: z.array(itemSchema).min(1, 'Agregá al menos un ítem'),
})

type FormInput = z.input<typeof schema>
type FormData = z.output<typeof schema>

interface Props {
  onSuccess: (dto: PurchaseDto) => void
  onClose: () => void
}

export function PurchaseFormModal({ onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)

  const { data: suppliersPage } = useQuery({
    queryKey: ['suppliers-picker'],
    queryFn: () => suppliersApi.search(null, 1, 100),
  })

  const { data: catalogPage } = useQuery({
    queryKey: ['catalog-picker-products'],
    queryFn: () => catalogApi.search(null, 'product', true, 1, 100),
  })

  const { register, handleSubmit, control, setValue, formState: { errors, isSubmitting } } = useForm<FormInput, unknown, FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      supplierId: '',
      invoiceDate: new Date().toISOString().substring(0, 10),
      pointOfSale: '',
      invoiceNumber: '',
      perceptions: 0,
      retentions: 0,
      isExcludedFromCiti: false,
      items: [{ catalogItemId: '', description: '', quantity: 1, unitCost: 0, vatRate: 21 }],
    },
  })

  const { fields, append, remove } = useFieldArray({ control, name: 'items' })

  function handleCatalogSelect(index: number, catalogId: string) {
    const item = catalogPage?.items.find(i => i.id === catalogId)
    if (item) {
      setValue(`items.${index}.description`, item.name)
      setValue(`items.${index}.unitCost`, String(item.netPrice) as unknown as number)
      setValue(`items.${index}.vatRate`, String(item.vatRate * 100) as unknown as number)
    }
  }

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      const result = await purchasesApi.create({
        supplierId: data.supplierId,
        invoiceDate: data.invoiceDate,
        pointOfSale: typeof data.pointOfSale === 'number' ? data.pointOfSale : undefined,
        invoiceNumber: typeof data.invoiceNumber === 'number' ? data.invoiceNumber : undefined,
        perceptions: data.perceptions,
        retentions: data.retentions,
        items: data.items.map(i => ({
          catalogItemId: i.catalogItemId || undefined,
          description: i.description,
          quantity: i.quantity,
          unitCost: i.unitCost,
          vatRate: i.vatRate,
        })),
      })
      onSuccess(result)
    } catch (e) {
      setServerError(e instanceof ApiError ? e.detail : 'Error inesperado')
    }
  }

  return (
    <Modal title="Nueva compra" onClose={onClose} className="max-w-2xl">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="pur-supplier" className={labelCls}>Proveedor *</label>
            <select id="pur-supplier" {...register('supplierId')} className={inputCls}>
              <option value="">Seleccioná un proveedor</option>
              {suppliersPage?.items.map(s => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
            {errors.supplierId && <p className="mt-1 text-xs text-red-600">{errors.supplierId.message}</p>}
          </div>
          <div>
            <label htmlFor="pur-invoice-date" className={labelCls}>Fecha de factura *</label>
            <input id="pur-invoice-date" type="date" {...register('invoiceDate')} className={inputCls} />
            {errors.invoiceDate && <p className="mt-1 text-xs text-red-600">{errors.invoiceDate.message}</p>}
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="pur-pos" className={labelCls}>Punto de venta <span className="font-normal normal-case text-slate-400">(opcional)</span></label>
            <input id="pur-pos" type="number" {...register('pointOfSale')} className={inputCls} placeholder="Ej. 1" min={1} />
          </div>
          <div>
            <label htmlFor="pur-inv-number" className={labelCls}>Nro. de factura <span className="font-normal normal-case text-slate-400">(opcional)</span></label>
            <input id="pur-inv-number" type="number" {...register('invoiceNumber')} className={inputCls} placeholder="Ej. 1234" min={1} />
          </div>
        </div>

        {/* Ítems */}
        <div>
          <div className="mb-2 flex items-center justify-between">
            <span className={labelCls}>Ítems *</span>
            <button
              type="button"
              onClick={() => append({ catalogItemId: '', description: '', quantity: '1' as unknown as number, unitCost: '0' as unknown as number, vatRate: '21' as unknown as number })}
              className="text-xs font-bold tracking-wider uppercase text-tc-purple hover:opacity-70 transition-opacity"
            >
              + Agregar ítem
            </button>
          </div>
          {errors.items && !Array.isArray(errors.items) && (
            <p className="mb-2 text-xs text-red-600">{(errors.items as { message?: string }).message}</p>
          )}
          <div className="space-y-2">
            {fields.map((field, index) => (
              <div key={field.id} className="grid grid-cols-[1fr_5rem_6rem_4.5rem_2rem] gap-2 items-start border border-tc-border bg-tc-stub-bg p-2">
                <div className="space-y-1.5">
                  {catalogPage && catalogPage.items.length > 0 && (
                    <select
                      className="w-full border border-tc-border bg-white px-2 py-1.5 text-xs text-slate-700 outline-none focus:border-tc-border-mid"
                      onChange={e => {
                        setValue(`items.${index}.catalogItemId`, e.target.value)
                        handleCatalogSelect(index, e.target.value)
                      }}
                      defaultValue=""
                    >
                      <option value="">Catálogo (opcional)</option>
                      {catalogPage.items.map(c => (
                        <option key={c.id} value={c.id}>{c.name}</option>
                      ))}
                    </select>
                  )}
                  <input
                    {...register(`items.${index}.description`)}
                    className="w-full border border-tc-border bg-white px-2 py-1.5 text-xs text-slate-800 outline-none focus:border-tc-border-mid"
                    placeholder="Descripción del ítem"
                  />
                  {errors.items?.[index]?.description && (
                    <p className="text-xs text-red-600">{errors.items[index]?.description?.message}</p>
                  )}
                </div>
                <div>
                  <input
                    type="number"
                    step="0.001"
                    min="0.001"
                    {...register(`items.${index}.quantity`)}
                    className="w-full border border-tc-border bg-white px-2 py-1.5 text-xs tabular-nums outline-none focus:border-tc-border-mid"
                    placeholder="Cant."
                  />
                </div>
                <div>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    {...register(`items.${index}.unitCost`)}
                    className="w-full border border-tc-border bg-white px-2 py-1.5 text-xs tabular-nums outline-none focus:border-tc-border-mid"
                    placeholder="Costo unit."
                  />
                </div>
                <div>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    max="100"
                    {...register(`items.${index}.vatRate`)}
                    className="w-full border border-tc-border bg-white px-2 py-1.5 text-xs tabular-nums outline-none focus:border-tc-border-mid"
                    placeholder="IVA%"
                  />
                </div>
                <div className="flex items-center justify-center pt-1">
                  {fields.length > 1 && (
                    <button
                      type="button"
                      onClick={() => remove(index)}
                      className="text-xs text-slate-400 hover:text-red-600 transition-colors"
                    >
                      ✕
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Percepciones / retenciones */}
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="pur-perceptions" className={labelCls}>Percepciones</label>
            <input id="pur-perceptions" type="number" step="0.01" min="0" {...register('perceptions')} className={inputCls} placeholder="0,00" />
          </div>
          <div>
            <label htmlFor="pur-retentions" className={labelCls}>Retenciones</label>
            <input id="pur-retentions" type="number" step="0.01" min="0" {...register('retentions')} className={inputCls} placeholder="0,00" />
          </div>
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" {...register('isExcludedFromCiti')} className="h-4 w-4" />
          Excluir del CITI (sin datos fiscales completos)
        </label>

        {serverError && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
        )}

        <div className="flex justify-end gap-3 pt-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
          <Button type="submit" size="sm" loading={isSubmitting}>Guardar como borrador</Button>
        </div>
      </form>
    </Modal>
  )
}
