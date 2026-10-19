import { useState } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery } from '@tanstack/react-query'
import { workOrdersApi, type WorkOrderPriority } from '../../../lib/api/workOrders'
import { customersApi } from '../../../lib/api/customers'
import { catalogApi, type CatalogItemKind } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'

const inputCls = 'w-full border border-tc-border bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors'
const labelCls = 'mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500'

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
    enabled: customerSearch.length >= 1,
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
    <Modal title="Nueva orden de trabajo" onClose={onClose} className="max-w-2xl">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">

          {/* Cliente */}
          <div>
            <label htmlFor="wo-customer-search" className={labelCls}>Cliente *</label>
            {!selectedCustomerId && (
              <div className="relative">
                <input
                  id="wo-customer-search"
                  type="search"
                  value={customerSearch}
                  onChange={e => setCustomerSearch(e.target.value)}
                  placeholder="Escribir nombre o DNI para buscar…"
                  className={inputCls}
                  autoComplete="off"
                />
                {customerSearch.length >= 1 && (
                  <ul className="absolute z-10 mt-1 w-full max-h-52 overflow-y-auto rounded-md border border-slate-200 bg-white shadow-lg">
                    {!customersData && (
                      <li className="px-3 py-2 text-sm text-slate-400">Buscando…</li>
                    )}
                    {customersData && customersData.items.length === 0 && (
                      <li className="px-3 py-2 text-sm text-slate-400">Sin resultados</li>
                    )}
                    {customersData && customersData.items.map(c => (
                      <li key={c.id}>
                        <button
                          type="button"
                          onClick={() => handleSelectCustomer(c.id)}
                          className="w-full px-3 py-2 text-left text-sm hover:bg-slate-50"
                        >
                          <span className="font-medium">{c.name}</span>
                          {c.docNumber && <span className="ml-2 text-xs text-slate-400">{c.docNumber}</span>}
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
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
              <label htmlFor="wo-vehicle" className={labelCls}>Vehículo *</label>
              {vehicles.length === 0 ? (
                <p className="text-sm text-slate-400">El cliente no tiene vehículos registrados.</p>
              ) : (
                <select id="wo-vehicle" {...register('vehicleId')} className={inputCls}>
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
              <label htmlFor="wo-odometer" className={labelCls}>Odómetro (km) *</label>
              <input
                id="wo-odometer"
                {...register('odometerKm', { valueAsNumber: true })}
                type="number"
                min={0}
                className={inputCls}
                placeholder="0"
              />
              {errors.odometerKm && <p className="mt-1 text-xs text-red-600">{errors.odometerKm.message}</p>}
            </div>
            <div>
              <label htmlFor="wo-fuel" className={labelCls}>Combustible %</label>
              <input
                id="wo-fuel"
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
            <label htmlFor="wo-priority" className={labelCls}>Prioridad</label>
            <select id="wo-priority" {...register('priority')} className={inputCls}>
              <option value="low">Baja</option>
              <option value="normal">Normal</option>
              <option value="high">Alta</option>
            </select>
          </div>

          {/* Problema reportado */}
          <div>
            <label htmlFor="wo-issue" className={labelCls}>Problema reportado</label>
            <textarea
              id="wo-issue"
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
            <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
          )}

          <div className="flex justify-end gap-3 pt-2">
            <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
            <Button type="submit" size="sm" loading={isSubmitting}>Crear orden</Button>
          </div>
        </form>
    </Modal>
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
  const rowInputCls = 'w-full border border-tc-border bg-white px-2 py-1.5 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors'
  const catId = `wo-item-${index}-cat`

  return (
    <div className="border border-tc-border p-3 bg-tc-surface">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-xs font-bold tracking-wider uppercase text-slate-400">Renglón {index + 1}</span>
        <button type="button" onClick={onRemove} className="text-xs font-bold tracking-wider uppercase text-tc-stamp-red hover:opacity-70 transition-opacity">Quitar</button>
      </div>
      <div className="space-y-2">
        <div>
          <label htmlFor={catId} className="mb-1 block text-[10px] font-bold tracking-wider uppercase text-slate-400">Ítem de catálogo (opcional)</label>
          <select
            id={catId}
            onChange={e => { if (e.target.value) onSelectCatalog(index, e.target.value) }}
            className={rowInputCls}
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
            aria-label={`Descripción del renglón ${index + 1}`}
            placeholder="Descripción *"
            className={rowInputCls}
          />
          {errors.items?.[index]?.description && (
            <p className="mt-0.5 text-xs text-red-600">{errors.items[index].description.message}</p>
          )}
        </div>
        <div className="grid grid-cols-3 gap-2">
          <div>
            <input
              {...register(`items.${index}.quantity`, { valueAsNumber: true })}
              aria-label="Cantidad"
              type="number"
              min={0}
              step="1"
              className={rowInputCls}
              placeholder="Cant."
            />
          </div>
          <div>
            <input
              {...register(`items.${index}.unitNetPrice`, { valueAsNumber: true })}
              aria-label="Precio neto"
              type="number"
              min={0}
              step="1"
              className={rowInputCls}
              placeholder="P. neto"
            />
          </div>
          <div>
            <input
              {...register(`items.${index}.vatRate`, { valueAsNumber: true })}
              aria-label="IVA %"
              type="number"
              min={0}
              max={100}
              className={rowInputCls}
              placeholder="IVA%"
            />
          </div>
        </div>
      </div>
    </div>
  )
}
