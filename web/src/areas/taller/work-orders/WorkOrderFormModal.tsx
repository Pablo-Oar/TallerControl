import { useState } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery } from '@tanstack/react-query'
import { workOrdersApi, type WorkOrderPriority } from '../../../lib/api/workOrders'
import { customersApi } from '../../../lib/api/customers'
import { catalogApi, type CatalogItemKind } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect, FormTextarea } from '../../../components/ui/FormField'
import { CustomerPicker } from '../../../components/ui/EntityPickers'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'
import { labelCls } from '../../../lib/styles'


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

  const { data: customerDetail } = useQuery({
    queryKey: ['customer', selectedCustomerId],
    queryFn: () => customersApi.get(selectedCustomerId),
    enabled: !!selectedCustomerId,
  })

  const { data: catalogData } = useQuery({
    queryKey: ['catalog-active'],
    queryFn: () => catalogApi.search(null, null, true, 1, 100),
  })

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
            <div>
              {vehicles.length === 0 ? (
                <>
                  <span className={labelCls}>Vehículo *</span>
                  <p className="text-sm text-tc-text-muted">El cliente no tiene vehículos registrados.</p>
                </>
              ) : (
                <FormSelect
                  key={customerDetail?.id ?? 'loading'}
                  label="Vehículo"
                  requiredMark
                  error={errors.vehicleId?.message}
                  {...register('vehicleId')}
                >
                  <option value="">Seleccionar…</option>
                  {vehicles.map(v => (
                    <option key={v.id} value={v.id}>
                      {v.plate}{v.brand ? ` — ${v.brand}` : ''}{v.model ? ` ${v.model}` : ''}
                    </option>
                  ))}
                </FormSelect>
              )}
              
            </div>
          )}

          {/* Odómetro y combustible */}
          <div className="grid grid-cols-2 gap-4">
            <FormField
              label="Odómetro (km)"
              requiredMark
              type="number"
              min={0}
              placeholder="0"
              inputClassName="tabular-nums"
              error={errors.odometerKm?.message}
              {...register('odometerKm', { valueAsNumber: true })}
            />
            <FormField
              label="Combustible %"
              type="number"
              min={0}
              max={100}
              step={5}
              placeholder="—"
              inputClassName="tabular-nums"
              {...register('fuelLevel', { valueAsNumber: true, setValueAs: v => (v === '' || isNaN(v) ? null : v) })}
            />
          </div>

          {/* Prioridad */}
          <FormSelect label="Prioridad" {...register('priority')}>
            <option value="low">Baja</option>
            <option value="normal">Normal</option>
            <option value="high">Alta</option>
          </FormSelect>

          {/* Problema reportado */}
          <FormTextarea label="Problema reportado" rows={2} placeholder="Descripción del problema…" {...register('reportedIssue')} />

          {/* Renglones */}
          <div>
            <div className="mb-2 flex items-center justify-between">
              <span className="text-sm font-medium">Renglones</span>
              <button
                type="button"
                onClick={addItem}
                className="text-xs text-blue-600 hover:underline"
              >
                + Agregar
              </button>
            </div>
            {fields.length === 0 && (
              <p className="text-xs text-tc-text-muted">Sin renglones. La orden se crea sin ítems.</p>
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
  const catId = `wo-item-${index}-cat`

  return (
    <div className="border border-tc-border bg-tc-surface p-3">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-xs font-bold tracking-wider uppercase text-tc-text-muted">Renglón {index + 1}</span>
        <button type="button" onClick={onRemove} className="text-xs font-bold tracking-wider uppercase text-tc-stamp-red hover:opacity-70 transition-opacity">Quitar</button>
      </div>
      <div className="space-y-2">
        <FormSelect
          id={catId}
          label="Ítem de catálogo (opcional)"
          size="sm"
          onChange={e => { if (e.target.value) onSelectCatalog(index, e.target.value) }}
        >
          <option value="">— manual —</option>
          {catalogItems.map(c => (
            <option key={c.id} value={c.id}>{c.name}</option>
          ))}
        </FormSelect>
        <FormField
          label="Descripción"
          requiredMark
          size="sm"
          aria-label={`Descripción del renglón ${index + 1}`}
          error={errors.items?.[index]?.description?.message}
          {...register(`items.${index}.description`)}
        />
        <div className="grid grid-cols-3 gap-2">
          <FormField label="Cantidad" size="sm" type="number" min={0} step="1" inputClassName="tabular-nums" {...register(`items.${index}.quantity`, { valueAsNumber: true })} />
          <FormField label="Precio neto" size="sm" type="number" min={0} step="1" inputClassName="tabular-nums" {...register(`items.${index}.unitNetPrice`, { valueAsNumber: true })} />
          <FormField label="IVA %" size="sm" type="number" min={0} max={100} inputClassName="tabular-nums" {...register(`items.${index}.vatRate`, { valueAsNumber: true })} />
        </div>
      </div>
    </div>
  )
}
