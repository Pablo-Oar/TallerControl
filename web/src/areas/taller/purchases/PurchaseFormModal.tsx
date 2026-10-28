import { useState } from 'react'
import { useDefaultVat } from '../../../lib/auth/defaults'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery } from '@tanstack/react-query'
import { purchasesApi, type PurchaseDto } from '../../../lib/api/purchases'
import { suppliersApi } from '../../../lib/api/suppliers'
import type { CatalogItemListDto } from '../../../lib/api/catalog'
import { CatalogPicker } from '../../../components/ui/EntityPickers'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect } from '../../../components/ui/FormField'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'
import { labelCls } from '../../../lib/styles'
import { dateOnlyToUtcIso, localDateISO } from '../../../lib/dates'


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
  items: z.array(itemSchema).min(1, 'Agregá al menos un ítem'),
})

type FormInput = z.input<typeof schema>
type FormData = z.output<typeof schema>

interface Props {
  /** Con una compra en borrador, el formulario la edita. */
  purchase?: PurchaseDto
  onSuccess: (dto: PurchaseDto) => void
  onClose: () => void
}

export function PurchaseFormModal({ purchase, onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)
  const isEdit = !!purchase
  const defaultVat = useDefaultVat()

  const { data: suppliersPage } = useQuery({
    queryKey: ['suppliers-picker'],
    queryFn: () => suppliersApi.search(null, 1, 100),
  })

  const { register, handleSubmit, control, setValue, watch, formState: { errors, isSubmitting } } = useForm<FormInput, unknown, FormData>({
    resolver: zodResolver(schema),
    defaultValues: purchase
      ? {
          supplierId: purchase.supplierId,
          invoiceDate: purchase.invoiceDate.slice(0, 10),
          pointOfSale: purchase.pointOfSale ?? '',
          invoiceNumber: purchase.invoiceNumber ?? '',
          perceptions: purchase.perceptions,
          retentions: purchase.retentions,
          items: purchase.items.map(i => ({
            catalogItemId: i.catalogItemId ?? '',
            description: i.description,
            quantity: i.quantity,
            unitCost: i.unitCost,
            vatRate: i.vatRate,
          })),
        }
      : {
      supplierId: '',
      invoiceDate: localDateISO(),
      pointOfSale: '',
      invoiceNumber: '',
      perceptions: 0,
      retentions: 0,
      items: [{ catalogItemId: '', description: '', quantity: 1, unitCost: 0, vatRate: defaultVat }],
    },
  })

  const { fields, append, remove } = useFieldArray({ control, name: 'items' })

  // Sin punto de venta y número de factura la compra no se puede informar al CITI (se avisa, no se elige).
  const pointOfSale = watch('pointOfSale')
  const invoiceNumber = watch('invoiceNumber')
  const missingFiscalData = [pointOfSale, invoiceNumber].some(v => v === '' || v === undefined || v === null)

  function handleCatalogSelect(index: number, item: CatalogItemListDto | null) {
    setValue(`items.${index}.catalogItemId`, item?.id ?? '')
    if (item) {
      setValue(`items.${index}.description`, item.name)
      setValue(`items.${index}.unitCost`, String(item.netPrice) as unknown as number)
      setValue(`items.${index}.vatRate`, String(item.vatRate) as unknown as number)
    }
  }

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      const body = {
        supplierId: data.supplierId,
        invoiceDate: dateOnlyToUtcIso(data.invoiceDate),
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
      }
      const result = purchase
        ? await purchasesApi.update(purchase.id, { ...body, version: purchase.version })
        : await purchasesApi.create(body)
      onSuccess(result)
    } catch (e) {
      setServerError(e instanceof ApiError ? e.detail : 'Error inesperado')
    }
  }

  return (
    <Modal title={isEdit ? `Editar compra #${purchase.documentNumber}` : 'Nueva compra'} onClose={onClose} className="max-w-2xl">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <FormSelect
            key={suppliersPage ? 'loaded' : 'loading'}
            label="Proveedor"
            requiredMark
            error={errors.supplierId?.message}
            disabled={isEdit}
            {...register('supplierId')}
          >
            <option value="">Elegir proveedor…</option>
            {suppliersPage?.items.map(s => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </FormSelect>
          <FormField label="Fecha de factura" requiredMark type="date" error={errors.invoiceDate?.message} {...register('invoiceDate')} />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <FormField label="Punto de venta" type="number" placeholder="Ej. 1" min={1} {...register('pointOfSale')} />
          <FormField label="Nro. de factura" type="number" placeholder="Ej. 1234" min={1} {...register('invoiceNumber')} />
        </div>

        {/* Ítems */}
        <div>
          <div className="mb-2 flex items-center justify-between">
            <span className={labelCls}>Ítems *</span>
            <button
              type="button"
              onClick={() => append({ catalogItemId: '', description: '', quantity: '1' as unknown as number, unitCost: '0' as unknown as number, vatRate: String(defaultVat) as unknown as number })}
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
              <div key={field.id} className="grid grid-cols-3 gap-2 items-start border border-tc-border bg-tc-stub-bg p-2 sm:grid-cols-[minmax(0,1fr)_5rem_6rem_4.5rem_2rem]">
                <div className="col-span-3 space-y-1.5 sm:col-span-1">
                  <CatalogPicker
                    label="Del catálogo"
                    kind="product"
                    size="sm"
                    onPick={item => handleCatalogSelect(index, item)}
                  />
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
                <div className="col-span-3 flex items-center justify-end sm:col-span-1 sm:justify-center sm:pt-1">
                  {fields.length > 1 && (
                    <button
                      type="button"
                      onClick={() => remove(index)}
                      aria-label="Quitar ítem"
                      className="min-h-[44px] px-3 text-xs text-tc-text-muted hover:text-red-600 transition-colors sm:min-h-0 sm:px-0"
                    >
                      <span className="sm:hidden">Quitar ítem </span>✕
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

        {missingFiscalData && (
          <p className="border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
            Sin punto de venta y número de factura la compra queda fuera del CITI. Podés completarlos ahora o más tarde, mientras sea un borrador.
          </p>
        )}

        {serverError && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
        )}

        <div className="sticky -bottom-5 -mx-5 -mb-5 mt-2 flex justify-end gap-3 border-t border-tc-border bg-white px-5 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] shadow-[0_-6px_8px_-6px_rgb(0_0_0/0.12)]">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
          <Button type="submit" size="sm" loading={isSubmitting}>{isEdit ? 'Guardar cambios' : 'Guardar como borrador'}</Button>
        </div>
      </form>
    </Modal>
  )
}
