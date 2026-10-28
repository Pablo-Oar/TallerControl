import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { attachmentsApi } from '../lib/api/attachments'
import { Button } from './ui/Button'
import { confirmDialog } from './ui/dialogs'
import { actionErrorMessage } from '../lib/api/errors'

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

/** Foto que se está subiendo: se muestra al instante con la imagen local, sin esperar al servidor. */
interface PendingPhoto {
  key: string
  file: File
  previewUrl: string
  status: 'queued' | 'uploading' | 'failed'
}

interface Props {
  workOrderId: string
  /** Dueño y administrativo pueden quitar fotos; el mecánico solo agrega. */
  canDelete?: boolean
  /** Solo mirar: sin cámara, galería ni subida (órdenes de otro mecánico). */
  readOnly?: boolean
}

export function WorkOrderPhotos({ workOrderId, canDelete = false, readOnly = false }: Props) {
  const queryClient = useQueryClient()
  const inputRef = useRef<HTMLInputElement>(null)
  const galleryRef = useRef<HTMLInputElement>(null)
  const [broken, setBroken] = useState<Set<string>>(new Set())
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState<PendingPhoto[]>([])
  const pendingRef = useRef(pending)
  pendingRef.current = pending
  const nextKey = useRef(0)

  // Al salir de la pantalla se liberan las vistas previas locales.
  useEffect(() => () => pendingRef.current.forEach(p => URL.revokeObjectURL(p.previewUrl)), [])
  const queryKey = ['work-order-attachments', workOrderId]

  const { data: attachments } = useQuery({
    queryKey,
    queryFn: () => attachmentsApi.listForWorkOrder(workOrderId),
  })

  const upload = useMutation({
    mutationFn: async (file: File) => attachmentsApi.upload('work_order', workOrderId, await prepareImage(file)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey }),
    onError: e => setError(actionErrorMessage(e, 'No se pudo subir la foto. Probá de nuevo.')),
  })

  const remove = useMutation({
    mutationFn: (id: string) => attachmentsApi.delete(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey }),
    onError: e => setError(actionErrorMessage(e, 'No se pudo quitar la foto.')),
  })

  // Celular/tablet (puntero táctil): "Sacar foto" + "Desde la galería". Escritorio: un solo "Adjuntar imágenes"
  // (varias a la vez, también arrastrando). Sin `matchMedia` (jsdom) se asume táctil.
  const touch = typeof window.matchMedia !== 'function' || window.matchMedia('(pointer: coarse)').matches
  const [dragging, setDragging] = useState(false)

  function setStatus(key: string, status: PendingPhoto['status']) {
    setPending(list => list.map(p => (p.key === key ? { ...p, status } : p)))
  }

  function discard(key: string) {
    setPending(list => {
      const found = list.find(p => p.key === key)
      if (found) URL.revokeObjectURL(found.previewUrl)
      return list.filter(p => p.key !== key)
    })
  }

  /** Sube de a una (en orden). Cada foto aparece en la grilla apenas se elige, con su vista previa local. */
  async function uploadQueue(items: PendingPhoto[]) {
    for (const item of items) {
      setStatus(item.key, 'uploading')
      try {
        await upload.mutateAsync(item.file) // al resolver, la lista del servidor ya está refrescada
        discard(item.key)
      } catch {
        setStatus(item.key, 'failed') // el error ya lo muestra la mutación
        // Las que seguían en cola quedan para reintentar: no se insiste con una conexión caída.
        setPending(list => list.map(p => (p.status === 'queued' && items.some(i => i.key === p.key) ? { ...p, status: 'failed' } : p)))
        return
      }
    }
  }

  function uploadFiles(files: File[]) {
    setError(null)
    const valid = files.filter(f => ACCEPTED.includes(f.type))
    if (valid.length < files.length) setError('Las fotos deben ser JPG, PNG o WebP.')
    if (valid.length === 0) return
    const items: PendingPhoto[] = valid.map(file => ({
      key: `local-${nextKey.current++}`, // sin crypto.randomUUID: no existe fuera de https/localhost (p. ej. por IP de la red)
      file,
      previewUrl: URL.createObjectURL(file),
      status: 'queued',
    }))
    setPending(list => [...list, ...items])
    void uploadQueue(items)
  }

  function retry(item: PendingPhoto) {
    setError(null)
    setStatus(item.key, 'queued')
    void uploadQueue([item])
  }

  function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? [])
    e.target.value = '' // permite volver a elegir el mismo archivo
    if (files.length) uploadFiles(files)
  }

  function onDrop(e: React.DragEvent) {
    e.preventDefault()
    setDragging(false)
    if (!touch) uploadFiles(Array.from(e.dataTransfer.files))
  }

  const photos = (attachments ?? []).filter(a => a.status === 'ready' && a.contentType.startsWith('image/') && a.downloadUrl)

  return (
    <div
      className={`space-y-3 ${dragging ? 'outline-2 outline-dashed outline-tc-purple' : ''}`}
      onDragOver={e => {
        if (touch) return
        e.preventDefault()
        setDragging(true)
      }}
      onDragLeave={() => setDragging(false)}
      onDrop={onDrop}
    >
      {photos.length === 0 && pending.length === 0 ? (
        <p className="text-sm text-tc-text-muted">Sin fotos todavía.</p>
      ) : (
        <ul className="grid grid-cols-3 gap-2 md:grid-cols-6 xl:grid-cols-8" aria-label="Fotos de la orden">
          {photos.map((p, i) => (
            <li key={p.id} className="relative">
              <a href={p.downloadUrl!} target="_blank" rel="noreferrer" className="block border border-tc-border bg-tc-surface">
                {broken.has(p.id) ? (
                  <span className="flex aspect-square w-full items-center justify-center p-1 text-center text-[11px] text-tc-text-muted">
                    Foto {i + 1}: no se pudo cargar
                  </span>
                ) : (
                  <img
                    src={p.downloadUrl!}
                    alt={`Foto ${i + 1} de la orden`}
                    loading="lazy"
                    onError={() => setBroken(b => new Set(b).add(p.id))}
                    className="aspect-square w-full object-cover"
                  />
                )}
              </a>
              {canDelete && (
                <button
                  type="button"
                  onClick={async () => {
                    if (await confirmDialog('¿Quitar esta foto de la orden?', { confirmLabel: 'Quitar', danger: true })) remove.mutate(p.id)
                  }}
                  disabled={remove.isPending}
                  aria-label={`Quitar foto ${i + 1}`}
                  className="group absolute right-0 top-0 flex h-11 w-11 items-center justify-center text-lg font-bold text-white"
                >
                  <span aria-hidden className="flex h-6 w-6 items-center justify-center bg-tc-navy/80 leading-none group-hover:bg-tc-stamp-red">×</span>
                </button>
              )}
            </li>
          ))}
          {pending.map(p => (
            <li key={p.key} className="relative" aria-live="polite">
              <div className="relative block overflow-hidden border border-tc-border bg-tc-surface">
                <img src={p.previewUrl} alt="Foto que se está subiendo" className={`aspect-square w-full object-cover ${p.status === 'failed' ? 'opacity-40' : 'opacity-70'}`} />
                <div className="absolute inset-x-0 bottom-0 flex items-center justify-center gap-1.5 bg-tc-navy/80 px-1 py-1 text-[11px] font-semibold text-white">
                  {p.status === 'failed' ? (
                    <span>No se subió</span>
                  ) : (
                    <>
                      <span className="h-3 w-3 animate-spin rounded-full border-2 border-white/40 border-t-white" aria-hidden="true" />
                      <span>{p.status === 'uploading' ? 'Subiendo…' : 'En cola…'}</span>
                    </>
                  )}
                </div>
              </div>
              {p.status === 'failed' && (
                <div className="absolute right-1 top-1 flex gap-1">
                  <button
                    type="button"
                    onClick={() => retry(p)}
                    className="min-h-[36px] bg-tc-purple px-2 text-[11px] font-bold text-white hover:opacity-90"
                  >
                    Reintentar
                  </button>
                  <button
                    type="button"
                    onClick={() => discard(p.key)}
                    aria-label="Descartar foto"
                    className="group flex h-11 w-11 items-center justify-center text-lg font-bold text-white"
                  >
                    <span aria-hidden className="flex h-6 w-6 items-center justify-center bg-tc-navy/80 leading-none group-hover:bg-tc-stamp-red">×</span>
                  </button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      {readOnly ? null : touch ? (
        <>
          {/* `capture` fuerza la cámara en Android; sin él se abre la galería/archivos. */}
          <input
            ref={inputRef}
            type="file"
            accept="image/*"
            capture="environment"
            className="sr-only"
            aria-hidden="true"
            aria-label="Sacar foto"
            tabIndex={-1}
            onChange={onPick}
          />
          <input
            ref={galleryRef}
            type="file"
            accept="image/*"
            multiple
            className="sr-only"
            aria-hidden="true"
            aria-label="Elegir de la galería"
            tabIndex={-1}
            onChange={onPick}
          />
          <div className="flex flex-wrap gap-2">
            <Button size="sm" variant="secondary" loading={upload.isPending} onClick={() => inputRef.current?.click()}>
              Sacar foto
            </Button>
            <Button size="sm" variant="secondary" disabled={upload.isPending} onClick={() => galleryRef.current?.click()}>
              Desde la galería
            </Button>
          </div>
        </>
      ) : (
        <>
          <input
            ref={galleryRef}
            type="file"
            accept="image/jpeg,image/png,image/webp"
            multiple
            className="sr-only"
            aria-hidden="true"
            aria-label="Adjuntar imágenes"
            tabIndex={-1}
            onChange={onPick}
          />
          <div className="flex flex-wrap items-center gap-3">
            <Button size="sm" variant="secondary" loading={upload.isPending} onClick={() => galleryRef.current?.click()}>
              Adjuntar imágenes
            </Button>
            <span className="text-xs text-tc-text-muted">o arrastralas acá (JPG, PNG o WebP)</span>
          </div>
        </>
      )}
      {error && (
        <p role="alert" className="text-xs text-tc-stamp-red">
          {error}
        </p>
      )}
    </div>
  )
}
