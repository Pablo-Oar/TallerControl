import { useWatch, type Control, type FieldValues } from 'react-hook-form'
import { FormField } from './FormField'

const ars = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

/** Columnas de la fila Cantidad · Precio neto · IVA % · Total (la cantidad y el IVA son angostos). */
export const LINE_GRID_CLS = 'grid grid-cols-[5.5rem_minmax(0,1fr)_5.5rem_minmax(0,1fr)] gap-2'

interface Props {
  /** El `control` de cualquier formulario (la variancia de los genéricos de RHF no deja tiparlo más fino). */
  control: unknown
  /** Ruta del renglón, p. ej. `items.0`. */
  base: string
  /** Nombre del campo del precio neto dentro del renglón (`unitPrice` o `unitNetPrice`). */
  priceKey: string
}

/**
 * Total del renglón (cantidad × precio neto + IVA), solo lectura. Es una ayuda para quien carga:
 * el total que vale lo calcula siempre el servidor.
 */
export function LineTotalField({ control, base, priceKey }: Props) {
  const [quantity, price, vat] = useWatch<FieldValues>({
    control: control as Control<FieldValues>,
    name: [`${base}.quantity`, `${base}.${priceKey}`, `${base}.vatRate`],
  }) as unknown[]
  const q = Number(quantity)
  const p = Number(price)
  const v = Number(vat)
  const net = q * p
  const total = Number.isFinite(net) ? net * (1 + (Number.isFinite(v) ? v : 0) / 100) : 0

  return (
    <FormField
      label="Total"
      size="sm"
      readOnly
      tabIndex={-1}
      value={ars.format(total)}
      inputClassName="tabular-nums bg-tc-surface font-semibold"
    />
  )
}
