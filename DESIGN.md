---
name: TallerControl
description: SaaS de gestión para talleres mecánicos; documentos como pases de abordo sobre una consola navy.
colors:
  carrier-navy: "#0a1628"
  carrier-navy-mid: "#1e3a5f"
  carrier-navy-surface: "#0d1f38"
  carrier-navy-deep: "#05101f"
  carrier-navy-text: "#4b6fa5"
  carrier-navy-text-dim: "#2d4f7c"
  paper-white: "#ffffff"
  paper-surface: "#f8fafc"
  paper-border: "#e2e8f0"
  paper-border-mid: "#cbd5e1"
  stub-blue: "#f0f4fb"
  perforation: "#c7d3e8"
  carbon-purple: "#7c3aed"
  carbon-purple-light: "#ede9fe"
  carbon-purple-mid: "#5b21b6"
  stamp-red: "#d32f2f"
  stamp-pending-bg: "#fef3c7"
  stamp-pending-text: "#92400e"
  stamp-inprog-bg: "#dbeafe"
  stamp-inprog-text: "#1e40af"
  stamp-done-bg: "#d1fae5"
  stamp-done-text: "#065f46"
  ink: "#0f172a"
  ink-secondary: "#475569"
  ink-muted: "#94a3b8"
typography:
  title:
    fontFamily: "ui-sans-serif, system-ui, Arial, Helvetica, sans-serif"
    fontSize: "1.125rem"
    fontWeight: 700
    lineHeight: 1.3
  body:
    fontFamily: "ui-sans-serif, system-ui, Arial, Helvetica, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.5
  label:
    fontFamily: "ui-sans-serif, system-ui, Arial, Helvetica, sans-serif"
    fontSize: "0.625rem"
    fontWeight: 700
    letterSpacing: "0.1em"
  numeric:
    fontFamily: "ui-monospace, 'Courier New', monospace"
    fontSize: "0.875rem"
    fontWeight: 500
    fontFeature: "'tnum'"
rounded:
  none: "0px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "20px"
components:
  button-primary:
    backgroundColor: "{colors.carbon-purple}"
    textColor: "{colors.paper-white}"
    rounded: "{rounded.none}"
    padding: "8px 16px"
  button-primary-hover:
    backgroundColor: "{colors.carbon-purple-mid}"
  button-secondary:
    backgroundColor: "{colors.paper-white}"
    textColor: "{colors.ink-secondary}"
    rounded: "{rounded.none}"
    padding: "8px 16px"
  button-danger:
    backgroundColor: "{colors.stamp-red}"
    textColor: "{colors.paper-white}"
    rounded: "{rounded.none}"
    padding: "8px 16px"
  modal-header:
    backgroundColor: "{colors.carrier-navy}"
    textColor: "{colors.paper-white}"
    padding: "12px 20px"
  status-stamp:
    backgroundColor: "{colors.stamp-pending-bg}"
    textColor: "{colors.stamp-pending-text}"
    typography: "{typography.label}"
    rounded: "{rounded.none}"
    padding: "2px 8px"
---

# Design System: TallerControl

## Overview

**Creative North Star: "The Boarding Pass"**

Cada documento del taller (orden, presupuesto, cobro) es un pase de abordo: un ticket con talón, línea perforada y sello de estado, emitido desde una cabina navy. El shell oscuro (header y sidebar) enmarca el trabajo; el contenido vive sobre papel blanco, plano y delimitado por bordes finos. La identidad es sobria y firme —esquinas rectas, mayúsculas espaciadas en rótulos, números tabulares— porque la usan dueños y administrativos que escanean mucha información, y mecánicos con las manos sucias en el celular.

La densidad es alta pero ordenada: pocos colores, una sola voz de acento (el violeta del dinero) y sellos de estado de colores semánticos. No hay decoración: la personalidad está en los detalles de ticket (perforación, talón, sellos).

**Key Characteristics:**
- Shell navy + contenido blanco; el contraste entre ambos es la estructura.
- Esquinas rectas en todo (`--radius: 0px`); es la palanca única si D-26 cambiara.
- Plano en reposo: bordes de 1px, sin sombras salvo el modal.
- Violeta reservado a montos y acción primaria; estados por sellos de color.
- Sistema tipográfico nativo, sin fuentes web.

## Colors

Paleta de cabina y papel: navy profundo, blancos fríos y un único acento violeta, más cuatro sellos semánticos.

### Primary
- **Carbon Purple** (#7c3aed): montos, botón primario, foco, selección. Hover **Carbon Purple Deep** (#5b21b6); fondo suave **Carbon Purple Mist** (#ede9fe) para estados "convertido" y acentos tenues.

### Secondary
- **Carrier Navy** (#0a1628): header, encabezado de modal, shell. Escalón medio #1e3a5f (bordes del shell), superficie #0d1f38, profundo #05101f (sidebar). Texto sobre navy: #4b6fa5 (normal) y #2d4f7c (atenuado).

### Neutral
- **Paper White** (#ffffff) y **Paper Surface** (#f8fafc): fondos de contenido y hover de filas/secundarios.
- **Paper Border** (#e2e8f0) y **Border Mid** (#cbd5e1): bordes de tarjetas, tablas e inputs.
- **Stub Blue** (#f0f4fb): columna talón y track de scrollbar. **Perforation** (#c7d3e8): línea punteada del ticket.
- **Ink** (#0f172a), **Ink Secondary** (#475569), **Ink Muted** (#94a3b8): jerarquía de texto (tokens `tc-text*`, migrados desde `text-slate-*` en F5).

### Stamps (estado)
- Pendiente (#fef3c7 / #92400e), En progreso (#dbeafe / #1e40af), Completada (#d1fae5 / #065f46), Anulada (gris sobre blanco, tachado) y **Stamp Red** (#d32f2f) para acciones destructivas y rechazo.

### Named Rules
**The Money Purple Rule.** El violeta marca dinero y la acción principal; no se usa de relleno decorativo ni para estados.
**The Stamp Rule.** El estado se comunica con sello (borde + fondo suave + texto en mayúsculas), nunca solo con color de texto.

## Typography

**Display/Body/Label Font:** pila nativa `ui-sans-serif, system-ui, Arial, Helvetica, sans-serif`.
**Mono (números):** `ui-monospace, 'Courier New', monospace`.

**Character:** utilitaria y legible; la jerarquía sale del peso, el tamaño pequeño y el espaciado, no de fuentes decorativas.

### Hierarchy
- **Title** (700, 1.125rem, 1.3): títulos de página y de tarjeta.
- **Body** (400, 0.875rem, 1.5): contenido y formularios.
- **Label** (700, 0.625–0.75rem, tracking 0.1em, MAYÚSCULAS): sellos, encabezados de modal y de columnas.
- **Numeric** (500, 0.875rem, `tnum`): todo monto y cantidad usa clase `tabular-nums`.

### Named Rules
**The Tabular Money Rule.** Los importes siempre en numerales tabulares y alineados a la derecha.

## Layout

Shell fijo: header de 52px y sidebar de 220px sobre navy; el contenido ocupa el resto en blanco, con scroll propio. `/m` (mecánico) es mobile-first con objetivos táctiles de ≥44px; las páginas públicas (`/p`, `/o`, `/c`, `/q`) son de una columna centrada para celular. Espaciado en pasos de 4/8/16/20px; el relleno de panel es 20px.

## Elevation & Depth

Plano por defecto. La profundidad se logra con contraste de superficies (navy / blanco / talón azulado) y bordes de 1px, no con sombras.

### Shadow Vocabulary
- **Modal** (`shadow-2xl`): única sombra estructural; separa el diálogo del velo `bg-black/60`.

### Named Rules
**The Flat-At-Rest Rule.** Ninguna tarjeta, botón o fila lleva sombra en reposo.

## Shapes

Esquinas rectas (`--radius: 0px`, puente shadcn → `tc-*`). Bordes de 1px; la forma distintiva es el ticket: línea perforada punteada (1px, 4px/4px) y talón lateral. Excepción puntual: píldoras o círculos solo en spinners y avatares. 

## Components

### Buttons
- **Shape:** recto, borde de 1px, `tracking-wide`, `font-medium`.
- **Primary:** Carbon Purple con texto blanco, `8px 16px`; hover Carbon Purple Deep.
- **Secondary:** blanco con borde Paper Border; hover Paper Surface. **Danger:** Stamp Red. **Ghost:** transparente con borde navy-mid (solo sobre navy).
- **Loading/Disabled:** spinner de 16px antes del texto; `opacity-50` al deshabilitar. Tamaños `sm` (12px) y `md` (14px).
- **Focus:** anillo violeta de 2px con offset (regla global `:focus-visible`).

### Status Stamp
- Mayúsculas, 10px, negrita, tracking amplio, borde + fondo suave del color semántico; la anulación va tachada.
- **Sello editable** (`EditableStatusBadge`): en el encabezado de la OT ofrece solo las transiciones válidas; "Anulada" pide motivo en un diálogo.

### Modal
- Encabezado navy con título en mayúsculas espaciadas y botón de cierre; línea perforada bajo el encabezado; cuerpo blanco de 20px de relleno; velo negro 60%.
- Pila de diálogos: Escape cierra solo el superior, el foco queda atrapado y vuelve al disparador, el fondo se marca `inert`. Bajo 640px es una hoja inferior (`max-h-[92dvh]`). `EditDialog` es el marco de edición: Guardar deshabilitado sin cambios y confirmación al descartar.

### Inputs / Fields
- **Pantallas de acceso (`AuthInput`):** etiqueta flotante dentro del campo; al enfocar o escribir sube despacio (300 ms) y se apoya sobre el borde superior; un solo borde violeta al enfocar (`data-auth-field` apaga el contorno global); el placeholder solo se ve con el foco y solo si aporta algo distinto de la etiqueta. Esquinas redondeadas (`rounded-xl`) por excepción a D-26.
- Borde Paper Border, fondo blanco, texto 14px; foco con el anillo global violeta; error con borde rojo y mensaje de 12px. Todos los formularios usan `FormField`/`FormSelect`/`FormTextarea` (etiqueta flotante por CSS, `htmlFor`/`id`, `aria-invalid`/`aria-describedby`). Firma manuscrita: `SignaturePad` (Pointer Events, PNG).

### Navigation
- Sidebar navy profundo de 220px con grupos por rol; header navy con búsqueda por patente y chip de rol; en `/m` navegación inferior táctil.

### Tabs
- `Tabs` (controlado) y `TabNav` (rutas): subrayado violeta que se desliza, flechas/Home/End, `aria-current` en navegación. Objetivo táctil ≥44px.

### Timeline
- Lista `ol` con marcadores cuadrados por tipo (estado, orden, presupuesto, respuesta), fecha en `time`, título enlazable, sello a la derecha y detalle plegable (`aria-expanded`). La usan historial de OT, respuestas del presupuesto, ficha del vehículo y portal del dueño (sin hora).

### Data Table
- `DataTable`: `<table>` real en escritorio (marco y filas redondeados, encabezados ordenables con `aria-sort`, orden en el servidor en listas paginadas) y tarjetas en celular (talón + cuerpo + barra de acciones a todo el ancho). `fixedLayout` reparte columnas en % (Órdenes); las columnas numéricas y de estado se centran para que el espacio sea parejo. Paginación de 10 con «Mostrando 11–20 de 57».
- **Importe pendiente:** rojo (`tc-stamp-red`) si no se cobró nada, naranja si hay cobro parcial, apagado si no se debe nada (`pendingTone`).

### Public Nav
- `PublicNav`: barra navy sticky compartida por landing y pantallas de acceso; la marca vuelve a `/`; «Ingresar» y «Crear cuenta» de 44 px. Fondo de cuadrícula gris tenue (44 px) en planes y acceso.

### Ticket (signature)
- Tarjeta de documento con talón de 20% a la izquierda (Stub Blue), línea perforada vertical y sello de estado; la usan OT, presupuestos y cobros.

## Do's and Don'ts

### Do:
- **Do** usar esquinas rectas en todo control y superficie nuevos (`--radius: 0px`).
- **Do** reservar Carbon Purple para dinero y acción primaria, y mostrar el estado con sellos.
- **Do** mostrar importes en numerales tabulares alineados a la derecha.
- **Do** conservar el foco visible global de 2px violeta; no anularlo con `outline-none` sin reemplazo.
- **Do** mantener objetivos táctiles ≥44px en `/m` y en las páginas públicas.

### Don't:
- **Don't** agregar sombras en reposo ni radios redondeados a tarjetas y botones.
- **Don't** usar el violeta como relleno decorativo ni para estados.
- **Don't** introducir fuentes web ni iconos de bibliotecas pesadas sin decisión explícita (D-25).
- **Don't** comunicar estado solo con color de texto.
- **Don't** usar `text-slate-*` en código nuevo: usar los tokens `tc-text*`.
- **Don't** vendorear código de Watermelon sin decisión: los componentes son propios (ver `web/src/components/watermelon/README.md`).
