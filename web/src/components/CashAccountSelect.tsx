import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { cashApi, type CashAccountKind } from '../lib/api/cash'
import { useCan } from '../lib/auth/permissions'
import { FormSelect } from './ui/FormField'

const STORAGE_KEY = 'tc_cash_account'
const KIND_LABELS: Record<CashAccountKind, string> = { cash: 'Efectivo', bank: 'Banco', digital: 'Digital' }

function readStored(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) ?? ''
  } catch {
    return ''
  }
}

/**
 * Cuenta de caja elegida para asentar un cobro, una compra o una venta. Se recuerda la última usada.
 * Devuelve `{ accountId, field }`: `field` es el selector listo para poner en el formulario (null si la sesión
 * no puede ver la caja) y `accountId` es null si no se eligió cuenta.
 */
export function useCashAccountChoice(label = 'Cuenta de caja') {
  const canRead = useCan()('cash.read')
  const [choice, setChoice] = useState(readStored)

  const { data: accounts } = useQuery({
    queryKey: ['cash-accounts'],
    queryFn: () => cashApi.listAccounts(),
    enabled: canRead,
  })
  const active = (accounts ?? []).filter(a => a.isActive)
  const valid = active.some(a => a.id === choice) ? choice : ''

  useEffect(() => {
    if (!accounts) return
    try {
      localStorage.setItem(STORAGE_KEY, valid)
    } catch {
      /* sin almacenamiento: no se recuerda */
    }
  }, [valid, accounts])

  const field = canRead ? (
    <FormSelect
      label={label}
      value={valid}
      onChange={e => setChoice(e.target.value)}
      hint={
        valid && !active.find(a => a.id === valid)?.sessionOpenedAt
          ? 'Esta cuenta no tiene la sesión abierta: igual se asienta el movimiento.'
          : 'Elegí dónde entra o sale el dinero para que la Caja quede al día.'
      }
    >
      <option value="">Sin cuenta de caja (no se asienta)</option>
      {active.map(a => (
        <option key={a.id} value={a.id}>{a.name} · {KIND_LABELS[a.kind]}</option>
      ))}
    </FormSelect>
  ) : null

  return { accountId: valid || null, field }
}
