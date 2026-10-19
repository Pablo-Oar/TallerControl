# UI-R2 F0 · 0.3 — Relevamiento del registry de Watermelon UI (19/10/2026)

Fuente: `WatermelonCorp/watermelon-platform` (MIT, D-29), leído con `gh api`. El registry real tiene
**131 componentes animados** (`src/data/contents/registry/*.mdx`) y **bloques** (auth-01..13, hero, footer, etc.).
Todos dependen de `motion`; casi todos de `react-icons` o `lucide-react`; algunos de `@hugeicons/*`,
`next-themes`, `react-use-measure`, `@number-flow/react`.

## Hallazgos que afectan al plan
1. **auth-05 sí existe** (`blocks/auth/auth-05`), pero es un login con botones Google/GitHub, depende de `components/base-ui/{button,input,label,separator,checkbox}` y de `react-icons` (no es autocontenido). TallerControl no tiene login social (D-18 abierta). **Recomendación: no adoptar**; usarlo solo como referencia visual para `LoginPage` (layout dividido formulario/panel). Pasa a "descartado salvo inspiración".
2. **Peso de dependencias:** `draw-signature` exige `next-themes`, `react-use-measure` y `react-icons`; `edit-badge` usa 5 paquetes de iconos de `react-icons`. Regla para F1+: reemplazar los iconos por `lucide-react` (o SVG propios) y quitar `next-themes` (no hay modo oscuro) al vendorear; no sumar `react-icons` completo (tree-shaking a medias).
3. **Código "base" vs "original":** cada componente trae dos variantes (`base.tsx` simple, `original.tsx` con animación completa). Los `base` suelen ser CSS-only y más livianos; elegir caso a caso.
4. Los componentes usan `rounded-lg`, `bg-input`, sombras fijas y `theme-injected`: todos requieren adaptación al puente `tc-*` (ver README de `components/watermelon/`).

## Button animado (criterios de doc-12 0.3)
| Candidato | loading | disabled | sm/md | 4 variantes | Sin salto de layout | ≤200 ms | Veredicto |
|---|---|---|---|---|---|---|---|
| `shimmer-button` (base) | no | no (hereda) | no | no (solo primary) | sí | no (barrido 700 ms en hover) | **Mejor base:** CSS-only, sin `motion`, sin iconos. Hay que acortar el barrido, sumar `loading`/variantes/tamaños y `motion-reduce:`. |
| `save-toggle` | sí (idle→loading→success→saved) | parcial | sm/md/lg fijos en px | no | no (cambia de ancho; fuerza `stableWidth`) | no (spinner + éxito ~1,8 s) | Inspiración para el patrón guardar→confirmado; usa `next-themes`, tamaños de 52–68 px: no sirve directo. |
| `morphing-button` | no | no | no | no | no (se expande a 84 de ancho) | no | Descartado (es un CTA de captura de email). |
| `run-action-button` | sí (pasos) | no | no | no | no | no | Descartado (secuencia de pasos de IA, texto por carácter). |

**Propuesta:** el "Button animado" es el `Button.tsx` actual (variantes/tamaños/`loading` ya resueltos en UI-Redesign) con una micro-interacción
tomada de `shimmer-button` (brillo en hover/`loading`, ≤200 ms, desactivado con `reduced-motion`) y un check animado al éxito inspirado en `save-toggle`.
No se instala un componente nuevo: se porta la idea. Costo bajo, cero dependencias.

## Catálogo por categoría (131)
- **Comprometidos D-25:** `floating-input` (inputs), `draw-signature` (inputs), `dialog-stack` (dialog), `edit-profile` (dialog), `fluid-tabs` (tabs), `activities-card` (disclosure), `edit-badge` (interaction). Condicional: `command-search` (navigation).
- **Candidatos útiles a futuro (no comprometidos):** `status-picker` (micro-interaction, alternativa a edit-badge), `inline-table-control` / `inline-edit` / `split-to-edit` (edición en línea en tablas), `step-indicator` / `step-pager` / `pop-stepper` (asistentes, p. ej. onboarding), `continuous-tabs` / `discrete-tabs` (tabs más livianos que fluid-tabs), `pagination` (hoy hecha a mano en listas), `inline-toast` / `copy-confirm` / `reveal-copy` (feedback), `onboarding-checklist` (widget de guía de inicio, F-54), `show-qr` (widget QR), `slot-picker` / `schedule-date` / `calendar-widget` (Agenda E14), `transaction-list` (Caja), `tree-menu` / `macos-sidebar` (navegación; el sidebar actual ya está resuelto).
- **Descartados por dominio o peso:** carruseles (`minimal-carousel`, `radial-carousel`, `wiggling-cards`, `card-swipe`, `carousel-*`), financieros/cripto (`uniswap-dialog`, `aave-swap-component`, `swap-form`, `swap-currency-card`, `trade-summary`, `family-wallet`, `send-money`, `fund-widget`, `returns-calculator-snippet`), voz/IA (`voice-*`, `contextual-ai-bar`, `select-ai-agent`, `predictive-text`), marketing (`changeable-pricing-section`, `pricing-widget`), sliders decorativos (`knob-slider`, `adaptive-slider`, `weight-widget`, `waveform-scrub`).
- **Regla:** cualquier candidato nuevo requiere decisión explícita (se agrega a D-25) antes de instalarse; el ahorro de F0 es no sumar dependencias de iconos.

## 0.10 — Spike visual D-26 (19/10/2026)
Comparación lado a lado: `Input.tsx` actual · Watermelon `floating-input` con `--radius: 0` · mismo con `--radius: 0.5rem`, usando el puente de tokens.

- **El puente funciona:** con `--radius: 0` el campo y el botón de Watermelon quedan coherentes con la identidad "ticket" (esquinas rectas, navy/violeta); con `0.5rem` se ve más "SaaS genérico" y choca con las tarjetas y el sidebar actuales. `--radius` es efectivamente la palanca única.
- **Bug confirmado en `floating-input`:** con valor inicial (`defaultValue`, y por extensión `reset`/`setValue` de React Hook Form) el label queda **encima del texto** ("Nombre" sobre "Juan Pérez"), porque `hasValue` es estado interno. Se arregla en `FormField` con CSS (`:placeholder-shown`) en vez de estado.
- **Doble anillo de foco:** el `border-primary` del componente se suma a la regla global `input:focus-visible` de `index.css` (outline 2px violeta + offset); se ve un marco doble. `FormField` debe usar una sola señal de foco.
- **Densidad:** el campo flotante mide ~50 px de alto contra ~38 px del `Input` actual (+30 %); en formularios largos (OT, presupuesto) y en `/m` conviene un modo `size="sm"`/denso.

**Recomendación D-26:** confirmar el valor provisorio (mantener identidad `tc-*`, `--radius: 0px`). Capturas guardadas fuera del repo (scratchpad de la sesión).
