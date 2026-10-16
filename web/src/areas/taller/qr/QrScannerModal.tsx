import { useState } from 'react'
import { Link } from 'react-router'
import { getVehicleSummaryByCode, type QrVehicleSummaryDto } from '../../../lib/api/qrCards'
import { Button } from '../../../components/ui/Button'

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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold">Buscar vehículo por código QR</h2>
          <button className="text-slate-400 hover:text-slate-600 text-xl" onClick={onClose}>
            ×
          </button>
        </div>

        <form onSubmit={handleSearch} className="flex gap-2">
          <input
            autoFocus
            type="text"
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            placeholder="Código QR (Ej. ABC12345)"
            className="flex-1 rounded border border-slate-300 px-3 py-2 text-sm font-mono uppercase tracking-widest outline-none focus:ring-2 focus:ring-blue-500"
          />
          <Button type="submit" loading={loading} disabled={!code.trim()}>
            Buscar
          </Button>
        </form>

        {error && (
          <p className="mt-3 text-sm text-red-600">{error}</p>
        )}

        {result && (
          <div className="mt-4 rounded border border-slate-200 bg-slate-50 p-4 space-y-2">
            <div className="flex items-center justify-between">
              <span className="font-mono font-semibold tracking-widest text-slate-800">
                {result.code}
              </span>
              <span className={`rounded px-2 py-0.5 text-xs font-medium ${
                result.status === 'assigned' ? 'bg-green-100 text-green-800' :
                result.status === 'lost' ? 'bg-amber-100 text-amber-800' :
                'bg-slate-100 text-slate-600'
              }`}>
                {STATUS_LABELS[result.status] ?? result.status}
              </span>
            </div>

            {result.vehicleId ? (
              <>
                <p className="text-sm text-slate-700">
                  <span className="font-medium">Patente:</span>{' '}
                  <span className="font-mono">{result.vehiclePlate ?? '—'}</span>
                </p>
                <p className="text-sm text-slate-700">
                  <span className="font-medium">Vehículo:</span>{' '}
                  {[result.vehicleBrand, result.vehicleModel].filter(Boolean).join(' ') || '—'}
                </p>
                <p className="text-sm text-slate-700">
                  <span className="font-medium">Cliente:</span>{' '}
                  {result.customerName ?? '—'}
                </p>
                <div className="pt-2">
                  <Link
                    to={`/app/vehicles?q=${encodeURIComponent(result.vehiclePlate ?? '')}`}
                    className="text-sm text-blue-600 hover:underline"
                    onClick={onClose}
                  >
                    Ver ficha del vehículo →
                  </Link>
                </div>
              </>
            ) : (
              <p className="text-sm text-slate-500">Esta tarjeta no tiene vehículo asignado.</p>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
