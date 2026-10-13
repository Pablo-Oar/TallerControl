import { useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ALLOWED_LOGO_CONTENT_TYPES,
  ALLOWED_VAT_RATES,
  MAX_LOGO_SIZE_BYTES,
  settingsApi,
  type SettingsDto,
  type UpdateSettingsRequest,
} from '../../../../lib/api/settings'
import { Button } from '../../../../components/ui/Button'
import { ApiError } from '../../../../lib/api/client'

const schema = z.object({
  name: z.string().trim().min(1, 'El nombre es obligatorio'),
  legalName: z.string().trim(),
  taxId: z.string().trim(),
  address: z.string().trim(),
  phone: z.string().trim(),
  timeZone: z.string().trim().min(1, 'La zona horaria es obligatoria'),
  defaultVatRate: z.coerce.number(),
  quoteValidityDays: z.coerce.number().int().min(1, 'Mínimo 1 día').max(180, 'Máximo 180 días'),
})

// z.coerce.number() hace que el tipo de entrada (antes de validar) difiera del de salida: React
// Hook Form necesita los dos por separado para tipar bien register() y el callback de submit.
type FormInput = z.input<typeof schema>
type FormData = z.output<typeof schema>

function toFormData(dto: SettingsDto): FormInput {
  return {
    name: dto.name,
    legalName: dto.legalName ?? '',
    taxId: dto.taxId ?? '',
    address: dto.address ?? '',
    phone: dto.phone ?? '',
    timeZone: dto.timeZone,
    defaultVatRate: dto.defaultVatRate,
    quoteValidityDays: dto.quoteValidityDays,
  }
}

function toRequest(data: FormData): UpdateSettingsRequest {
  return {
    name: data.name,
    legalName: data.legalName || null,
    taxId: data.taxId || null,
    address: data.address || null,
    phone: data.phone || null,
    timeZone: data.timeZone,
    defaultVatRate: data.defaultVatRate,
    quoteValidityDays: data.quoteValidityDays,
  }
}

export function GeneralSettingsPage() {
  const queryClient = useQueryClient()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [logoError, setLogoError] = useState<string | null>(null)
  const [uploadingLogo, setUploadingLogo] = useState(false)

  const { data, isLoading, error } = useQuery({
    queryKey: ['settings'],
    queryFn: () => settingsApi.get(),
  })

  const { register, handleSubmit, reset, formState: { isDirty } } = useForm<FormInput, unknown, FormData>({
    resolver: zodResolver(schema),
    values: data ? toFormData(data) : undefined,
  })

  const saveMutation = useMutation({
    mutationFn: (formData: FormData) => settingsApi.update(toRequest(formData), data!.version),
    onSuccess: (dto) => {
      queryClient.setQueryData(['settings'], dto)
      reset(toFormData(dto))
    },
  })

  async function uploadLogo(file: File) {
    setLogoError(null)

    if (!ALLOWED_LOGO_CONTENT_TYPES.includes(file.type as (typeof ALLOWED_LOGO_CONTENT_TYPES)[number])) {
      setLogoError('El logo debe ser PNG, JPG o WebP.')
      return
    }
    if (file.size > MAX_LOGO_SIZE_BYTES) {
      setLogoError('El logo no puede superar los 5 MB.')
      return
    }

    setUploadingLogo(true)
    try {
      const { uploadUrl } = await settingsApi.requestLogoUploadUrl(file.type, file.size)
      const putResp = await fetch(uploadUrl, {
        method: 'PUT',
        headers: { 'Content-Type': file.type },
        body: file,
      })
      if (!putResp.ok) {
        throw new Error('No se pudo subir el archivo.')
      }

      const dto = await settingsApi.confirmLogo()
      queryClient.setQueryData(['settings'], dto)
    } catch (err) {
      setLogoError(err instanceof ApiError ? err.detail : 'No se pudo subir el logo. Probá de nuevo.')
    } finally {
      setUploadingLogo(false)
      if (fileInputRef.current) fileInputRef.current.value = ''
    }
  }

  if (isLoading) {
    return <div className="flex justify-center py-12"><div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" /></div>
  }

  if (error || !data) {
    return <p className="text-sm text-red-600">No se pudo cargar la configuración del taller.</p>
  }

  const saveError = saveMutation.error instanceof ApiError ? saveMutation.error.detail : null

  return (
    <div className="max-w-2xl">
      <h1 className="mb-6 text-2xl font-semibold">Configuración general</h1>

      <section className="mb-8 rounded-lg border border-slate-200 bg-white p-6">
        <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-slate-500">Logo</h2>
        <div className="flex items-center gap-4">
          <div className="flex h-20 w-36 items-center justify-center overflow-hidden rounded-md border border-dashed border-slate-300 bg-slate-50">
            {data.logoUrl ? (
              <img src={data.logoUrl} alt="Logo del taller" className="h-full w-full object-contain" />
            ) : (
              <span className="text-xs text-slate-400">Sin logo</span>
            )}
          </div>
          <div>
            <input
              ref={fileInputRef}
              type="file"
              accept={ALLOWED_LOGO_CONTENT_TYPES.join(',')}
              className="hidden"
              onChange={(e) => {
                const file = e.target.files?.[0]
                if (file) void uploadLogo(file)
              }}
            />
            <Button
              type="button"
              variant="secondary"
              loading={uploadingLogo}
              onClick={() => fileInputRef.current?.click()}
            >
              Cambiar logo
            </Button>
            <p className="mt-2 text-xs text-slate-500">PNG, JPG o WebP. Máximo 5 MB.</p>
            {logoError && <p role="alert" className="mt-2 text-xs text-red-600">{logoError}</p>}
          </div>
        </div>
      </section>

      <form
        onSubmit={handleSubmit((formData) => saveMutation.mutate(formData))}
        className="flex flex-col gap-4 rounded-lg border border-slate-200 bg-white p-6"
      >
        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium text-slate-700">Nombre del taller *</label>
          <input
            {...register('name')}
            className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-slate-700">Razón social</label>
            <input
              {...register('legalName')}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-slate-700">CUIT</label>
            <input
              {...register('taxId')}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-slate-700">Dirección</label>
            <input
              {...register('address')}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-slate-700">Teléfono</label>
            <input
              {...register('phone')}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>

        <div className="grid grid-cols-3 gap-4">
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-slate-700">Zona horaria</label>
            <input
              {...register('timeZone')}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-slate-700">IVA por defecto</label>
            <select
              {...register('defaultVatRate')}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            >
              {ALLOWED_VAT_RATES.map((rate) => (
                <option key={rate} value={rate}>{rate}%</option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-slate-700">Vigencia del presupuesto (días)</label>
            <input
              type="number"
              min={1}
              max={180}
              {...register('quoteValidityDays')}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>

        {saveError && (
          <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">
            {saveError}
          </p>
        )}

        <div className="flex justify-end pt-2">
          <Button type="submit" loading={saveMutation.isPending} disabled={!isDirty}>
            Guardar cambios
          </Button>
        </div>
      </form>
    </div>
  )
}
