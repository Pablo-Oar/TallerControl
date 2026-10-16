import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  listCertificates,
  listPointsOfSale,
  createCertificate,
  uploadCertificate,
  testConnection,
  createPointOfSale,
  type FiscalCertificateDto,
  type FiscalPointOfSaleDto,
} from '../../../lib/api/invoicing'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../lib/api/client'

export function InvoicingSetupPage() {
  const queryClient = useQueryClient()

  const { data: certs, isLoading: certsLoading } = useQuery({
    queryKey: ['fiscal-certificates'],
    queryFn: listCertificates,
  })

  const { data: points, isLoading: pointsLoading } = useQuery({
    queryKey: ['fiscal-points-of-sale'],
    queryFn: listPointsOfSale,
  })

  return (
    <div className="space-y-8">
      <h1 className="text-xl font-semibold">Configuración ARCA</h1>

      <CertificatesSection
        certs={certs ?? []}
        loading={certsLoading}
        onUpdated={() => queryClient.invalidateQueries({ queryKey: ['fiscal-certificates'] })}
      />

      <PointsOfSaleSection
        points={points ?? []}
        certs={certs ?? []}
        loading={pointsLoading}
        onUpdated={() => queryClient.invalidateQueries({ queryKey: ['fiscal-points-of-sale'] })}
      />
    </div>
  )
}

function CertificatesSection({
  certs,
  loading,
  onUpdated,
}: {
  certs: FiscalCertificateDto[]
  loading: boolean
  onUpdated: () => void
}) {
  const [showForm, setShowForm] = useState(false)
  const [cuit, setCuit] = useState('')
  const [name, setName] = useState('')
  const [isPrimary, setIsPrimary] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [csrResult, setCsrResult] = useState<string | null>(null)

  const [uploadId, setUploadId] = useState<string | null>(null)
  const [certPem, setCertPem] = useState('')
  const [expiresAt, setExpiresAt] = useState('')
  const [uploading, setUploading] = useState(false)
  const [uploadError, setUploadError] = useState<string | null>(null)

  const [testingId, setTestingId] = useState<string | null>(null)
  const [testPosId, setTestPosId] = useState('')
  const [testResult, setTestResult] = useState<string | null>(null)

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setCsrResult(null)
    try {
      const result = await createCertificate({ cuit, name, isPrimary })
      setCsrResult(result.csrPem)
      onUpdated()
      setShowForm(false)
      setCuit('')
      setName('')
      setIsPrimary(false)
    } catch (err) {
      setError(err instanceof ApiError ? err.detail : 'Error al crear certificado')
    } finally {
      setSaving(false)
    }
  }

  async function handleUpload(e: React.FormEvent) {
    e.preventDefault()
    if (!uploadId) return
    setUploading(true)
    setUploadError(null)
    try {
      await uploadCertificate(uploadId, {
        certificatePem: certPem,
        expiresAt: new Date(expiresAt).toISOString(),
      })
      onUpdated()
      setUploadId(null)
      setCertPem('')
      setExpiresAt('')
    } catch (err) {
      setUploadError(err instanceof ApiError ? err.detail : 'Error al subir certificado')
    } finally {
      setUploading(false)
    }
  }

  async function handleTest(certId: string) {
    if (!testPosId) return
    setTestingId(certId)
    setTestResult(null)
    try {
      const result = await testConnection(certId, testPosId)
      setTestResult(
        result.success
          ? `✓ Conexión exitosa. Último número: ${result.lastInvoiceNumber ?? 0}`
          : `✗ Error: ${result.errorMessage}`
      )
    } catch {
      setTestResult('✗ Error al probar la conexión')
    } finally {
      setTestingId(null)
    }
  }

  return (
    <section>
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-base font-semibold">Certificados ARCA</h2>
        {certs.length < 3 && (
          <Button variant="primary" onClick={() => setShowForm(true)}>
            Nuevo certificado
          </Button>
        )}
      </div>

      {loading ? (
        <p className="text-sm text-slate-500">Cargando...</p>
      ) : certs.length === 0 ? (
        <p className="text-sm text-slate-500">No hay certificados registrados.</p>
      ) : (
        <div className="space-y-3">
          {certs.map(cert => (
            <div key={cert.id} className="rounded border border-slate-200 p-4">
              <div className="flex items-center justify-between">
                <div>
                  <span className="font-medium">{cert.name}</span>
                  <span className="ml-2 text-sm text-slate-500">{cert.cuit}</span>
                  {cert.isPrimary && (
                    <span className="ml-2 rounded bg-blue-100 px-1.5 py-0.5 text-xs text-blue-700">
                      Principal
                    </span>
                  )}
                  <span
                    className={`ml-2 rounded px-1.5 py-0.5 text-xs font-medium ${
                      cert.status === 'active'
                        ? 'bg-green-100 text-green-700'
                        : cert.status === 'pending'
                          ? 'bg-yellow-100 text-yellow-700'
                          : 'bg-red-100 text-red-700'
                    }`}
                  >
                    {cert.status}
                  </span>
                </div>
                <div className="flex gap-2">
                  {cert.status === 'pending' && (
                    <Button variant="secondary" onClick={() => setUploadId(cert.id)}>
                      Subir cert.
                    </Button>
                  )}
                </div>
              </div>

              {cert.status === 'pending' && (
                <details className="mt-3">
                  <summary className="cursor-pointer text-sm text-blue-600">
                    Ver CSR para presentar en ARCA
                  </summary>
                  <pre className="mt-2 max-h-40 overflow-auto rounded bg-slate-100 p-3 text-xs">
                    {cert.csrPem}
                  </pre>
                </details>
              )}
            </div>
          ))}
        </div>
      )}

      {/* Test de conexión */}
      {certs.some(c => c.isActive) && (
        <div className="mt-4 rounded border border-slate-200 p-4">
          <h3 className="mb-2 text-sm font-medium">Probar conexión ARCA</h3>
          <div className="flex gap-2">
            <input
              type="text"
              placeholder="ID del punto de venta"
              value={testPosId}
              onChange={e => setTestPosId(e.target.value)}
              className="flex-1 rounded border border-slate-300 px-3 py-1.5 text-sm"
            />
            <Button
              variant="secondary"
              loading={testingId !== null}
              disabled={!testPosId}
              onClick={() => {
                const activeCert = certs.find(c => c.isActive)
                if (activeCert) handleTest(activeCert.id)
              }}
            >
              Probar
            </Button>
          </div>
          {testResult && (
            <p className={`mt-2 text-sm ${testResult.startsWith('✓') ? 'text-green-600' : 'text-red-600'}`}>
              {testResult}
            </p>
          )}
        </div>
      )}

      {/* Modal nuevo certificado */}
      {showForm && (
        <div className="fixed inset-0 flex items-center justify-center bg-black/40 z-50">
          <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
            <h3 className="mb-4 text-base font-semibold">Nuevo certificado ARCA</h3>
            <form onSubmit={handleCreate} className="space-y-3">
              <Input
                label="CUIT (sin guiones)"
                value={cuit}
                onChange={e => setCuit(e.target.value)}
                placeholder="20123456789"
                required
              />
              <Input
                label="Nombre identificador"
                value={name}
                onChange={e => setName(e.target.value)}
                required
              />
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={isPrimary}
                  onChange={e => setIsPrimary(e.target.checked)}
                />
                Certificado principal
              </label>
              {error && <p className="text-sm text-red-600">{error}</p>}
              <div className="flex justify-end gap-2">
                <Button variant="secondary" type="button" onClick={() => setShowForm(false)}>
                  Cancelar
                </Button>
                <Button variant="primary" type="submit" loading={saving}>
                  Generar CSR
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal subir PEM */}
      {uploadId && (
        <div className="fixed inset-0 flex items-center justify-center bg-black/40 z-50">
          <div className="w-full max-w-lg rounded-lg bg-white p-6 shadow-xl">
            <h3 className="mb-4 text-base font-semibold">Subir certificado PEM</h3>
            <form onSubmit={handleUpload} className="space-y-3">
              <div>
                <label className="mb-1 block text-sm font-medium text-slate-700">
                  Certificado PEM (obtenido de ARCA)
                </label>
                <textarea
                  value={certPem}
                  onChange={e => setCertPem(e.target.value)}
                  className="h-32 w-full rounded border border-slate-300 p-2 font-mono text-xs"
                  placeholder="-----BEGIN CERTIFICATE-----&#10;...&#10;-----END CERTIFICATE-----"
                  required
                />
              </div>
              <Input
                label="Fecha de vencimiento"
                type="date"
                value={expiresAt}
                onChange={e => setExpiresAt(e.target.value)}
                required
              />
              {uploadError && <p className="text-sm text-red-600">{uploadError}</p>}
              <div className="flex justify-end gap-2">
                <Button variant="secondary" type="button" onClick={() => setUploadId(null)}>
                  Cancelar
                </Button>
                <Button variant="primary" type="submit" loading={uploading}>
                  Activar certificado
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {csrResult && (
        <div className="mt-4 rounded border border-blue-200 bg-blue-50 p-4">
          <p className="mb-2 text-sm font-medium text-blue-800">
            Certificado creado. Presentá este CSR en el portal ARCA para obtener el certificado firmado:
          </p>
          <pre className="max-h-40 overflow-auto rounded bg-white p-3 text-xs">
            {csrResult}
          </pre>
          <Button variant="secondary" className="mt-2" onClick={() => setCsrResult(null)}>
            Entendido
          </Button>
        </div>
      )}
    </section>
  )
}

function PointsOfSaleSection({
  points,
  certs,
  loading,
  onUpdated,
}: {
  points: FiscalPointOfSaleDto[]
  certs: FiscalCertificateDto[]
  loading: boolean
  onUpdated: () => void
}) {
  const [showForm, setShowForm] = useState(false)
  const [certId, setCertId] = useState('')
  const [number, setNumber] = useState('')
  const [name, setName] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await createPointOfSale({ fiscalCertificateId: certId, number: Number(number), name })
      onUpdated()
      setShowForm(false)
      setCertId('')
      setNumber('')
      setName('')
    } catch (err) {
      setError(err instanceof ApiError ? err.detail : 'Error al crear punto de venta')
    } finally {
      setSaving(false)
    }
  }

  return (
    <section>
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-base font-semibold">Puntos de Venta</h2>
        <Button variant="primary" onClick={() => setShowForm(true)}>
          Nuevo punto de venta
        </Button>
      </div>

      {loading ? (
        <p className="text-sm text-slate-500">Cargando...</p>
      ) : points.length === 0 ? (
        <p className="text-sm text-slate-500">No hay puntos de venta registrados.</p>
      ) : (
        <table className="min-w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs font-medium uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Nº</th>
              <th className="px-4 py-3">Nombre</th>
              <th className="px-4 py-3">Estado</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {points.map(pos => (
              <tr key={pos.id}>
                <td className="px-4 py-3 font-mono">{String(pos.number).padStart(5, '0')}</td>
                <td className="px-4 py-3">{pos.name}</td>
                <td className="px-4 py-3">
                  <span
                    className={`rounded px-1.5 py-0.5 text-xs font-medium ${
                      pos.isActive ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'
                    }`}
                  >
                    {pos.isActive ? 'Activo' : 'Inactivo'}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {showForm && (
        <div className="fixed inset-0 flex items-center justify-center bg-black/40 z-50">
          <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
            <h3 className="mb-4 text-base font-semibold">Nuevo punto de venta</h3>
            <form onSubmit={handleCreate} className="space-y-3">
              <div>
                <label className="mb-1 block text-sm font-medium text-slate-700">Certificado</label>
                <select
                  value={certId}
                  onChange={e => setCertId(e.target.value)}
                  className="w-full rounded border border-slate-300 px-3 py-2 text-sm"
                  required
                >
                  <option value="">Seleccionar certificado...</option>
                  {certs.map(c => (
                    <option key={c.id} value={c.id}>
                      {c.name} ({c.cuit})
                    </option>
                  ))}
                </select>
              </div>
              <Input
                label="Número de punto de venta"
                type="number"
                min="1"
                value={number}
                onChange={e => setNumber(e.target.value)}
                required
              />
              <Input
                label="Nombre"
                value={name}
                onChange={e => setName(e.target.value)}
                required
              />
              {error && <p className="text-sm text-red-600">{error}</p>}
              <div className="flex justify-end gap-2">
                <Button variant="secondary" type="button" onClick={() => setShowForm(false)}>
                  Cancelar
                </Button>
                <Button variant="primary" type="submit" loading={saving}>
                  Crear
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}
    </section>
  )
}
