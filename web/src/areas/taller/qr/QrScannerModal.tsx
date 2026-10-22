import { useState } from 'react'
import { Link } from 'react-router'
import { getVehicleSummaryByCode, type QrVehicleSummaryDto } from '../../../lib/api/qrCards'
import { Button } from '../../../components/ui/Button'
import { FormField } from '../../../components/ui/FormField'
import { Modal } from '../../../components/ui/Modal'
import { StatusBadge } from '../../../components/ui/StatusBadge'

interface Props {
  onClose: () => void
}

const STATUS_LABELS: Record<string, string> = {
  free: 'Libre',
  assigned: 'Asignada',
  lost: 'Reportada perdida',
  revoked: 'Revocada',
}

export function QrScannerModal({ onClose }: Props) {
  const [code, setCode] = useState('')
  const [loading, setLoading] = useState(false)
  const [result, setResult] = useState<QrVehicleSummaryDto | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    const trimmed = code.trim().toUpperCase()
    if (!trimmed) return
    setLoading(true)
    setError(null)
    setResult(null)
    try {
      const dto = await getVehicleSummaryByCode(trimmed)
      setResult(dto)
    } catch {
      setError('No se encontró ningún vehículo para ese código en este taller.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal title="Buscar vehículo por código QR" onClose={onClose} className="max-w-md">
      <form onSubmit={handleSearch} className="flex items-start gap-2">
        <FormField
          label="Código QR"
          type="text"
          value={code}
          onChange={(e) => setCode(e.target.value.toUpperCase())}
          placeholder="Ej. K3M9T2XQ7R"
          inputClassName="font-mono uppercase tracking-widest"
          className="flex-1"
          data-autofocus
        />
        <Button type="submit" loading={loading} disabled={!code.trim()} className="mt-0.5 h-[46px]">
          Buscar
        </Button>
      </form>

      {error && (
        <p role="alert" className="mt-3 border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>
      )}

      {result && (
        <div className="mt-4 space-y-2 border border-tc-border bg-tc-surface p-4">
          <div className="flex items-center justify-between">
            <span className="font-mono font-semibold tracking-widest text-tc-text">{result.code}</span>
            <StatusBadge status={result.status} label={(STATUS_LABELS[result.status] ?? result.status).toUpperCase()} />
          </div>

          {result.vehicleId ? (
            <>
              <p className="text-sm text-tc-text-secondary">
                <span className="font-medium">Patente:</span>{' '}
                <span className="font-mono">{result.vehiclePlate ?? '—'}</span>
              </p>
              <p className="text-sm text-tc-text-secondary">
                <span className="font-medium">Vehículo:</span>{' '}
                {[result.vehicleBrand, result.vehicleModel].filter(Boolean).join(' ') || '—'}
              </p>
              <p className="text-sm text-tc-text-secondary">
                <span className="font-medium">Cliente:</span>{' '}
                {result.customerName ?? '—'}
              </p>
              <div className="pt-2">
                <Link
                  to={result.vehicleId ? `/app/vehicles/${result.vehicleId}` : `/app/vehicles?q=${encodeURIComponent(result.vehiclePlate ?? '')}`}
                  className="text-sm font-medium text-tc-purple hover:text-tc-purple-mid"
                  onClick={onClose}
                >
                  Ver ficha del vehículo →
                </Link>
              </div>
            </>
          ) : (
            <p className="text-sm text-tc-text-secondary">Esta tarjeta no tiene vehículo asignado.</p>
          )}
        </div>
      )}
    </Modal>
  )
}
