import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { customersApi, type CustomerDto, type CustomerUpsertRequest, type DocType, type VatCondition } from '../../../lib/api/customers'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect, FormTextarea } from '../../../components/ui/FormField'
import { EditDialog } from '../../../components/ui/EditDialog'
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

  const { register, handleSubmit, formState: { errors, isSubmitting, isDirty } } = useForm<FormData>({
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
    <EditDialog
      title={isEdit ? 'Editar cliente' : 'Nuevo cliente'}
      onClose={onClose}
      onSubmit={handleSubmit(onSubmit)}
      dirty={isDirty}
      saving={isSubmitting}
      error={serverError}
      requireChanges={isEdit}
      footerStart={
        isEdit && onDelete && !deleting ? (
          <Button type="button" variant="danger" size="sm" onClick={() => setDeleting(true)}>
            Eliminar
          </Button>
        ) : undefined
      }
    >
        <FormField
          label="Nombre"
          requiredMark
          placeholder="Nombre completo o razón social"
          error={errors.name?.message}
          {...register('name')}
        />

        <div className="grid grid-cols-2 gap-4">
          <FormField label="Email" type="email" placeholder="email@ejemplo.com" {...register('email')} />
          <FormField label="Teléfono" placeholder="011 15-0000-0000" {...register('phone')} />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <FormSelect label="Tipo de documento" {...register('docType')}>
            <option value="">Sin documento</option>
            {DOC_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </FormSelect>
          <FormField label="Número" placeholder="20-12345678-9" {...register('docNumber')} />
        </div>

        <FormSelect label="Condición IVA" {...register('vatCondition')}>
          {VAT_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </FormSelect>

        <FormField label="Dirección" placeholder="Av. Ejemplo 1234, CABA" {...register('address')} />

        <FormTextarea label="Notas" rows={2} placeholder="Observaciones internas…" {...register('notes')} />

        <label className="flex min-h-[44px] items-center gap-2 text-sm text-tc-text-secondary">
          <input type="checkbox" {...register('doNotContact')} className="h-4 w-4" />
          No contactar
        </label>


        {deleting && (
          <div className="space-y-2 border border-red-200 bg-red-50 p-3">
            <p className="text-xs font-bold tracking-wider uppercase text-red-700">Confirmar eliminación</p>
            <FormField
              label="Motivo de eliminación (requerido)"
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
    </EditDialog>
  )
}
