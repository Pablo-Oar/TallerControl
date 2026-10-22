import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { vehiclesApi } from '../../../lib/api/vehicles'
import type { ConvertQuoteRequest } from '../../../lib/api/quotes'
import { Button } from '../../../components/ui/Button'
import { FormField, FormTextarea } from '../../../components/ui/FormField'
import { Modal } from '../../../components/ui/Modal'

interface Props {
  vehicleId: string
  vehiclePlate: string
  busy: boolean
  error: string | null
  onConfirm: (body: ConvertQuoteRequest) => void
  onClose: () => void
}

/** Datos de ingreso al convertir un presupuesto en orden: kilometraje (precargado con el del vehículo), combustible y problema. */
export function ConvertQuoteModal({ vehicleId, vehiclePlate, busy, error, onConfirm, onClose }: Props) {
  const { data: vehicle } = useQuery({ queryKey: ['vehicle', vehicleId], queryFn: () => vehiclesApi.get(vehicleId) })
  const [odometer, setOdometer] = useState<string | null>(null)
  const [fuel, setFuel] = useState('')
  const [issue, setIssue] = useState('')

  // Mientras el usuario no escribe, se muestra el kilometraje actual del vehículo.
  const shownOdometer = odometer ?? (vehicle ? String(vehicle.odometerKm) : '')
  const km = Number(shownOdometer)
  const invalid = shownOdometer.trim() === '' || Number.isNaN(km) || km < 0

  function submit(e: React.FormEvent) {
    e.preventDefault()
    if (invalid) return
    onConfirm({
      odometerKm: Math.round(km),
      fuelLevel: fuel.trim() === '' ? null : Number(fuel),
      reportedIssue: issue.trim() || null,
    })
  }

  return (
    <Modal title="Convertir en orden de trabajo" onClose={onClose} className="max-w-md">
      <form onSubmit={submit} className="space-y-4">
        <p className="text-sm text-tc-text-secondary">
          Se crea la orden de <b className="font-mono tracking-widest">{vehiclePlate}</b> con los renglones aprobados.
          Cargá los datos con los que entra el vehículo.
        </p>
        {error && <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}
        <FormField
          label="Odómetro (km)"
          requiredMark
          type="number"
          min={0}
          inputClassName="tabular-nums"
          value={shownOdometer}
          onChange={e => setOdometer(e.target.value)}
          hint={vehicle ? `Kilometraje registrado del vehículo: ${vehicle.odometerKm.toLocaleString('es-AR')} km.` : undefined}
        />
        <FormField
          label="Combustible %"
          type="number"
          min={0}
          max={100}
          step={5}
          inputClassName="tabular-nums"
          value={fuel}
          onChange={e => setFuel(e.target.value)}
        />
        <FormTextarea
          label="Problema reportado"
          rows={2}
          maxLength={1000}
          placeholder="Descripción del problema…"
          value={issue}
          onChange={e => setIssue(e.target.value)}
        />
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose} disabled={busy}>Cancelar</Button>
          <Button type="submit" size="sm" loading={busy} disabled={invalid}>Crear orden</Button>
        </div>
      </form>
    </Modal>
  )
}
