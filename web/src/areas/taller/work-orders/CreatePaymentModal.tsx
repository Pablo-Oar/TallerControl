import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { createPayment, METHOD_LABELS, type PaymentMethod } from '../../../lib/api/payments'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect } from '../../../components/ui/FormField'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'

const schema = z.object({
  method: z.enum(['cash', 'transfer', 'debit', 'credit']),
  amount: z.number().positive('El monto debe ser mayor a 0'),
  reference: z.string().trim().max(255).optional(),
})

type FormData = z.infer<typeof schema>


const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

interface Props {
  workOrderId: string
  balance: number
  onSuccess: () => void
  onClose: () => void
}

export function CreatePaymentModal({ workOrderId, balance, onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { method: 'cash', amount: undefined, reference: '' },
  })

  async function onSubmit(data: FormData) {
    setServerError(null)
    try {
      await createPayment(workOrderId, {
        method: data.method,
        amount: data.amount,
        reference: data.reference || undefined,
      })
      onSuccess()
    } catch (e) {
      setServerError(e instanceof ApiError ? e.detail : 'Error inesperado')
    }
  }

  return (
    <Modal title="Registrar cobro" onClose={onClose} className="max-w-sm">
      <div className="mb-4 flex items-center justify-between border border-tc-border bg-tc-stub-bg px-4 py-2.5">
        <span className="text-xs font-bold tracking-wider uppercase text-tc-text-muted">Saldo pendiente</span>
        <span className="tabular-nums font-bold text-tc-purple">{fmtARS(balance)}</span>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <FormSelect label="Método" {...register('method')}>
          {(Object.entries(METHOD_LABELS) as [PaymentMethod, string][]).map(([v, l]) => (
            <option key={v} value={v}>{l}</option>
          ))}
        </FormSelect>

        <FormField
          label="Monto"
          type="number"
          step="0.01"
          placeholder="0.00"
          inputClassName="tabular-nums"
          error={errors.amount?.message}
          {...register('amount', { valueAsNumber: true })}
        />

        <FormField label="Referencia (opcional)" type="text" placeholder="N° de transferencia, etc." {...register('reference')} />

        {serverError && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{serverError}</p>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose} disabled={isSubmitting}>Cancelar</Button>
          <Button type="submit" size="sm" loading={isSubmitting}>Registrar</Button>
        </div>
      </form>
    </Modal>
  )
}
