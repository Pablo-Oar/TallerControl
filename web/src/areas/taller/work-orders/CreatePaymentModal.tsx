import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { createPayment, METHOD_LABELS, type PaymentMethod } from '../../../lib/api/payments'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'

const schema = z.object({
  method: z.enum(['cash', 'transfer', 'debit', 'credit']),
  amount: z.number().positive('El monto debe ser mayor a 0'),
  reference: z.string().trim().max(255).optional(),
})

type FormData = z.infer<typeof schema>

const inputCls = 'w-full border border-tc-border bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors'
const labelCls = 'mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500'

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
        <span className="text-xs font-bold tracking-wider uppercase text-slate-400">Saldo pendiente</span>
        <span className="tabular-nums font-bold text-tc-purple">{fmtARS(balance)}</span>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <label htmlFor="pay-method" className={labelCls}>Método</label>
          <select id="pay-method" {...register('method')} className={inputCls}>
            {(Object.entries(METHOD_LABELS) as [PaymentMethod, string][]).map(([v, l]) => (
              <option key={v} value={v}>{l}</option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="pay-amount" className={labelCls}>Monto</label>
          <input
            id="pay-amount"
            type="number"
            step="0.01"
            {...register('amount', { valueAsNumber: true })}
            className={`${inputCls} tabular-nums`}
            placeholder="0.00"
          />
          {errors.amount && <p className="mt-1 text-xs text-tc-stamp-red">{errors.amount.message}</p>}
        </div>

        <div>
          <label htmlFor="pay-ref" className={labelCls}>Referencia <span className="normal-case font-normal text-slate-400">(opcional)</span></label>
          <input id="pay-ref" type="text" {...register('reference')} className={inputCls} placeholder="N° de transferencia, etc." />
        </div>

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
