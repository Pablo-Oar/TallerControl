import { useState } from 'react'
import { CheckIcon, CopyIcon, LinkIcon, WhatsappIcon } from './ui/icons'

/**
 * Enlace para compartir con el cliente: campo de solo lectura con el texto cortado y un botón azul, solo con
 * ícono, pegado al campo para copiar. Debajo, el botón verde de WhatsApp.
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
    <div className="space-y-3">
      <div className="flex items-center gap-2 rounded-xl border border-slate-300 bg-slate-50 p-1.5 pl-3 shadow-inner focus-within:border-blue-500 focus-within:ring-2 focus-within:ring-blue-500/20">
        <LinkIcon className="h-4 w-4 shrink-0 text-tc-text-muted" />
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
          className={`inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-white shadow-sm transition-colors focus-visible:ring-2 focus-visible:ring-offset-1 max-md:h-11 max-md:w-11 ${
            copied ? 'bg-emerald-600 hover:bg-emerald-700 focus-visible:ring-emerald-600' : 'bg-blue-600 hover:bg-blue-700 focus-visible:ring-blue-600'
          }`}
        >
          {copied ? <CheckIcon className="h-5 w-5" /> : <CopyIcon className="h-5 w-5" />}
        </button>
      </div>
      <a
        href={`https://wa.me/?text=${encodeURIComponent(`${whatsappText} ${url}`)}`}
        target="_blank"
        rel="noreferrer"
        className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-tc-success-solid px-4 py-2.5 text-sm font-bold text-white shadow-sm transition-colors hover:brightness-90 focus-visible:ring-2 focus-visible:ring-tc-success-solid focus-visible:ring-offset-1 focus-visible:outline-none"
      >
        <WhatsappIcon className="h-5 w-5" />
        Enviar por WhatsApp
      </a>
    </div>
  )
}
