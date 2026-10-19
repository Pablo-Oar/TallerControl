import { forwardRef, useCallback, useEffect, useImperativeHandle, useRef, useState } from 'react'
import { cn } from '@/lib/utils'

/**
 * Firma manuscrita sobre canvas (UI-R2 F1, D-25).
 *
 * Propia, no vendoreada: `draw-signature` de Watermelon exige next-themes, react-use-measure y react-icons.
 * Usa Pointer Events (ratón, táctil y lápiz con un solo camino), trazo suavizado con curvas cuadráticas y
 * fondo blanco (el PNG exportado no es transparente). Resolución lógica 600×160, igual a la firma anterior.
 */

const WIDTH = 600
const HEIGHT = 160

export interface SignaturePadHandle {
  clear: () => void
  /** PNG con fondo blanco, o null si no hay trazo. */
  toBlob: () => Promise<Blob | null>
}

interface SignaturePadProps {
  /** Se llama al pasar de vacío a firmado y viceversa. */
  onChange?: (hasSignature: boolean) => void
  disabled?: boolean
  className?: string
  /** Texto de ayuda bajo el lienzo. */
  hint?: string
  clearLabel?: string
}

function paintBackground(canvas: HTMLCanvasElement) {
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  ctx.fillStyle = '#fff'
  ctx.fillRect(0, 0, canvas.width, canvas.height)
}

export const SignaturePad = forwardRef<SignaturePadHandle, SignaturePadProps>(function SignaturePad(
  { onChange, disabled, className, hint = 'Dibuje su firma con el dedo o el mouse', clearLabel = 'Borrar firma' },
  ref,
) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const drawing = useRef(false)
  const last = useRef<{ x: number; y: number } | null>(null)
  const signed = useRef(false)
  const [hasSignature, setHasSignature] = useState(false)

  useEffect(() => {
    if (canvasRef.current) paintBackground(canvasRef.current)
  }, [])

  const update = useCallback(
    (value: boolean) => {
      if (signed.current === value) return
      signed.current = value
      setHasSignature(value)
      onChange?.(value)
    },
    [onChange],
  )

  const clear = useCallback(() => {
    if (canvasRef.current) paintBackground(canvasRef.current)
    last.current = null
    update(false)
  }, [update])

  useImperativeHandle(
    ref,
    () => ({
      clear,
      toBlob: () =>
        new Promise<Blob | null>(resolve => {
          const canvas = canvasRef.current
          if (!canvas || !hasSignature) return resolve(null)
          canvas.toBlob(b => resolve(b), 'image/png')
        }),
    }),
    [clear, hasSignature],
  )

  function pos(e: React.PointerEvent<HTMLCanvasElement>) {
    const canvas = e.currentTarget
    const rect = canvas.getBoundingClientRect()
    const w = rect.width || WIDTH
    const h = rect.height || HEIGHT
    return { x: ((e.clientX - rect.left) * canvas.width) / w, y: ((e.clientY - rect.top) * canvas.height) / h }
  }

  function onPointerDown(e: React.PointerEvent<HTMLCanvasElement>) {
    if (disabled) return
    e.preventDefault()
    e.currentTarget.setPointerCapture?.(e.pointerId)
    drawing.current = true
    last.current = pos(e)
    update(true)
  }

  function onPointerMove(e: React.PointerEvent<HTMLCanvasElement>) {
    if (!drawing.current || !last.current) return
    e.preventDefault()
    const ctx = e.currentTarget.getContext('2d')
    if (!ctx) return
    const p = pos(e)
    ctx.lineWidth = 2.5
    ctx.lineCap = 'round'
    ctx.lineJoin = 'round'
    ctx.strokeStyle = '#0f172a'
    // Curva cuadrática hacia el punto medio: trazo continuo sin picos.
    const mid = { x: (last.current.x + p.x) / 2, y: (last.current.y + p.y) / 2 }
    ctx.beginPath()
    ctx.moveTo(last.current.x, last.current.y)
    ctx.quadraticCurveTo(last.current.x, last.current.y, mid.x, mid.y)
    ctx.lineTo(p.x, p.y)
    ctx.stroke()
    last.current = p
  }

  function end(e: React.PointerEvent<HTMLCanvasElement>) {
    if (!drawing.current) return
    drawing.current = false
    // Un toque sin movimiento deja un punto visible.
    const ctx = e.currentTarget.getContext('2d')
    if (ctx && last.current) {
      ctx.fillStyle = '#0f172a'
      ctx.beginPath()
      ctx.arc(last.current.x, last.current.y, 1.25, 0, Math.PI * 2)
      ctx.fill()
    }
    last.current = null
  }

  return (
    <div className={className}>
      <div
        className={cn(
          'touch-none select-none overflow-hidden border border-tc-border-mid bg-white',
          disabled && 'opacity-50',
        )}
      >
        <canvas
          ref={canvasRef}
          width={WIDTH}
          height={HEIGHT}
          role="img"
          aria-label={hasSignature ? 'Firma dibujada' : 'Área de firma vacía'}
          className="block w-full cursor-crosshair"
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={end}
          onPointerCancel={end}
          onLostPointerCapture={end}
        />
      </div>
      <div className="mt-1 flex min-h-[44px] items-center justify-between gap-3">
        <p className="text-xs text-tc-text-muted">{hint}</p>
        <button
          type="button"
          onClick={clear}
          disabled={!hasSignature || disabled}
          className="px-2 py-3 text-xs font-bold uppercase tracking-wider text-tc-text-secondary transition-colors hover:text-tc-stamp-red disabled:cursor-not-allowed disabled:opacity-40"
        >
          {clearLabel}
        </button>
      </div>
    </div>
  )
})
