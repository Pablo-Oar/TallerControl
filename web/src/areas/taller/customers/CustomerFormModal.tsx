import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { customersApi, type CustomerDto, type CustomerUpsertRequest, type DocType, type VatCondition } from '../../../lib/api/customers'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'

const VAT_OPTIONS: { value: VatCondition; label: string }[] = [
  { value: 'consumidor_final', label: 'Consumidor Final' },
  { value: 'responsable_inscripto', label: 'Responsable Inscripto' },
  { value: 'monotributo', label: 'Monotributo' },
  { value: 'exento', label: 'Exento' },
  { value: 'no_responsable', label: 'No Responsable' },
]

const DOC_OPTIONS: { value: DocType; label: string }[] = [
  { value: 'dni', label: 'DNI' },
  { value: 'cuit', label: 'CUIT' },
  { value: 'cuil', label: 'CUIL' },
  { value: 'pasaporte', label: 'Pasaporte' },
  { value: 'otro', label: 'Otro' },
]

const inputCls = 'w-full border border-tc-border bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors'

const schema = z.object({
  name: z.string().trim().min(1, 'Requerido').max(150),
  email: z.string().trim().max(200).optional(),
  phone: z.string().trim().max(50).optional(),
  docType: z.string().optional(),
  docNumber: z.string().trim().max(30).optional(),
  vatCondition: z.string().min(1),
  address: z.string().trim().max(200).optional(),
  doNotContact: z.boolean(),
  notes: z.string().trim().max(1000).optional(),
})

type FormData = z.infer<typeof schema>

function toFormData(dto: CustomerDto): FormData {
  return {
    name: dto.name,
    email: dto.email ?? '',
    phone: dto.phone ?? '',
    docType: dto.docType ?? '',
    docNumber: dto.docNumber ?? '',
    vatCondition: dto.vatCondition,
    address: dto.address ?? '',
    doNotContact: dto.doNotContact,
    notes: dto.notes ?? '',
  }
}

interface Props {
  customer?: CustomerDto
  onSuccess: (dto: CustomerDto) => void
  onClose: () => void
  onDelete?: () => void
}

export function CustomerFormModal({ customer, onSuccess, onClose, onDelete }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)
  const [deleting, setDeleting] = useState(false)
  const [deleteReason, setDeleteReason] = useState('')
  const [deleteLoading, setDeleteLoading] = useState(false)
  const isEdit = !!customer

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: customer
      ? toFormData(customer)
      : { vatCondition: 'consumidor_final', doNotContact: false },
  })

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      const body: CustomerUpsertRequest = {
        name: data.name,
        email: data.email || null,
        phone: data.phone || null,
        docType: (data.docType || null) as DocType | null,
        docNumber: data.docNumber || null,
        vatCondition: data.vatCondition as VatCondition,
        address: data.address || null,
        doNotContact: data.doNotContact,
        notes: data.notes || null,
      }
      const result = isEdit
        ? await customersApi.update(customer.id, body, customer.version)
        : await customersApi.create(body)
      onSuccess(result)
    } catch (e) {
      setServerError(e instanceof ApiError ? e.detail : 'Error inesperado')
    }
  }

  return (
    <Modal title={isEdit ? 'Editar cliente' : 'Nuevo cliente'} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <label htmlFor="cust-name" className="mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500">Nombre *</label>
          <input id="cust-name" {...register('name')} className={inputCls} placeholder="Nombre completo o razón social" />
          {errors.name && <p className="mt-1 text-xs text-red-600">{errors.name.message}</p>}
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="cust-email" className="mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500">Email</label>
            <input id="cust-email" {...register('email')} type="email" className={inputCls} placeholder="email@ejemplo.com" />
          </div>
          <div>
            <label htmlFor="cust-phone" className="mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500">Teléfono</label>
            <input id="cust-phone" {...register('phone')} className={inputCls} placeholder="011 15-0000-0000" />
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="cust-doc-type" className="mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500">Tipo de documento</label>
            <select id="cust-doc-type" {...register('docType')} className={inputCls}>
              <option value="">Sin documento</option>
              {DOC_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>
          <div>
            <label htmlFor="cust-doc-number" className="mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500">Número</label>
            <input id="cust-doc-number" {...register('docNumber')} className={inputCls} placeholder="20-12345678-9" />
          </div>
        </div>

        <div>
          <label htmlFor="cust-vat" className="mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500">Condición IVA</label>
          <select id="cust-vat" {...register('vatCondition')} className={inputCls}>
            {VAT_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
        </div>

        <div>
          <label htmlFor="cust-address" className="mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500">Dirección</label>
          <input id="cust-address" {...register('address')} className={inputCls} placeholder="Av. Ejemplo 1234, CABA" />
        </div>

        <div>
          <label htmlFor="cust-notes" className="mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500">Notas</label>
          <textarea id="cust-notes" {...register('notes')} rows={2} className={inputCls} placeholder="Observaciones internas…" />
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" {...register('doNotContact')} className="h-4 w-4" />
          No contactar
        </label>

        {serverError && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
        )}

        {deleting && (
          <div className="space-y-2 border border-red-200 bg-red-50 p-3">
            <p className="text-xs font-bold tracking-wider uppercase text-red-700">Confirmar eliminación</p>
            <input
              className="w-full border border-red-300 bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-red-400"
              placeholder="Motivo de eliminación (requerido)"
              value={deleteReason}
              onChange={e => setDeleteReason(e.target.value)}
              autoFocus
            />
            <div className="flex gap-2">
              <Button
                type="button"
                variant="danger"
                size="sm"
                loading={deleteLoading}
                disabled={!deleteReason.trim()}
                onClick={async () => {
                  setDeleteLoading(true)
                  try {
                    await customersApi.delete(customer!.id, deleteReason.trim())
                    onDelete?.()
                    onClose()
                  } catch (e) {
                    setServerError(e instanceof ApiError ? e.detail : 'Error al eliminar')
                    setDeleting(false)
                  } finally {
                    setDeleteLoading(false)
                  }
                }}
              >
                Eliminar definitivamente
              </Button>
              <Button type="button" variant="secondary" size="sm" onClick={() => setDeleting(false)}>Cancelar</Button>
            </div>
          </div>
        )}

        <div className="flex items-center justify-between pt-2">
          <div>
            {isEdit && onDelete && !deleting && (
              <Button type="button" variant="danger" size="sm" onClick={() => setDeleting(true)}>
                Eliminar
              </Button>
            )}
          </div>
          <div className="flex gap-3">
            <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
            <Button type="submit" size="sm" loading={isSubmitting}>Guardar</Button>
          </div>
        </div>
      </form>
    </Modal>
  )
}
