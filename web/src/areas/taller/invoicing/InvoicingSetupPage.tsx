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
import { FormField, FormSelect, FormTextarea } from '../../../components/ui/FormField'
import { Modal } from '../../../components/ui/Modal'
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
      <h1 className="text-xl font-bold tracking-widest text-slate-900 uppercase">Configuración ARCA</h1>

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
    <div className="space-y-4">
      <div className="border border-tc-border bg-white overflow-hidden">
        <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2 flex items-center justify-between">
          <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Certificados ARCA</span>
          {certs.length < 3 && (
            <Button size="sm" onClick={() => setShowForm(true)}>
              Nuevo certificado
            </Button>
          )}
        </div>

        <div className="p-4 space-y-3">
          {loading ? (
            <div className="space-y-2">
              {[...Array(2)].map((_, i) => (
                <div key={i} className="h-14 animate-pulse border border-tc-border bg-tc-surface" />
              ))}
            </div>
          ) : certs.length === 0 ? (
            <p className="text-sm text-slate-400">No hay certificados registrados.</p>
          ) : (
            <div className="space-y-2">
              {certs.map(cert => (
                <div key={cert.id} className="border border-tc-border bg-white overflow-hidden">
                  <div className="flex items-center justify-between px-4 py-3">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="font-medium text-sm text-slate-800">{cert.name}</span>
                      <span className="font-mono text-xs text-slate-500">{cert.cuit}</span>
                      {cert.isPrimary && (
                        <span className="border border-blue-300 px-1.5 py-0.5 text-[10px] font-bold tracking-wider uppercase text-blue-700">
                          Principal
                        </span>
                      )}
                      <span
                        className={`border px-1.5 py-0.5 text-[10px] font-bold tracking-wider uppercase ${
                          cert.status === 'active'
                            ? 'border-green-300 text-green-700'
                            : cert.status === 'pending'
                              ? 'border-amber-300 text-amber-700'
                              : 'border-red-300 text-red-700'
                        }`}
                      >
                        {cert.status === 'active' ? 'Activo' : cert.status === 'pending' ? 'Pendiente' : 'Inactivo'}
                      </span>
                    </div>
                    {cert.status === 'pending' && (
                      <Button size="sm" variant="secondary" onClick={() => setUploadId(cert.id)}>
                        Subir cert.
                      </Button>
                    )}
                  </div>

                  {cert.status === 'pending' && (
                    <details className="border-t border-tc-border">
                      <summary className="cursor-pointer px-4 py-2 text-xs font-bold tracking-wider uppercase text-tc-purple hover:bg-tc-surface">
                        Ver CSR para presentar en ARCA
                      </summary>
                      <pre className="border-t border-tc-border bg-tc-stub-bg px-4 py-3 text-xs font-mono max-h-40 overflow-auto">
                        {cert.csrPem}
                      </pre>
                    </details>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {certs.some(c => c.isActive) && (
        <div className="border border-tc-border bg-white overflow-hidden">
          <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
            <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Probar conexión ARCA</span>
          </div>
          <div className="p-4 space-y-2">
            <div className="flex gap-2">
              <FormField
                label="ID del punto de venta"
                size="sm"
                type="text"
                value={testPosId}
                onChange={e => setTestPosId(e.target.value)}
                className="flex-1"
              />
              <Button
                variant="secondary"
                size="sm"
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
              <p className={`text-sm ${testResult.startsWith('✓') ? 'text-green-700' : 'text-tc-stamp-red'}`}>
                {testResult}
              </p>
            )}
          </div>
        </div>
      )}

      {csrResult && (
        <div className="border border-tc-border bg-white overflow-hidden">
          <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
            <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">CSR generado</span>
          </div>
          <div className="p-4 space-y-2">
            <p className="text-sm text-slate-700">
              Presentá este CSR en el portal ARCA para obtener el certificado firmado:
            </p>
            <pre className="border border-tc-border bg-tc-stub-bg px-3 py-2 text-xs font-mono max-h-40 overflow-auto">
              {csrResult}
            </pre>
            <Button size="sm" variant="secondary" onClick={() => setCsrResult(null)}>
              Entendido
            </Button>
          </div>
        </div>
      )}

      {showForm && (
        <Modal title="Nuevo certificado ARCA" onClose={() => setShowForm(false)} className="max-w-md">
          <form onSubmit={handleCreate} className="space-y-3">
            <FormField
              label="CUIT (sin guiones)"
              value={cuit}
              onChange={e => setCuit(e.target.value)}
              placeholder="20123456789"
              required
              inputClassName="tabular-nums"
            />
            <FormField
              label="Nombre identificador"
              value={name}
              onChange={e => setName(e.target.value)}
              required
            />
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input
                type="checkbox"
                checked={isPrimary}
                onChange={e => setIsPrimary(e.target.checked)}
                className="accent-tc-purple"
              />
              Certificado principal
            </label>
            {error && <p className="text-sm text-tc-stamp-red">{error}</p>}
            <div className="flex justify-end gap-2">
              <Button variant="secondary" type="button" onClick={() => setShowForm(false)}>
                Cancelar
              </Button>
              <Button type="submit" loading={saving}>
                Generar CSR
              </Button>
            </div>
          </form>
        </Modal>
      )}

      {uploadId && (
        <Modal title="Subir certificado PEM" onClose={() => setUploadId(null)} className="max-w-lg">
          <form onSubmit={handleUpload} className="space-y-3">
            <FormTextarea
              label="Certificado PEM (obtenido de ARCA)"
              rows={6}
              value={certPem}
              onChange={e => setCertPem(e.target.value)}
              inputClassName="font-mono text-xs"
              placeholder="-----BEGIN CERTIFICATE-----&#10;...&#10;-----END CERTIFICATE-----"
              required
            />
            <FormField
              label="Fecha de vencimiento"
              type="date"
              value={expiresAt}
              onChange={e => setExpiresAt(e.target.value)}
              required
            />
            {uploadError && <p className="text-sm text-tc-stamp-red">{uploadError}</p>}
            <div className="flex justify-end gap-2">
              <Button variant="secondary" type="button" onClick={() => setUploadId(null)}>
                Cancelar
              </Button>
              <Button type="submit" loading={uploading}>
                Activar certificado
              </Button>
            </div>
          </form>
        </Modal>
      )}
    </div>
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
    <div className="border border-tc-border bg-white overflow-hidden">
      <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2 flex items-center justify-between">
        <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Puntos de Venta</span>
        <Button size="sm" onClick={() => setShowForm(true)}>
          Nuevo punto de venta
        </Button>
      </div>

      <div className="p-4">
        {loading ? (
          <div>
            {[...Array(3)].map((_, i) => (
              <div key={i} className="h-10 animate-pulse border-b border-tc-border bg-tc-surface last:border-0" />
            ))}
          </div>
        ) : points.length === 0 ? (
          <p className="text-sm text-slate-400">No hay puntos de venta registrados.</p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-tc-border">
                <th className="pb-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Nº</th>
                <th className="pb-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Nombre</th>
                <th className="pb-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Estado</th>
              </tr>
            </thead>
            <tbody>
              {points.map(pos => (
                <tr key={pos.id} className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
                  <td className="py-2.5 pr-4 font-mono text-slate-800 tabular-nums">{String(pos.number).padStart(5, '0')}</td>
                  <td className="py-2.5 pr-4 text-slate-700">{pos.name}</td>
                  <td className="py-2.5">
                    <span
                      className={`border px-1.5 py-0.5 text-[10px] font-bold tracking-wider uppercase ${
                        pos.isActive ? 'border-green-300 text-green-700' : 'border-tc-border text-slate-400'
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
      </div>

      {showForm && (
        <Modal title="Nuevo punto de venta" onClose={() => setShowForm(false)} className="max-w-md">
          <form onSubmit={handleCreate} className="space-y-3">
            <FormSelect label="Certificado" value={certId} onChange={e => setCertId(e.target.value)} required>
              <option value="">Seleccionar certificado...</option>
              {certs.map(c => (
                <option key={c.id} value={c.id}>
                  {c.name} ({c.cuit})
                </option>
              ))}
            </FormSelect>
            <FormField
              label="Número de punto de venta"
              type="number"
              min="1"
              value={number}
              onChange={e => setNumber(e.target.value)}
              required
              inputClassName="tabular-nums"
            />
            <FormField label="Nombre" value={name} onChange={e => setName(e.target.value)} required />
            {error && <p className="text-sm text-tc-stamp-red">{error}</p>}
            <div className="flex justify-end gap-2">
              <Button variant="secondary" type="button" onClick={() => setShowForm(false)}>
                Cancelar
              </Button>
              <Button type="submit" loading={saving}>
                Crear
              </Button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  )
}
