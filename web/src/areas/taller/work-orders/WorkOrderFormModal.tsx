import { useState } from 'react'
import { useDefaultVat } from '../../../lib/auth/defaults'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery } from '@tanstack/react-query'
import { workOrdersApi, type WorkOrderDto, type WorkOrderPriority } from '../../../lib/api/workOrders'
import { customersApi } from '../../../lib/api/customers'
import type { CatalogItemListDto } from '../../../lib/api/catalog'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect, FormTextarea } from '../../../components/ui/FormField'
import { CatalogPicker, CustomerPicker } from '../../../components/ui/EntityPickers'
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
  internalNotes: z.string().trim().max(2000).nullable(),
  priority: z.enum(['low', 'normal', 'high']),
  items: z.array(itemSchema),
})

type FormData = z.infer<typeof schema>

interface Props {
  /** Si viene, el formulario edita esa orden (cliente y vehículo no se cambian). */
  order?: WorkOrderDto
  onSuccess: () => void
  onClose: () => void
}

function toFormData(order: WorkOrderDto): FormData {
  return {
    customerId: order.customerId,
    vehicleId: order.vehicleId,
    odometerKm: order.odometerKm,
    fuelLevel: order.fuelLevel,
    reportedIssue: order.reportedIssue,
    internalNotes: order.internalNotes,
    priority: order.priority,
    items: order.items.map(i => ({
      kind: i.kind,
      description: i.description,
      catalogItemId: i.catalogItemId,
      quantity: i.quantity,
      unitNetPrice: i.unitNetPrice ?? 0,
      vatRate: i.vatRate,
    })),
  }
}

export function WorkOrderFormModal({ order, onSuccess, onClose }: Props) {
  const defaultVat = useDefaultVat()
  const isEdit = !!order
  const [serverError, setServerError] = useState<string | null>(null)

  const { register, handleSubmit, watch, control, setValue, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: order
      ? toFormData(order)
      : {
          customerId: '',
          vehicleId: '',
          odometerKm: 0,
          fuelLevel: null,
          reportedIssue: null,
          internalNotes: null,
          priority: 'normal',
          items: [],
        },
  })

  const { fields, append, remove } = useFieldArray({ control, name: 'items' })

  const selectedCustomerId = watch('customerId')

  // Solo al crear: el mecánico se puede elegir desde el inicio (después se reasigna desde el detalle).
  const [mechanicId, setMechanicId] = useState('')
  const { data: mechanics } = useQuery({
    queryKey: ['work-order-mechanics'],
    queryFn: () => workOrdersApi.listMechanics(),
    staleTime: 60_000,
    enabled: !isEdit,
  })

  const { data: customerDetail } = useQuery({
    queryKey: ['customer', selectedCustomerId],
    queryFn: () => customersApi.get(selectedCustomerId),
    enabled: !!selectedCustomerId && !isEdit,
  })

  function handleCatalogItemSelect(index: number, item: CatalogItemListDto | null) {
    if (!item) {
      setValue(`items.${index}.catalogItemId`, null)
      return
    }
    setValue(`items.${index}.catalogItemId`, item.id)
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
      vatRate: defaultVat,
    })
  }

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      if (order) {
        await workOrdersApi.update(
          order.id,
          {
            odometerKm: data.odometerKm,
            fuelLevel: data.fuelLevel,
            reportedIssue: data.reportedIssue,
            internalNotes: data.internalNotes,
            priority: data.priority as WorkOrderPriority,
            assignedMechanicId: order.assignedMechanicId,
            items: data.items.map(item => ({
              kind: item.kind,
              description: item.description,
              catalogItemId: item.catalogItemId,
              quantity: item.quantity,
              unitNetPrice: item.unitNetPrice,
              vatRate: item.vatRate,
            })),
          },
          order.version,
        )
        onSuccess()
        return
      }
      await workOrdersApi.create({
        customerId: data.customerId,
        vehicleId: data.vehicleId,
        odometerKm: data.odometerKm,
        fuelLevel: data.fuelLevel,
        reportedIssue: data.reportedIssue,
        priority: data.priority as WorkOrderPriority,
        assignedMechanicId: mechanicId || null,
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
    <Modal title={order ? `Editar orden #${order.number}` : 'Nueva orden de trabajo'} onClose={onClose} className="max-w-2xl">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">

          {order && (
            <div className="border border-tc-border bg-tc-surface px-3 py-2 text-sm text-tc-text-secondary">
              <b className="text-tc-text">{order.customerName}</b> · <span className="font-mono tracking-widest">{order.vehiclePlate}</span>
              <span className="block text-xs text-tc-text-muted">El cliente y el vehículo no se pueden cambiar. Si te equivocaste, cancelá la orden y creá otra.</span>
            </div>
          )}

          {/* Cliente */}
          {!order && (
          <CustomerPicker
            value={selectedCustomerId}
            requiredMark
            error={errors.customerId?.message}
            onChange={id => {
              setValue('customerId', id, { shouldValidate: !!id })
              setValue('vehicleId', '')
            }}
          />
          )}

          {/* Vehículo */}
          {selectedCustomerId && !order && (
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

          {!isEdit && (mechanics?.length ?? 0) > 0 && (
            <FormSelect label="Mecánico" value={mechanicId} onChange={e => setMechanicId(e.target.value)}>
              <option value="">Sin asignar (lo toma un mecánico)</option>
              {mechanics!.map(m => (
                <option key={m.membershipId} value={m.membershipId}>{m.displayName}</option>
              ))}
            </FormSelect>
          )}

          {/* Problema reportado */}
          <FormTextarea label="Problema reportado" rows={2} placeholder="Descripción del problema…" {...register('reportedIssue')} />

          {isEdit && (
            <FormTextarea
              label="Notas internas"
              rows={2}
              placeholder="Solo las ve el taller…"
              hint="No salen en el PDF ni en el enlace del cliente."
              {...register('internalNotes')}
            />
          )}

          {/* Renglones */}
          <div>
            <div className="mb-2 flex items-center justify-between">
              <span className="text-sm font-medium">Renglones</span>
              <button
                type="button"
                onClick={addItem}
                className="text-xs font-bold uppercase tracking-wider text-tc-purple hover:opacity-70"
              >
                + Agregar
              </button>
            </div>
            {fields.length === 0 && (
              <p className="text-xs text-tc-text-muted">{isEdit ? 'Sin renglones.' : 'Sin renglones. La orden se crea sin ítems.'}</p>
            )}
            {isEdit && (
              <p className="mb-2 text-xs text-tc-text-muted">
                Al guardar, los productos con control de stock se ajustan solos: se repone lo que quitás y se descuenta lo que agregás.
              </p>
            )}
            <div className="space-y-3">
              {fields.map((field, index) => (
                <ItemRow
                  key={field.id}
                  index={index}
                  register={register}
                  errors={errors}
                  onSelectCatalog={handleCatalogItemSelect}
                  onRemove={() => remove(index)}
                />
              ))}
            </div>
          </div>

          {serverError && (
            <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
          )}

          <div className="sticky bottom-0 -mx-5 -mb-5 mt-2 flex justify-end gap-3 border-t border-tc-border bg-white px-5 py-3">
            <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
            <Button type="submit" size="sm" loading={isSubmitting}>{isEdit ? 'Guardar cambios' : 'Crear orden'}</Button>
          </div>
        </form>
    </Modal>
  )
}

// ── Sub-componente de renglón ──────────────────────────────────────────────────

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function ItemRow({ index, register, errors, onSelectCatalog, onRemove }: {
  index: number
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  register: any
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  errors: any
  onSelectCatalog: (index: number, item: CatalogItemListDto | null) => void
  onRemove: () => void
}) {
  return (
    <div className="border border-tc-border bg-tc-surface p-3">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-xs font-bold tracking-wider uppercase text-tc-text-muted">Renglón {index + 1}</span>
        <button type="button" onClick={onRemove} className="text-xs font-bold tracking-wider uppercase text-tc-stamp-red hover:opacity-70 transition-opacity">Quitar</button>
      </div>
      <div className="space-y-2">
        <CatalogPicker
          label="Ítem de catálogo (opcional)"
          size="sm"
          onPick={item => onSelectCatalog(index, item)}
        />
        <FormField
          label="Descripción"
          requiredMark
          size="sm"
          aria-label={`Descripción del renglón ${index + 1}`}
          error={errors.items?.[index]?.description?.message}
          {...register(`items.${index}.description`)}
        />
        <div className="grid grid-cols-3 gap-2">
          <FormField label="Cantidad" size="sm" type="number" min={0} step="0.001" inputClassName="tabular-nums" {...register(`items.${index}.quantity`, { valueAsNumber: true })} />
          <FormField label="Precio neto" size="sm" type="number" min={0} step="0.01" inputClassName="tabular-nums" {...register(`items.${index}.unitNetPrice`, { valueAsNumber: true })} />
          <FormField label="IVA %" size="sm" type="number" min={0} max={100} inputClassName="tabular-nums" {...register(`items.${index}.vatRate`, { valueAsNumber: true })} />
        </div>
      </div>
    </div>
  )
}
