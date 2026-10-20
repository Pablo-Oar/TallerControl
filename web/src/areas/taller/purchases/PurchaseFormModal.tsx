import { useState } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery } from '@tanstack/react-query'
import { purchasesApi, type PurchaseDto } from '../../../lib/api/purchases'
import { suppliersApi } from '../../../lib/api/suppliers'
import { catalogApi } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect } from '../../../components/ui/FormField'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'
import { labelCls } from '../../../lib/styles'


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
          <FormSelect
            key={suppliersPage ? 'loaded' : 'loading'}
            label="Proveedor"
            requiredMark
            error={errors.supplierId?.message}
            {...register('supplierId')}
          >
            <option value="">Seleccioná un proveedor</option>
            {suppliersPage?.items.map(s => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </FormSelect>
          <FormField label="Fecha de factura" requiredMark type="date" error={errors.invoiceDate?.message} {...register('invoiceDate')} />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <FormField label="Punto de venta (opcional)" type="number" placeholder="Ej. 1" min={1} {...register('pointOfSale')} />
          <FormField label="Nro. de factura (opcional)" type="number" placeholder="Ej. 1234" min={1} {...register('invoiceNumber')} />
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
                    <FormSelect
                      label="Catálogo (opcional)"
                      size="sm"
                      onChange={e => {
                        setValue(`items.${index}.catalogItemId`, e.target.value)
                        handleCatalogSelect(index, e.target.value)
                      }}
                      defaultValue=""
                    >
                      <option value="">Sin catálogo</option>
                      {catalogPage.items.map(c => (
                        <option key={c.id} value={c.id}>{c.name}</option>
                      ))}
                    </FormSelect>
                  )}
                  <FormField
                    label="Descripción del ítem"
                    size="sm"
                    error={errors.items?.[index]?.description?.message}
                    {...register(`items.${index}.description`)}
                  />
                </div>
                <FormField
                  label="Cant."
                  size="sm"
                  type="number"
                  step="0.001"
                  min="0.001"
                  inputClassName="tabular-nums"
                  {...register(`items.${index}.quantity`)}
                />
                <FormField
                  label="Costo unit."
                  size="sm"
                  type="number"
                  step="0.01"
                  min="0"
                  inputClassName="tabular-nums"
                  {...register(`items.${index}.unitCost`)}
                />
                <FormField
                  label="IVA %"
                  size="sm"
                  type="number"
                  step="0.01"
                  min="0"
                  max="100"
                  inputClassName="tabular-nums"
                  {...register(`items.${index}.vatRate`)}
                />
                <div className="flex items-center justify-center pt-1">
                  {fields.length > 1 && (
                    <button
                      type="button"
                      onClick={() => remove(index)}
                      className="text-xs text-tc-text-muted hover:text-red-600 transition-colors"
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
          <FormField label="Percepciones" type="number" step="0.01" min="0" placeholder="0,00" inputClassName="tabular-nums" {...register('perceptions')} />
          <FormField label="Retenciones" type="number" step="0.01" min="0" placeholder="0,00" inputClassName="tabular-nums" {...register('retentions')} />
        </div>

        <label className="flex items-center gap-2 text-sm text-tc-text-secondary">
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
