import { useRef, useState } from 'react' // useRef: limpiar value del input tras subir
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
import { ApiError } from '../../../../lib/api/client'
import { FormField, FormSelect } from '../../../../components/ui/FormField'

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

const LOGO_PICKER_TYPES = [{ description: 'Imágenes', accept: { 'image/png': ['.png'], 'image/jpeg': ['.jpg', '.jpeg'], 'image/webp': ['.webp'] } }]

export function GeneralSettingsPage() {
  const queryClient = useQueryClient()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [logoError, setLogoError] = useState<string | null>(null)
  const [uploadingLogo, setUploadingLogo] = useState(false)

  async function handlePickLogo() {
    if ('showOpenFilePicker' in window) {
      try {
        const [handle] = await (window as Window & { showOpenFilePicker: (o: unknown) => Promise<FileSystemFileHandle[]> })
          .showOpenFilePicker({ types: LOGO_PICKER_TYPES, multiple: false })
        const file = await handle.getFile()
        void uploadLogo(file)
        return
      } catch (e) {
        if (e instanceof DOMException && e.name === 'AbortError') return // usuario canceló
        // Si la API fue bloqueada (SecurityError, NotAllowedError, etc.) → fallback
        console.warn('[TallerControl] showOpenFilePicker bloqueado, usando <input>:', e)
      }
    }
    fileInputRef.current?.click()
  }

  const { data, isLoading, error } = useQuery({
    queryKey: ['settings'],
    queryFn: () => settingsApi.get(),
  })

  const { register, handleSubmit, reset, formState: { isDirty, errors } } = useForm<FormInput, unknown, FormData>({
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
      if (!putResp.ok) throw new Error('No se pudo subir el archivo.')

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
    return (
      <div className="flex justify-center py-16">
        <div className="h-6 w-6 animate-spin border-2 border-tc-purple border-t-transparent" />
      </div>
    )
  }

  if (error || !data) {
    return (
      <p className="border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
        No se pudo cargar la configuración del taller.
      </p>
    )
  }

  const saveError = saveMutation.error instanceof ApiError ? saveMutation.error.detail : null

  return (
    <div className="max-w-4xl space-y-6">
      <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Configuración general</h1>

      {/* Logo */}
      <section className="border border-tc-border bg-white">
        <div className="bg-tc-stub-bg border-b border-tc-border px-5 py-2.5">
          <span className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Logo del taller</span>
        </div>
        <div className="flex items-center gap-5 p-5">
          <div className="flex h-20 w-36 shrink-0 items-center justify-center border border-dashed border-tc-border-mid bg-tc-stub-bg">
            {data.logoUrl ? (
              <img src={data.logoUrl} alt="Logo del taller" className="h-full w-full object-contain" />
            ) : (
              <span className="text-xs text-tc-text-muted">Sin logo</span>
            )}
          </div>
          <div className="space-y-2">
            {/* input oculto: fallback para browsers sin showOpenFilePicker */}
            <input
              ref={fileInputRef}
              type="file"
              accept={ALLOWED_LOGO_CONTENT_TYPES.join(',')}
              className="sr-only"
              onChange={(e) => {
                const file = e.target.files?.[0]
                if (file) void uploadLogo(file)
              }}
            />
            <button
              type="button"
              disabled={uploadingLogo}
              onClick={handlePickLogo}
              title="Seleccionar archivo de imagen"
              className="inline-flex cursor-pointer items-center gap-2 border border-tc-border bg-white px-4 py-2 text-sm font-medium text-tc-text-secondary transition-colors hover:bg-tc-surface disabled:cursor-not-allowed disabled:opacity-40"
            >
              {uploadingLogo ? (
                <>
                  <span className="h-3.5 w-3.5 animate-spin border border-slate-400 border-t-transparent" />
                  Subiendo…
                </>
              ) : 'Cambiar logo'}
            </button>
            <p className="text-xs text-tc-text-muted">PNG, JPG o WebP · máx. 5 MB</p>
            {logoError && <p role="alert" className="text-xs text-tc-stamp-red">{logoError}</p>}
          </div>
        </div>
      </section>

      {/* Datos del taller */}
      <form
        onSubmit={handleSubmit((formData) => saveMutation.mutate(formData))}
        className="border border-tc-border bg-white overflow-hidden"
      >
        <div className="bg-tc-stub-bg border-b border-tc-border px-5 py-2.5">
          <span className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Datos del taller</span>
        </div>

        <div className="space-y-4 p-5">
          <FormField label="Nombre del taller" requiredMark error={errors.name?.message} {...register('name')} />

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FormField label="Razón social" {...register('legalName')} />
            <FormField label="CUIT" placeholder="20-12345678-9" inputClassName="tabular-nums" {...register('taxId')} />
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FormField label="Dirección" {...register('address')} />
            <FormField label="Teléfono" {...register('phone')} />
          </div>
        </div>

        <div className="h-px bg-tc-perf" />

        <div className="bg-tc-stub-bg border-b border-tc-border px-5 py-2.5">
          <span className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Parámetros operativos</span>
        </div>

        <div className="space-y-4 p-5">
          <FormField
            label="Zona horaria"
            placeholder="America/Argentina/Buenos_Aires"
            error={errors.timeZone?.message}
            {...register('timeZone')}
          />

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <FormSelect label="IVA por defecto" {...register('defaultVatRate')}>
              {ALLOWED_VAT_RATES.map((rate) => (
                <option key={rate} value={rate}>{rate}%</option>
              ))}
            </FormSelect>
            <FormField
              label="Vigencia presupuesto (días)"
              type="number"
              min={1}
              max={180}
              inputClassName="tabular-nums"
              error={errors.quoteValidityDays?.message}
              {...register('quoteValidityDays')}
            />
          </div>
        </div>

        {saveError && (
          <div className="mx-5 mb-4 border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {saveError}
          </div>
        )}

        <div className="flex items-center justify-between border-t border-tc-border bg-tc-stub-bg px-5 py-3">
          {saveMutation.isSuccess && !isDirty && (
            <span className="text-xs font-bold tracking-wider uppercase text-tc-done-text">Guardado</span>
          )}
          {!saveMutation.isSuccess && <span />}
          <button
            type="submit"
            disabled={!isDirty || saveMutation.isPending}
            className="inline-flex items-center gap-2 border border-tc-purple bg-tc-purple px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-tc-purple-mid disabled:cursor-not-allowed disabled:opacity-40"
          >
            {saveMutation.isPending ? (
              <>
                <span className="h-3.5 w-3.5 animate-spin border border-white border-t-transparent" />
                Guardando…
              </>
            ) : 'Guardar cambios'}
          </button>
        </div>
      </form>
    </div>
  )
}
