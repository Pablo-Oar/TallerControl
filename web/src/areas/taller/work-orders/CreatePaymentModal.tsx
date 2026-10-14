import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { createPayment, METHOD_LABELS, type PaymentMethod } from '../../../lib/api/payments'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../lib/api/client'

const schema = z.object({
  method: z.enum(['cash', 'transfer', 'debit', 'credit']),
  amount: z.number().positive('El monto debe ser mayor a 0'),
  reference: z.string().trim().max(255).optional(),
})

type FormData = z.infer<typeof schema>

interface Props {
  workOrderId: string
  balance: number
  onSuccess: () => void
  onClose: () => void
}

const inputCls =
  'w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500'

export function CreatePaymentModal({ workOrderId, balance, onSuccess, onClose }: Props) {
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      method: 'cash',
      amount: undefined,
      reference: '',
    },
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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-sm rounded-xl bg-white p-6 shadow-xl">
        <h2 className="mb-4 text-base font-semibold text-slate-800">Registrar cobro</h2>

        <p className="mb-4 text-sm text-slate-500">
          Saldo pendiente:{' '}
          <span className="font-semibold text-slate-800">
            ${balance.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
          </span>
        </p>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Método</label>
            <select {...register('method')} className={inputCls}>
              {(Object.entries(METHOD_LABELS) as [PaymentMethod, string][]).map(([v, l]) => (
                <option key={v} value={v}>{l}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Monto</label>
            <input
              type="number"
              step="0.01"
              {...register('amount', { valueAsNumber: true })}
              className={inputCls}
              placeholder="0.00"
            />
            {errors.amount && (
              <p className="mt-1 text-xs text-red-600">{errors.amount.message}</p>
            )}
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">
              Referencia <span className="text-slate-400">(opcional)</span>
            </label>
            <input
              type="text"
              {...register('reference')}
              className={inputCls}
              placeholder="N° de transferencia, etc."
            />
          </div>

          {serverError && (
            <p className="text-sm text-red-600">{serverError}</p>
          )}

          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="secondary" onClick={onClose} disabled={isSubmitting}>
              Cancelar
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Guardando…' : 'Registrar'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
