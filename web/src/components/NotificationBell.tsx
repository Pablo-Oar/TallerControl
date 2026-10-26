import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../lib/api/client'
import { useCan } from '../lib/auth/permissions'

interface NotificationDto {
  id: string
  type: string
  title: string
  link: string | null
  isRead: boolean
  createdAt: string
}

interface NotificationsDto {
  unread: number
  items: NotificationDto[]
}

const fmtWhen = (iso: string) => new Date(iso).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' })

/** Campanita de avisos del panel (llaves encontradas, recordatorios vencidos…). Se actualiza cada minuto. */
export function NotificationBell() {
  const can = useCan()('notifications.read')
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const root = useRef<HTMLDivElement>(null)

  const { data } = useQuery({
    queryKey: ['notifications'],
    queryFn: () => api.get<NotificationsDto>('/api/v1/notifications'),
    enabled: can,
    refetchInterval: 60_000,
  })

  const markRead = useMutation({
    mutationFn: (id: string) => api.post<void>(`/api/v1/notifications/${id}/read`, undefined, true),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  })
  const markAll = useMutation({
    mutationFn: () => api.post<void>('/api/v1/notifications/read-all', undefined, true),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  })

  // Cierra al tocar afuera o con Escape.
  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent) => {
      if (root.current && !root.current.contains(e.target as Node)) setOpen(false)
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDown)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  if (!can) return null

  const unread = data?.unread ?? 0

  function openNotification(n: NotificationDto) {
    if (!n.isRead) markRead.mutate(n.id)
    setOpen(false)
    if (n.link) navigate(n.link)
  }

  return (
    <div ref={root} className="relative">
      <button
        type="button"
        onClick={() => setOpen(v => !v)}
        aria-label={unread > 0 ? `Avisos: ${unread} sin leer` : 'Avisos'}
        aria-expanded={open}
        className="relative flex h-11 w-11 items-center justify-center text-tc-navy-text lg:h-7 lg:w-8 transition-colors hover:text-white"
      >
        <svg className="h-[18px] w-[18px]" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
          <path d="M6 8a6 6 0 1 1 12 0c0 7 3 9 3 9H3s3-2 3-9" />
          <path d="M10.3 21a1.94 1.94 0 0 0 3.4 0" />
        </svg>
        {unread > 0 && (
          <span className="absolute -top-0.5 right-0 flex h-4 min-w-4 items-center justify-center rounded-full bg-tc-purple px-1 text-[11px] font-bold text-white">
            {unread > 9 ? '9+' : unread}
          </span>
        )}
      </button>

      {open && (
        <div
          role="dialog"
          aria-label="Avisos"
          className="absolute right-0 z-40 mt-2 w-80 max-w-[calc(100vw-1.5rem)] rounded-2xl border border-tc-border bg-white shadow-xl"
        >
          <div className="flex items-center justify-between border-b border-tc-border px-4 py-2.5">
            <span className="text-xs font-bold tracking-wider uppercase text-tc-text">Avisos</span>
            {unread > 0 && (
              <button type="button" onClick={() => markAll.mutate()} className="text-xs font-semibold text-tc-purple hover:underline">
                Marcar todos como leídos
              </button>
            )}
          </div>
          {(data?.items.length ?? 0) === 0 ? (
            <p className="px-4 py-6 text-center text-sm text-tc-text-muted">No tenés avisos.</p>
          ) : (
            <ul className="max-h-96 overflow-auto">
              {data!.items.map(n => (
                <li key={n.id} className="border-b border-tc-border last:border-0">
                  <button
                    type="button"
                    onClick={() => openNotification(n)}
                    className="flex w-full items-start gap-2 px-4 py-3 text-left hover:bg-tc-surface"
                  >
                    <span className={`mt-1.5 h-2 w-2 shrink-0 rounded-full ${n.isRead ? 'bg-transparent' : 'bg-tc-purple'}`} aria-hidden />
                    <span className="min-w-0">
                      <span className={`block text-sm ${n.isRead ? 'text-tc-text-secondary' : 'font-semibold text-tc-text'}`}>{n.title}</span>
                      <span className="block text-xs text-tc-text-muted">{fmtWhen(n.createdAt)}</span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}
