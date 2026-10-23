import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { vehiclesApi } from '../../../lib/api/vehicles'
import { Button } from '../../../components/ui/Button'
import { FormField } from '../../../components/ui/FormField'
import { Modal } from '../../../components/ui/Modal'

interface Props {
  vehicleId: string
  vehiclePlate: string
  busy: boolean
  error: string | null
  onConfirm: (odometerKm: number) => void
  onClose: () => void
}

/** Crear la orden de un turno: el kilometraje es obligatorio (se precarga con el del vehículo). */
export function ConvertAppointmentModal({ vehicleId, vehiclePlate, busy, error, onConfirm, onClose }: Props) {
  const { data: vehicle } = useQuery({ queryKey: ['vehicle', vehicleId], queryFn: () => vehiclesApi.get(vehicleId) })
  const [typed, setTyped] = useState<string | null>(null)

  const shown = typed ?? (vehicle && vehicle.odometerKm > 0 ? String(vehicle.odometerKm) : '')
  const km = Number(shown)
  const invalid = shown.trim() === '' || Number.isNaN(km) || km <= 0

  return (
    <Modal title="Crear la orden de trabajo" onClose={onClose} className="max-w-md">
      <form
        onSubmit={e => {
          e.preventDefault()
          if (!invalid) onConfirm(Math.round(km))
        }}
        className="space-y-4"
      >
        <p className="text-sm text-tc-text-secondary">
          Llegó el vehículo <b className="font-mono tracking-widest">{vehiclePlate}</b>: se crea su orden con los servicios del
          turno y el turno pasa a Completado.
        </p>
        {error && <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}
        <FormField
          label="Odómetro (km)"
          requiredMark
          type="number"
          min={1}
          inputClassName="tabular-nums"
          value={shown}
          onChange={e => setTyped(e.target.value)}
          hint={vehicle ? `Kilometraje registrado del vehículo: ${vehicle.odometerKm.toLocaleString('es-AR')} km.` : undefined}
        />
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Volver</Button>
          <Button type="submit" size="sm" loading={busy} disabled={invalid}>Crear orden</Button>
        </div>
      </form>
    </Modal>
  )
}
