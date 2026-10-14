import { useState } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery } from '@tanstack/react-query'
import { workOrdersApi, type WorkOrderPriority } from '../../../lib/api/workOrders'
import { customersApi } from '../../../lib/api/customers'
import { catalogApi, type CatalogItemKind } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../lib/api/client'

const inputCls = 'w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500'

const itemSchema = z.object({
  kind: z.enum(['service', 'product', 'manual']),
  description: z.string().trim().min(1, 'Requerido').max(200),
  catalogItemId: z.string().nullable(),
  quantity: z.number().min(0.001, 'Cantidad > 0'),
  unitNetPrice: z.number().min(0),
  vatRate: z.number().min(0).max(100),
})

const schema = z.object({
  customerId: z.string().min(1, 'Seleccione un cliente'),
  vehicleId: z.string().min(1, 'Seleccione un vehículo'),
  odometerKm: z.number().int().min(0),
  fuelLevel: z.number().int().min(0).max(100).nullable(),
  reportedIssue: z.string().trim().max(1000).nullable(),
  priority: z.enum(['low', 'normal', 'high']),
  items: z.array(itemSchema),
})

type FormData = z.infer<typeof schema>

interface Props {
  onSuccess: () => void
  onClose: () => void
}

export function WorkOrderFormModal({ onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)
  const [customerSearch, setCustomerSearch] = useState('')

  const { register, handleSubmit, watch, control, setValue, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      customerId: '',
      vehicleId: '',
      odometerKm: 0,
      fuelLevel: null,
      reportedIssue: null,
      priority: 'normal',
      items: [],
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

  function handleCatalogItemSelect(index: number, itemId: string) {
    const item = catalogData?.items.find(i => i.id === itemId)
    if (!item) return
    setValue(`items.${index}.catalogItemId`, itemId)
    setValue(`items.${index}.description`, item.name)
    setValue(`items.${index}.unitNetPrice`, item.netPrice)
    setValue(`items.${index}.vatRate`, item.vatRate)
    setValue(`items.${index}.kind`, item.kind.toLowerCase() as 'service' | 'product' | 'manual')
  }

  function addItem() {
    append({
      kind: 'service',
      description: '',
      catalogItemId: null,
      quantity: 1,
      unitNetPrice: 0,
      vatRate: 21,
    })
  }

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      await workOrdersApi.create({
        customerId: data.customerId,
        vehicleId: data.vehicleId,
        odometerKm: data.odometerKm,
        fuelLevel: data.fuelLevel,
        reportedIssue: data.reportedIssue,
        priority: data.priority as WorkOrderPriority,
        items: data.items.map(item => ({
          kind: item.kind,
          description: item.description,
          catalogItemId: item.catalogItemId,
          quantity: item.quantity,
          unitNetPrice: item.unitNetPrice,
          vatRate: item.vatRate,
        })),
      })
      onSuccess()
    } catch (e) {
      setServerError(e instanceof ApiError ? e.detail : 'Error inesperado')
    }
  }

  const vehicles = customerDetail?.vehicles ?? []

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 p-4 pt-10">
      <div className="w-full max-w-2xl rounded-xl bg-white p-6 shadow-xl">
        <h2 className="mb-5 text-lg font-semibold">Nueva orden de trabajo</h2>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">

          {/* Cliente */}
          <div>
            <label className="mb-1 block text-sm font-medium">Cliente *</label>
            <input
              type="search"
              value={customerSearch}
              onChange={e => setCustomerSearch(e.target.value)}
              placeholder="Buscar cliente…"
              className={inputCls}
            />
            {customersData && customersData.items.length > 0 && !selectedCustomerId && (
              <ul className="mt-1 max-h-40 overflow-y-auto rounded-md border border-slate-200 bg-white shadow-sm">
                {customersData.items.map(c => (
                  <li key={c.id}>
                    <button
                      type="button"
                      onClick={() => handleSelectCustomer(c.id)}
                      className="w-full px-3 py-2 text-left text-sm hover:bg-slate-50"
                    >
                      {c.name}
                      {c.docNumber && <span className="ml-2 text-xs text-slate-400">{c.docNumber}</span>}
                    </button>
                  </li>
                ))}
              </ul>
            )}
            {selectedCustomerId && customerDetail && (
              <div className="mt-1 flex items-center gap-2 rounded-md bg-blue-50 px-3 py-2 text-sm">
                <span className="font-medium">{customerDetail.name}</span>
                <button
                  type="button"
                  onClick={() => { setValue('customerId', ''); setValue('vehicleId', ''); setCustomerSearch('') }}
                  className="ml-auto text-xs text-slate-400 hover:text-red-500"
                >
                  Cambiar
                </button>
              </div>
            )}
            {errors.customerId && <p className="mt-1 text-xs text-red-600">{errors.customerId.message}</p>}
          </div>

          {/* Vehículo */}
          {selectedCustomerId && (
            <div>
              <label className="mb-1 block text-sm font-medium">Vehículo *</label>
              {vehicles.length === 0 ? (
                <p className="text-sm text-slate-400">El cliente no tiene vehículos registrados.</p>
              ) : (
                <select {...register('vehicleId')} className={inputCls}>
                  <option value="">Seleccionar…</option>
                  {vehicles.map(v => (
                    <option key={v.id} value={v.id}>
                      {v.plate}{v.brand ? ` — ${v.brand}` : ''}{v.model ? ` ${v.model}` : ''}
                    </option>
                  ))}
                </select>
              )}
              {errors.vehicleId && <p className="mt-1 text-xs text-red-600">{errors.vehicleId.message}</p>}
            </div>
          )}

          {/* Odómetro y combustible */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium">Odómetro (km) *</label>
              <input
                {...register('odometerKm', { valueAsNumber: true })}
                type="number"
                min={0}
                className={inputCls}
                placeholder="0"
              />
              {errors.odometerKm && <p className="mt-1 text-xs text-red-600">{errors.odometerKm.message}</p>}
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">Combustible %</label>
              <input
                {...register('fuelLevel', { valueAsNumber: true, setValueAs: v => (v === '' || isNaN(v) ? null : v) })}
                type="number"
                min={0}
                max={100}
                step={5}
                className={inputCls}
                placeholder="—"
              />
            </div>
          </div>

          {/* Prioridad */}
          <div>
            <label className="mb-1 block text-sm font-medium">Prioridad</label>
            <select {...register('priority')} className={inputCls}>
              <option value="low">Baja</option>
              <option value="normal">Normal</option>
              <option value="high">Alta</option>
            </select>
          </div>

          {/* Problema reportado */}
          <div>
            <label className="mb-1 block text-sm font-medium">Problema reportado</label>
            <textarea
              {...register('reportedIssue')}
              rows={2}
              className={inputCls}
              placeholder="Descripción del problema…"
            />
          </div>

          {/* Renglones */}
          <div>
            <div className="mb-2 flex items-center justify-between">
              <label className="text-sm font-medium">Renglones</label>
              <button
                type="button"
                onClick={addItem}
                className="text-xs text-blue-600 hover:underline"
              >
                + Agregar
              </button>
            </div>
            {fields.length === 0 && (
              <p className="text-xs text-slate-400">Sin renglones. La orden se crea sin ítems.</p>
            )}
            <div className="space-y-3">
              {fields.map((field, index) => (
                <ItemRow
                  key={field.id}
                  index={index}
                  register={register}
                  errors={errors}
                  catalogItems={catalogData?.items ?? []}
                  onSelectCatalog={handleCatalogItemSelect}
                  onRemove={() => remove(index)}
                />
              ))}
            </div>
          </div>

          {serverError && (
            <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
          )}

          <div className="flex justify-end gap-3 pt-2">
            <Button type="button" variant="secondary" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={isSubmitting}>Crear orden</Button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── Sub-componente de renglón ──────────────────────────────────────────────────

interface CatalogItemOption {
  id: string
  name: string
  kind: CatalogItemKind
  netPrice: number
  vatRate: number
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function ItemRow({ index, register, errors, catalogItems, onSelectCatalog, onRemove }: {
  index: number
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  register: any
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  errors: any
  catalogItems: CatalogItemOption[]
  onSelectCatalog: (index: number, id: string) => void
  onRemove: () => void
}) {
  return (
    <div className="rounded-md border border-slate-200 p-3">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-xs font-medium text-slate-500">Renglón {index + 1}</span>
        <button type="button" onClick={onRemove} className="text-xs text-red-500 hover:underline">Quitar</button>
      </div>
      <div className="space-y-2">
        <div>
          <label className="mb-1 block text-xs text-slate-500">Ítem de catálogo (opcional)</label>
          <select
            onChange={e => { if (e.target.value) onSelectCatalog(index, e.target.value) }}
            className="w-full rounded-md border border-slate-200 px-2 py-1.5 text-sm outline-none"
          >
            <option value="">— manual —</option>
            {catalogItems.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </div>
        <div>
          <input
            {...register(`items.${index}.description`)}
            placeholder="Descripción *"
            className="w-full rounded-md border border-slate-200 px-2 py-1.5 text-sm outline-none focus:ring-1 focus:ring-blue-500"
          />
          {errors.items?.[index]?.description && (
            <p className="mt-0.5 text-xs text-red-600">{errors.items[index].description.message}</p>
          )}
        </div>
        <div className="grid grid-cols-3 gap-2">
          <div>
            <label className="mb-0.5 block text-xs text-slate-400">Cantidad</label>
            <input
              {...register(`items.${index}.quantity`, { valueAsNumber: true })}
              type="number"
              min={0}
              step="0.001"
              className="w-full rounded-md border border-slate-200 px-2 py-1.5 text-sm outline-none"
            />
          </div>
          <div>
            <label className="mb-0.5 block text-xs text-slate-400">P. neto</label>
            <input
              {...register(`items.${index}.unitNetPrice`, { valueAsNumber: true })}
              type="number"
              min={0}
              step="0.01"
              className="w-full rounded-md border border-slate-200 px-2 py-1.5 text-sm outline-none"
            />
          </div>
          <div>
            <label className="mb-0.5 block text-xs text-slate-400">IVA %</label>
            <input
              {...register(`items.${index}.vatRate`, { valueAsNumber: true })}
              type="number"
              min={0}
              max={100}
              className="w-full rounded-md border border-slate-200 px-2 py-1.5 text-sm outline-none"
            />
          </div>
        </div>
      </div>
    </div>
  )
}
