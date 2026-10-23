import { useState, useEffect } from 'react'
import { useDefaultVat, useQuoteValidityDays } from '../../../lib/auth/defaults'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery } from '@tanstack/react-query'
import { quotesApi, type QuoteDto } from '../../../lib/api/quotes'
import { customersApi } from '../../../lib/api/customers'
import type { CatalogItemListDto } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect, FormTextarea } from '../../../components/ui/FormField'
import { CatalogPicker, CustomerPicker } from '../../../components/ui/EntityPickers'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'
import { labelCls } from '../../../lib/styles'
import { localDateISO } from '../../../lib/dates'


const itemSchema = z.object({
  kind: z.enum(['service', 'product', 'manual']),
  description: z.string().trim().min(1, 'Requerido').max(200),
  catalogItemId: z.string().nullable(),
  quantity: z.number().min(0.001, 'Cantidad > 0'),
  unitNetPrice: z.number().min(0, 'Precio ≥ 0'),
  vatRate: z.number().min(0).max(100),
  requiredForApproval: z.boolean(),
})

const schema = z.object({
  customerId: z.string().min(1, 'Seleccione un cliente'),
  vehicleId: z.string().min(1, 'Seleccione un vehículo'),
  validUntil: z.string().min(1, 'Requerido'),
  globalDiscountPct: z.number().min(0).max(100),
  customerNotes: z.string().trim().max(1000).nullable(),
  internalNotes: z.string().trim().max(1000).nullable(),
  items: z.array(itemSchema),
})

type FormData = z.infer<typeof schema>

interface Props {
  quote?: QuoteDto
  onSuccess: () => void
  onClose: () => void
}

export function QuoteFormModal({ quote, onSuccess, onClose }: Props) {
  const defaultVat = useDefaultVat()
  const validityDays = useQuoteValidityDays()
  const isEditing = !!quote
  const [serverError, setServerError] = useState<string | null>(null)

  const { register, handleSubmit, watch, control, setValue, formState: { errors, isSubmitting } } =
    useForm<FormData>({
      resolver: zodResolver(schema),
      defaultValues: {
        customerId: quote?.customerId ?? '',
        vehicleId: quote?.vehicleId ?? '',
        validUntil: quote?.validUntil?.slice(0, 10) ?? '',
        globalDiscountPct: quote?.globalDiscountPct ?? 0,
        customerNotes: quote?.customerNotes ?? null,
        internalNotes: quote?.internalNotes ?? null,
        items: quote?.items.map(i => ({
          kind: i.kind,
          description: i.description,
          catalogItemId: i.catalogItemId,
          quantity: i.quantity,
          unitNetPrice: i.unitNetPrice,
          vatRate: i.vatRate,
          requiredForApproval: i.requiredForApproval,
        })) ?? [],
      },
    })

  const { fields, append, remove } = useFieldArray({ control, name: 'items' })

  const selectedCustomerId = watch('customerId')

  const { data: customerDetail } = useQuery({
    queryKey: ['customer', selectedCustomerId],
    queryFn: () => customersApi.get(selectedCustomerId),
    enabled: !!selectedCustomerId,
  })

  function handleSelectCatalogItem(index: number, item: CatalogItemListDto | null) {
    if (!item) {
      setValue(`items.${index}.catalogItemId`, null)
      return
    }
    setValue(`items.${index}.catalogItemId`, item.id)
    setValue(`items.${index}.description`, item.name)
    setValue(`items.${index}.unitNetPrice`, item.netPrice)
    setValue(`items.${index}.vatRate`, item.vatRate)
    setValue(`items.${index}.kind`, item.kind as 'service' | 'product')
  }

  async function onSubmit(values: FormData) {
    setServerError(null)
    try {
      if (isEditing) {
        await quotesApi.update(quote!.id, values, quote!.version)
      } else {
        await quotesApi.create(values)
      }
      onSuccess()
    } catch (err) {
      setServerError(err instanceof ApiError ? err.detail : 'Error al guardar el presupuesto.')
    }
  }

  useEffect(() => {
    if (!isEditing) {
      setValue('validUntil', localDateISO(validityDays))
    }
  }, [isEditing, setValue, validityDays])

  const vehicles = customerDetail?.vehicles ?? []

  return (
    <Modal
      title={isEditing ? 'Editar presupuesto' : 'Nuevo presupuesto'}
      onClose={onClose}
      className="max-w-2xl"
    >
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
        {/* Cliente */}
        <CustomerPicker
          value={selectedCustomerId}
          requiredMark
          error={errors.customerId?.message}
          onChange={id => {
            setValue('customerId', id, { shouldValidate: !!id })
            setValue('vehicleId', '')
          }}
        />

        {/* Vehículo */}
        {selectedCustomerId && (
          <FormSelect
            key={customerDetail ? 'loaded' : 'loading'}
            label="Vehículo"
            requiredMark
            error={errors.vehicleId?.message}
            {...register('vehicleId')}
          >
            <option value="">Seleccionar vehículo</option>
            {vehicles.map(v => (
              <option key={v.id} value={v.id}>
                {[v.brand, v.model, v.plate].filter(Boolean).join(' · ')}
              </option>
            ))}
          </FormSelect>
        )}

        <div className="grid grid-cols-2 gap-4">
          <FormField label="Válido hasta" requiredMark type="date" error={errors.validUntil?.message} {...register('validUntil')} />
          <FormField
            label="Descuento global (%)"
            type="number"
            step="1"
            min="0"
            max="100"
            inputClassName="tabular-nums"
            {...register('globalDiscountPct', { valueAsNumber: true })}
          />
        </div>

        <FormTextarea label="Notas para el cliente" rows={2} {...register('customerNotes')} />
        <FormTextarea label="Notas internas" rows={2} {...register('internalNotes')} />

        {/* Ítems */}
        <div>
          <div className="mb-2 flex items-center justify-between">
            <span className={labelCls.replace('mb-1 ', '')}>Ítems</span>
            <button
              type="button"
              onClick={() =>
                append({
                  kind: 'service',
                  description: '',
                  catalogItemId: null,
                  quantity: 1,
                  unitNetPrice: 0,
                  vatRate: defaultVat,
                  requiredForApproval: true,
                })
              }
              className="text-xs font-bold tracking-wider uppercase text-tc-purple hover:text-tc-purple-mid"
            >
              + Agregar ítem
            </button>
          </div>

          {fields.length === 0 && (
            <p className="border border-dashed border-tc-border px-4 py-3 text-xs text-tc-text-muted">
              Sin ítems todavía. Agregá al menos uno.
            </p>
          )}

          <div className="space-y-2">
            {fields.map((field, index) => (
              <div key={field.id} className="border border-tc-border bg-white">
                <div className="bg-tc-stub-bg border-b border-tc-border px-3 py-1.5 flex items-center justify-between">
                  <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">
                    Ítem {index + 1}
                  </span>
                  <button
                    type="button"
                    onClick={() => remove(index)}
                    className="text-xs text-tc-stamp-red hover:opacity-70"
                  >
                    Eliminar
                  </button>
                </div>
                <div className="space-y-3 p-3">
                  <div className="grid grid-cols-2 gap-2">
                    <CatalogPicker
                      label="Del catálogo"
                      size="sm"
                      initialId={field.catalogItemId ?? ''}
                      onPick={item => handleSelectCatalogItem(index, item)}
                    />
                    <FormSelect label="Tipo" size="sm" {...register(`items.${index}.kind`)}>
                      <option value="service">Servicio</option>
                      <option value="product">Producto</option>
                      <option value="manual">Manual</option>
                    </FormSelect>
                  </div>

                  <FormField
                    label="Descripción"
                    requiredMark
                    size="sm"
                    error={errors.items?.[index]?.description?.message}
                    {...register(`items.${index}.description`)}
                  />

                  <div className="grid grid-cols-3 gap-2">
                    <FormField
                      label="Cantidad"
                      size="sm"
                      type="number"
                      step="1"
                      min="0"
                      inputClassName="tabular-nums"
                      {...register(`items.${index}.quantity`, { valueAsNumber: true })}
                    />
                    <FormField
                      label="Precio neto"
                      size="sm"
                      type="number"
                      step="1"
                      min="0"
                      inputClassName="tabular-nums"
                      {...register(`items.${index}.unitNetPrice`, { valueAsNumber: true })}
                    />
                    <FormField
                      label="IVA %"
                      size="sm"
                      type="number"
                      step="1"
                      min="0"
                      max="100"
                      inputClassName="tabular-nums"
                      {...register(`items.${index}.vatRate`, { valueAsNumber: true })}
                    />
                  </div>

                  <label className="flex items-center gap-2 text-xs text-tc-text-secondary cursor-pointer">
                    <input type="checkbox" {...register(`items.${index}.requiredForApproval`)} className="accent-tc-purple" />
                    Requerido para aprobación
                  </label>
                </div>
              </div>
            ))}
          </div>
        </div>

        {serverError && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {serverError}
          </p>
        )}

        <div className="flex justify-end gap-3 pt-2 border-t border-tc-border">
          <Button type="button" variant="secondary" size="sm" onClick={onClose} disabled={isSubmitting}>
            Cancelar
          </Button>
          <Button type="submit" size="sm" loading={isSubmitting}>
            {isEditing ? 'Guardar cambios' : 'Crear presupuesto'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
