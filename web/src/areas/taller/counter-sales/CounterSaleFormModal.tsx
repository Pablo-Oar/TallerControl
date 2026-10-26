import { useForm, useFieldArray } from 'react-hook-form'
import { useDefaultVat } from '../../../lib/auth/defaults'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { counterSalesApi, type CounterSaleDto } from '../../../lib/api/counterSales'
import type { CatalogItemListDto } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { FormField } from '../../../components/ui/FormField'
import { CatalogPicker, CustomerPicker } from '../../../components/ui/EntityPickers'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'
import { labelCls } from '../../../lib/styles'


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
  const defaultVat = useDefaultVat()

  const { register, handleSubmit, control, setValue, watch, formState: { errors, isSubmitting } } = useForm<FormInput, unknown, FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      customerId: '',
      items: [{ catalogItemId: '', description: '', quantity: 1, unitPrice: 0, vatRate: defaultVat }],
    },
  })

  const { fields, append, remove } = useFieldArray({ control, name: 'items' })

  function handleCatalogSelect(index: number, item: CatalogItemListDto | null) {
    setValue(`items.${index}.catalogItemId`, item?.id ?? '')
    if (item) {
      setValue(`items.${index}.description`, item.name)
      setValue(`items.${index}.unitPrice`, String(item.netPrice) as unknown as number)
      setValue(`items.${index}.vatRate`, String(item.vatRate) as unknown as number)
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

        <CustomerPicker
          label="Cliente (opcional)"
          value={watch('customerId') ?? ''}
          onChange={id => setValue('customerId', id)}
        />

        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <span className={labelCls}>Ítems</span>
            <button
              type="button"
              onClick={() => append({ catalogItemId: '', description: '', quantity: 1, unitPrice: 0, vatRate: defaultVat })}
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
                <span className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Ítem {idx + 1}</span>
                {fields.length > 1 && (
                  <button type="button" onClick={() => remove(idx)} className="text-xs text-tc-stamp-red hover:opacity-70">
                    Quitar
                  </button>
                )}
              </div>

              <CatalogPicker
                label="Del catálogo (opcional)"
                size="sm"
                onPick={item => handleCatalogSelect(idx, item)}
              />

              <FormField
                label="Descripción"
                requiredMark
                size="sm"
                placeholder="Descripción del producto o servicio"
                error={errors.items?.[idx]?.description?.message}
                {...register(`items.${idx}.description`)}
              />
              <div className="grid grid-cols-3 gap-2">
                <FormField label="Cantidad" size="sm" type="number" step="0.001" inputClassName="tabular-nums" {...register(`items.${idx}.quantity`)} />
                <FormField label="Precio neto" size="sm" type="number" step="0.01" inputClassName="tabular-nums" {...register(`items.${idx}.unitPrice`)} />
                <FormField label="IVA %" size="sm" type="number" step="any" inputClassName="tabular-nums" {...register(`items.${idx}.vatRate`)} />
              </div>
            </div>
          ))}
        </div>

        <div className="sticky bottom-0 -mx-5 -mb-5 mt-2 flex justify-end gap-2 border-t border-tc-border bg-white px-5 py-3">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
          <Button type="submit" size="sm" loading={isSubmitting}>Crear venta</Button>
        </div>
      </form>
    </Modal>
  )
}
