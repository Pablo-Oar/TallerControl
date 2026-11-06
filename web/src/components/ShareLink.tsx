import { useState } from 'react'
import { CheckIcon, CopyIcon } from './ui/icons'

/**
 * Enlace para compartir con el cliente: campo de solo lectura con el texto truncado y un botón azul, solo con
 * ícono, pegado al campo para copiar. Debajo, el atajo a WhatsApp.
 */
export function ShareLink({ url, whatsappText, label = 'Enlace' }: { url: string; whatsappText: string; label?: string }) {
  const [copied, setCopied] = useState(false)

  async function copy() {
    try {
      await navigator.clipboard.writeText(url)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 2000)
    } catch {
      setCopied(false)
    }
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2 rounded-xl border border-tc-border bg-white p-1.5 pl-3">
        <span className="sr-only">{label}</span>
        <input
          readOnly
          value={url}
          aria-label={label}
          onFocus={e => e.currentTarget.select()}
          className="min-w-0 flex-1 truncate bg-transparent text-sm text-tc-text-secondary outline-none"
        />
        <button
          type="button"
          onClick={copy}
          aria-label={copied ? 'Copiado' : `Copiar ${label.toLowerCase()}`}
          title={copied ? 'Copiado' : 'Copiar'}
          className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-blue-600 text-white transition-colors hover:bg-blue-700 focus-visible:ring-2 focus-visible:ring-blue-600 focus-visible:ring-offset-1 max-md:h-11 max-md:w-11"
        >
          {copied ? <CheckIcon className="h-5 w-5" /> : <CopyIcon className="h-5 w-5" />}
        </button>
      </div>
      <a
        href={`https://wa.me/?text=${encodeURIComponent(`${whatsappText} ${url}`)}`}
        target="_blank"
        rel="noreferrer"
        className="inline-flex min-h-11 w-full items-center justify-center rounded-xl border border-tc-border bg-white px-3 py-1.5 text-xs font-bold tracking-wider uppercase text-tc-text-secondary transition-colors hover:bg-tc-surface"
      >
        Enviar por WhatsApp
      </a>
    </div>
  )
}
