import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { attachmentsApi } from '../lib/api/attachments'
import { ApiError } from '../lib/api/client'
import { Button } from './ui/Button'

/**
 * Fotos de una orden de trabajo (REQ-13, doc 06 /m/ordenes/:id "subir fotos desde la cámara").
 * En el celular el botón abre la cámara trasera (`capture="environment"`). Las fotos del teléfono pesan varios MB
 * (el servidor acepta hasta 10 MB), así que se reducen a 1600 px antes de subirlas: sube rápido con datos móviles.
 */

const MAX_SIDE = 1600
const ACCEPTED = ['image/jpeg', 'image/png', 'image/webp']

/** Reduce la imagen a MAX_SIDE y la recodifica como JPEG; si el navegador no puede, devuelve el archivo tal cual. */
export async function prepareImage(file: File): Promise<File> {
  if (typeof createImageBitmap !== 'function') return file
  try {
    const bitmap = await createImageBitmap(file)
    const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height))
    if (scale === 1 && file.size < 1_500_000) {
      bitmap.close()
      return file
    }
    const canvas = document.createElement('canvas')
    canvas.width = Math.round(bitmap.width * scale)
    canvas.height = Math.round(bitmap.height * scale)
    canvas.getContext('2d')?.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
    bitmap.close()
    const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/jpeg', 0.82))
    if (!blob) return file
    return new File([blob], file.name.replace(/\.\w+$/, '') + '.jpg', { type: 'image/jpeg' })
  } catch {
    return file
  }
}

interface Props {
  workOrderId: string
  /** Dueño y administrativo pueden quitar fotos; el mecánico solo agrega. */
  canDelete?: boolean
}

export function WorkOrderPhotos({ workOrderId, canDelete = false }: Props) {
  const queryClient = useQueryClient()
  const inputRef = useRef<HTMLInputElement>(null)
  const [error, setError] = useState<string | null>(null)
  const queryKey = ['work-order-attachments', workOrderId]

  const { data: attachments } = useQuery({
    queryKey,
    queryFn: () => attachmentsApi.listForWorkOrder(workOrderId),
  })

  const upload = useMutation({
    mutationFn: async (file: File) => attachmentsApi.upload('work_order', workOrderId, await prepareImage(file)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey }),
    onError: e => setError(e instanceof ApiError ? e.detail : 'No se pudo subir la foto. Probá de nuevo.'),
  })

  const remove = useMutation({
    mutationFn: (id: string) => attachmentsApi.delete(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey }),
    onError: e => setError(e instanceof ApiError ? e.detail : 'No se pudo quitar la foto.'),
  })

  function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = '' // permite volver a elegir el mismo archivo
    if (!file) return
    setError(null)
    if (!ACCEPTED.includes(file.type)) {
      setError('La foto debe ser JPG, PNG o WebP.')
      return
    }
    upload.mutate(file)
  }

  const photos = (attachments ?? []).filter(a => a.status === 'ready' && a.contentType.startsWith('image/') && a.downloadUrl)

  return (
    <div className="space-y-3">
      {photos.length === 0 ? (
        <p className="text-sm text-tc-text-muted">Sin fotos todavía.</p>
      ) : (
        <ul className="grid grid-cols-3 gap-2" aria-label="Fotos de la orden">
          {photos.map((p, i) => (
            <li key={p.id} className="relative">
              <a href={p.downloadUrl!} target="_blank" rel="noreferrer" className="block border border-tc-border bg-tc-surface">
                <img
                  src={p.downloadUrl!}
                  alt={`Foto ${i + 1} de la orden`}
                  loading="lazy"
                  className="aspect-square w-full object-cover"
                />
              </a>
              {canDelete && (
                <button
                  type="button"
                  onClick={() => remove.mutate(p.id)}
                  disabled={remove.isPending}
                  aria-label={`Quitar foto ${i + 1}`}
                  className="absolute right-1 top-1 flex h-7 w-7 items-center justify-center bg-tc-navy/80 text-xs font-bold text-white hover:bg-tc-stamp-red"
                >
                  ×
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        capture="environment"
        className="sr-only"
        aria-label="Elegir foto"
        tabIndex={-1}
        onChange={onPick}
      />
      <Button size="sm" variant="secondary" loading={upload.isPending} onClick={() => inputRef.current?.click()}>
        Agregar foto
      </Button>
      {error && (
        <p role="alert" className="text-xs text-tc-stamp-red">
          {error}
        </p>
      )}
    </div>
  )
}
