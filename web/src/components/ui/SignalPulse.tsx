import { useCallback, useEffect, useRef } from 'react'
import { cn } from '@/lib/utils'

/**
 * Fondo animado de circuitos que se revelan con pulsos (canvas 2D).
 * Basado en el componente `signal-pulse` de FutureUI; se pausa fuera de pantalla y,
 * con `prefers-reduced-motion`, queda el trazado fijo sin pulsos.
 */
export interface SignalPulseProps {
  pulseSpeed?: number
  pulseMaxRadius?: number
  pulseInterval?: number
  gridSpacing?: number
  accentColor?: string
  lineColor?: string
  className?: string
}

interface Pulse {
  ox: number
  oy: number
  radius: number
}

interface Segment {
  x1: number
  y1: number
  x2: number
  y2: number
}

function hexToRgb(hex: string): [number, number, number] {
  const m = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex)
  return m ? [parseInt(m[1], 16), parseInt(m[2], 16), parseInt(m[3], 16)] : [139, 92, 246]
}

function cssColorToRgb(css: string): [number, number, number] {
  const m = css.match(/(\d+)/g)
  return m ? [+m[0], +m[1], +m[2]] : [139, 92, 246]
}

// Aleatorio con semilla: el trazado es el mismo en cada carga.
function seededRandom(seed: number) {
  let s = seed
  return () => {
    s = (s * 16807) % 2147483647
    return s / 2147483647
  }
}

const RING_WIDTH = 30

export function SignalPulse({
  pulseSpeed = 2.5,
  pulseMaxRadius = 300,
  pulseInterval = 2000,
  gridSpacing = 30,
  accentColor = '#8b5cf6',
  lineColor = 'currentColor',
  className,
}: SignalPulseProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const rafRef = useRef(0)
  const visibleRef = useRef(true)
  const sizeRef = useRef({ w: 0, h: 0 })
  const dprRef = useRef(1)
  const pulsesRef = useRef<Pulse[]>([])
  const circuitRef = useRef<Segment[]>([])
  const lineRgbRef = useRef<[number, number, number]>([139, 92, 246])
  const accentRgbRef = useRef<[number, number, number]>([139, 92, 246])
  const reducedRef = useRef(false)
  const propsRef = useRef({ pulseSpeed, pulseMaxRadius, pulseInterval, gridSpacing })

  useEffect(() => {
    propsRef.current = { pulseSpeed, pulseMaxRadius, pulseInterval, gridSpacing }
  }, [pulseSpeed, pulseMaxRadius, pulseInterval, gridSpacing])

  useEffect(() => {
    accentRgbRef.current = hexToRgb(accentColor)
  }, [accentColor])

  useEffect(() => {
    const container = containerRef.current
    const canvas = canvasRef.current
    if (!container || !canvas) return

    reducedRef.current =
      typeof window.matchMedia === 'function' && window.matchMedia('(prefers-reduced-motion: reduce)').matches

    const buildCircuit = (w: number, h: number, spacing: number) => {
      const rand = seededRandom(42)
      const lines: Segment[] = []
      const cols = Math.floor(w / spacing)
      const rows = Math.floor(h / spacing)
      const offsetX = (w - cols * spacing) / 2
      const offsetY = (h - rows * spacing) / 2
      for (let row = 0; row <= rows; row++) {
        for (let col = 0; col <= cols; col++) {
          const x = offsetX + col * spacing
          const y = offsetY + row * spacing
          if (col < cols && rand() < 0.4) lines.push({ x1: x, y1: y, x2: x + spacing, y2: y })
          if (row < rows && rand() < 0.35) lines.push({ x1: x, y1: y, x2: x, y2: y + spacing })
          if (col < cols && row < rows && rand() < 0.1) lines.push({ x1: x, y1: y, x2: x + spacing, y2: y + spacing })
        }
      }
      circuitRef.current = lines
    }

    const draw = () => {
      const ctx = canvas.getContext('2d')
      if (!ctx) return
      const { w, h } = sizeRef.current
      const dpr = dprRef.current
      const { pulseSpeed: speed, pulseMaxRadius: rMax } = propsRef.current
      const pulses = pulsesRef.current
      const circuit = circuitRef.current
      const [lr, lg, lb] = lineRgbRef.current
      const [ar, ag, ab] = accentRgbRef.current

      ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
      ctx.clearRect(0, 0, w, h)

      for (let i = pulses.length - 1; i >= 0; i--) {
        pulses[i].radius += speed
        if (pulses[i].radius > rMax + RING_WIDTH) pulses.splice(i, 1)
      }

      // Trazado base, muy tenue.
      ctx.strokeStyle = `rgba(${lr},${lg},${lb},0.06)`
      ctx.lineWidth = 0.5
      ctx.beginPath()
      for (const c of circuit) {
        ctx.moveTo(c.x1, c.y1)
        ctx.lineTo(c.x2, c.y2)
      }
      ctx.stroke()

      if (pulses.length === 0) return

      // Tramos que el pulso va revelando.
      for (const c of circuit) {
        const mx = (c.x1 + c.x2) / 2
        const my = (c.y1 + c.y2) / 2
        let maxReveal = 0
        for (const pulse of pulses) {
          const dist = Math.hypot(mx - pulse.ox, my - pulse.oy)
          const outerR = pulse.radius + RING_WIDTH
          const innerR = Math.max(0, pulse.radius - RING_WIDTH)
          if (dist > outerR || dist < innerR) continue
          const t = (dist - pulse.radius) / RING_WIDTH
          const wave = 0.5 * (1 + Math.cos(t * Math.PI))
          const lifeFade = 1 - Math.max(0, (pulse.radius - rMax * 0.5) / (rMax * 0.5))
          maxReveal = Math.max(maxReveal, wave * Math.max(0, lifeFade))
        }
        if (maxReveal <= 0.02) continue

        ctx.strokeStyle = `rgba(${ar},${ag},${ab},${(maxReveal * 0.6).toFixed(3)})`
        ctx.lineWidth = 0.5 + maxReveal * 1.5
        ctx.beginPath()
        ctx.moveTo(c.x1, c.y1)
        ctx.lineTo(c.x2, c.y2)
        ctx.stroke()

        if (maxReveal > 0.3) {
          ctx.fillStyle = `rgba(${ar},${ag},${ab},${(maxReveal * 0.8).toFixed(2)})`
          ctx.beginPath()
          ctx.arc(c.x1, c.y1, 1.5 + maxReveal, 0, Math.PI * 2)
          ctx.arc(c.x2, c.y2, 1.5 + maxReveal, 0, Math.PI * 2)
          ctx.fill()
        }
      }

      // Anillos de los pulsos.
      for (const pulse of pulses) {
        const lifeFade = 1 - Math.max(0, (pulse.radius - rMax * 0.3) / (rMax * 0.7))
        if (lifeFade <= 0) continue
        ctx.strokeStyle = `rgba(${ar},${ag},${ab},${(lifeFade * 0.2).toFixed(3)})`
        ctx.lineWidth = 1
        ctx.beginPath()
        ctx.arc(pulse.ox, pulse.oy, pulse.radius, 0, Math.PI * 2)
        ctx.stroke()
      }
    }

    const loop = () => {
      if (!visibleRef.current) return
      draw()
      rafRef.current = requestAnimationFrame(loop)
    }

    const handleResize = () => {
      const { width, height } = container.getBoundingClientRect()
      if (width === 0 || height === 0) return
      const w = Math.round(width)
      const h = Math.round(height)
      sizeRef.current = { w, h }
      const dpr = Math.min(window.devicePixelRatio || 1, 2)
      dprRef.current = dpr
      canvas.width = w * dpr
      canvas.height = h * dpr
      lineRgbRef.current =
        lineColor === 'currentColor' ? cssColorToRgb(getComputedStyle(container).color) : hexToRgb(lineColor)
      buildCircuit(w, h, propsRef.current.gridSpacing)
      if (reducedRef.current) draw()
    }

    const ro = new ResizeObserver(handleResize)
    ro.observe(container)

    const io = new IntersectionObserver(([entry]) => {
      visibleRef.current = entry.isIntersecting
      if (entry.isIntersecting && !reducedRef.current) {
        cancelAnimationFrame(rafRef.current)
        rafRef.current = requestAnimationFrame(loop)
      }
    })
    io.observe(container)

    let timer = 0
    if (!reducedRef.current) {
      timer = window.setInterval(() => {
        if (!visibleRef.current) return
        const { w, h } = sizeRef.current
        const pulses = pulsesRef.current
        if (pulses.length >= 4) pulses.shift()
        pulses.push({ ox: Math.random() * w, oy: Math.random() * h, radius: 0 })
      }, propsRef.current.pulseInterval)
      rafRef.current = requestAnimationFrame(loop)
    }

    return () => {
      cancelAnimationFrame(rafRef.current)
      window.clearInterval(timer)
      ro.disconnect()
      io.disconnect()
    }
  }, [lineColor])

  const handleClick = useCallback((e: React.MouseEvent<HTMLDivElement>) => {
    if (reducedRef.current) return
    const el = containerRef.current
    if (!el) return
    const rect = el.getBoundingClientRect()
    const pulses = pulsesRef.current
    if (pulses.length >= 4) pulses.shift()
    pulses.push({ ox: e.clientX - rect.left, oy: e.clientY - rect.top, radius: 0 })
  }, [])

  return (
    // Decorativo: el clic solo dispara un pulso extra, no es una acción.
    // eslint-disable-next-line jsx-a11y/click-events-have-key-events, jsx-a11y/no-static-element-interactions
    <div ref={containerRef} className={cn('relative size-full', className)} onClick={handleClick}>
      <canvas ref={canvasRef} aria-hidden="true" className="block size-full" />
    </div>
  )
}

export default SignalPulse
