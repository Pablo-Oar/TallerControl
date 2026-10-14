import { useState, useEffect } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery } from '@tanstack/react-query'
import { quotesApi, type QuoteDto } from '../../../lib/api/quotes'
import { customersApi } from '../../../lib/api/customers'
import { catalogApi } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../lib/api/client'

const inputCls =
  'w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500'

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
  const isEditing = !!quote
  const [serverError, setServerError] = useState<string | null>(null)
  const [customerSearch, setCustomerSearch] = useState('')

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

  const { data: customersData } = useQuery({
    queryKey: ['customers-search', customerSearch],
    queryFn: () => customersApi.search(customerSearch || null, 1, 20),
    enabled: true,
  })

  const { data: customerDetail } = useQuery({
    queryKey: ['customer', selectedCustomerId],
    queryFn: () => customersApi.get(selectedCustomerId),
    enabled: !!selectedCustomerId,
  })

  const { data: catalogData } = useQuery({
    queryKey: ['catalog-active'],
    queryFn: () => catalogApi.search(null, null, true, 1, 100),
  })

  function handleSelectCustomer(id: string) {
    setValue('customerId', id)
    setValue('vehicleId', '')
  }

  function handleSelectCatalogItem(index: number, catalogId: string) {
    const item = catalogData?.items.find(c => c.id === catalogId)
    if (!item) return
    setValue(`items.${index}.catalogItemId`, catalogId)
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

  // Default validUntil to 15 days from now when creating
  useEffect(() => {
    if (!isEditing) {
      const d = new Date()
      d.setDate(d.getDate() + 15)
      setValue('validUntil', d.toISOString().slice(0, 10))
    }
  }, [isEditing, setValue])

  const vehicles = customerDetail?.vehicles ?? []

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 pt-8 pb-8">
      <div className="mx-4 w-full max-w-2xl rounded-xl bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-slate-200 px-6 py-4">
          <h2 className="text-base font-semibold text-slate-800">
            {isEditing ? 'Editar presupuesto' : 'Nuevo presupuesto'}
          </h2>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600 text-lg leading-none">
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="p-6 space-y-5">
          {/* Cliente */}
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">
              Cliente <span className="text-red-500">*</span>
            </label>
            <input
              value={customerSearch}
              onChange={e => setCustomerSearch(e.target.value)}
              className={inputCls}
              placeholder="Buscar cliente..."
            />
            {customersData && customersData.items.length > 0 && (
              <ul className="mt-1 max-h-40 overflow-y-auto rounded-md border border-slate-200 bg-white shadow-sm">
                {customersData.items.map(c => (
                  <li
                    key={c.id}
                    className="cursor-pointer px-3 py-2 text-sm hover:bg-blue-50"
                    onClick={() => { handleSelectCustomer(c.id); setCustomerSearch(c.name) }}
                  >
                    {c.name} — {c.docNumber}
                  </li>
                ))}
              </ul>
            )}
            {errors.customerId && (
              <p className="mt-1 text-xs text-red-600">{errors.customerId.message}</p>
            )}
          </div>

          {/* Vehículo */}
          {selectedCustomerId && (
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">
                Vehículo <span className="text-red-500">*</span>
              </label>
              <select {...register('vehicleId')} className={inputCls}>
                <option value="">Seleccionar vehículo</option>
                {vehicles.map(v => (
                  <option key={v.id} value={v.id}>
                    {[v.brand, v.model, v.plate].filter(Boolean).join(' · ')}
                  </option>
                ))}
              </select>
              {errors.vehicleId && (
                <p className="mt-1 text-xs text-red-600">{errors.vehicleId.message}</p>
              )}
            </div>
          )}

          <div className="grid grid-cols-2 gap-4">
            {/* Válido hasta */}
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">
                Válido hasta <span className="text-red-500">*</span>
              </label>
              <input type="date" {...register('validUntil')} className={inputCls} />
              {errors.validUntil && (
                <p className="mt-1 text-xs text-red-600">{errors.validUntil.message}</p>
              )}
            </div>
            {/* Descuento */}
            <div>
              <label className="mb-1 block text-sm font-medium text-slate-700">Descuento global (%)</label>
              <input
                type="number"
                step="0.01"
                min="0"
                max="100"
                {...register('globalDiscountPct', { valueAsNumber: true })}
                className={inputCls}
              />
            </div>
          </div>

          {/* Notas */}
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">
              Notas para el cliente
            </label>
            <textarea {...register('customerNotes')} rows={2} className={inputCls} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">
              Notas internas
            </label>
            <textarea {...register('internalNotes')} rows={2} className={inputCls} />
          </div>

          {/* Ítems */}
          <div>
            <div className="mb-2 flex items-center justify-between">
              <label className="text-sm font-medium text-slate-700">Ítems</label>
              <button
                type="button"
                onClick={() =>
                  append({
                    kind: 'service',
                    description: '',
                    catalogItemId: null,
                    quantity: 1,
                    unitNetPrice: 0,
                    vatRate: 21,
                    requiredForApproval: true,
                  })
                }
                className="text-sm text-blue-600 hover:underline"
              >
                + Agregar ítem
              </button>
            </div>

            {fields.length === 0 && (
              <p className="text-xs text-slate-400">Sin ítems todavía.</p>
            )}

            <div className="space-y-3">
              {fields.map((field, index) => (
                <div key={field.id} className="rounded-lg border border-slate-200 p-3 space-y-2">
                  {/* Selección del catálogo */}
                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <label className="mb-1 block text-xs text-slate-500">Del catálogo</label>
                      <select
                        className={inputCls}
                        onChange={e => handleSelectCatalogItem(index, e.target.value)}
                        defaultValue=""
                      >
                        <option value="">Sin catálogo</option>
                        {catalogData?.items.map(c => (
                          <option key={c.id} value={c.id}>{c.name}</option>
                        ))}
                      </select>
                    </div>
                    <div>
                      <label className="mb-1 block text-xs text-slate-500">Tipo</label>
                      <select {...register(`items.${index}.kind`)} className={inputCls}>
                        <option value="service">Servicio</option>
                        <option value="product">Producto</option>
                        <option value="manual">Manual</option>
                      </select>
                    </div>
                  </div>

                  <div>
                    <label className="mb-1 block text-xs text-slate-500">Descripción *</label>
                    <input {...register(`items.${index}.description`)} className={inputCls} />
                    {errors.items?.[index]?.description && (
                      <p className="text-xs text-red-600">{errors.items[index]?.description?.message}</p>
                    )}
                  </div>

                  <div className="grid grid-cols-3 gap-2">
                    <div>
                      <label className="mb-1 block text-xs text-slate-500">Cantidad</label>
                      <input
                        type="number"
                        step="0.001"
                        min="0"
                        {...register(`items.${index}.quantity`, { valueAsNumber: true })}
                        className={inputCls}
                      />
                    </div>
                    <div>
                      <label className="mb-1 block text-xs text-slate-500">Precio neto</label>
                      <input
                        type="number"
                        step="0.01"
                        min="0"
                        {...register(`items.${index}.unitNetPrice`, { valueAsNumber: true })}
                        className={inputCls}
                      />
                    </div>
                    <div>
                      <label className="mb-1 block text-xs text-slate-500">IVA (%)</label>
                      <input
                        type="number"
                        step="0.01"
                        min="0"
                        max="100"
                        {...register(`items.${index}.vatRate`, { valueAsNumber: true })}
                        className={inputCls}
                      />
                    </div>
                  </div>

                  <div className="flex items-center justify-between">
                    <label className="flex items-center gap-2 text-xs text-slate-600">
                      <input type="checkbox" {...register(`items.${index}.requiredForApproval`)} />
                      Requerido para aprobación
                    </label>
                    <button
                      type="button"
                      onClick={() => remove(index)}
                      className="text-xs text-red-500 hover:underline"
                    >
                      Eliminar
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {serverError && (
            <p className="text-sm text-red-600">{serverError}</p>
          )}

          <div className="flex justify-end gap-3 pt-2 border-t border-slate-100">
            <Button type="button" variant="secondary" onClick={onClose}>
              Cancelar
            </Button>
            <Button type="submit" loading={isSubmitting}>
              {isEditing ? 'Guardar cambios' : 'Crear presupuesto'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
