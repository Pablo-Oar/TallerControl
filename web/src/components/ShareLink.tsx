import { useState } from 'react'
import { Button } from './ui/Button'
import { FormField } from './ui/FormField'

/** Enlace para compartir con el cliente: campo de solo lectura, copiar y WhatsApp. */
export function ShareLink({ url, whatsappText }: { url: string; whatsappText: string }) {
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
    <div className="flex flex-wrap items-end gap-2">
      <FormField label="Enlace" size="sm" readOnly value={url} className="min-w-0 flex-1" />
      <Button variant="secondary" size="sm" onClick={copy}>{copied ? 'Copiado' : 'Copiar'}</Button>
      <a
        href={`https://wa.me/?text=${encodeURIComponent(`${whatsappText} ${url}`)}`}
        target="_blank"
        rel="noreferrer"
        className="inline-flex items-center border border-tc-border bg-white px-3 py-1.5 text-xs font-bold tracking-wider uppercase text-tc-text-secondary transition-colors hover:bg-tc-surface"
      >
        WhatsApp
      </a>
    </div>
  )
}
