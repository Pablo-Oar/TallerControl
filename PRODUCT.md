# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

**Dueño del taller (owner):** gestiona el negocio completo — clientes, vehículos, presupuestos, órdenes de trabajo, cobros, caja, facturación, personal y reportes. Trabaja desde escritorio (PC de mostrador o laptop). Necesita información densa, accionable y rápida de escanear.

**Personal administrativo (admin_staff):** opera el día a día del taller — carga OTs, presupuestos, clientes, caja. Misma vista que el Owner, sin acceso a configuraciones sensibles. Escritorio.

**Mecánico (mechanic):** ve y actualiza sus OTs asignadas. Trabaja desde el celular en el taller, con las manos sucias, luz variable y poco tiempo. La vista `/m/*` debe ser mobile-first con elementos grandes y fácil de operar con una mano.

**Contador (accountant):** solo lectura en facturación (libro IVA, facturas). Acceso esporádico.

**Dueño del vehículo (público anónimo):** escanea un QR del auto para ver el estado, aprobar presupuestos y ver comprobantes de pago. No tiene cuenta. Accede desde el celular. El flujo debe ser simple y generar confianza.

## Product Purpose

TallerControl es un SaaS multi-empresa para talleres mecánicos independientes de Argentina. Cierra el ciclo completo: vehículo + historial → presupuesto → orden de trabajo → cobro → factura ARCA. Diferencia clave: el portal QR permite al dueño del auto seguir su vehículo sin instalar nada y sin contraseñas, y el mecánico opera desde su celular sin necesidad de acceder al panel de escritorio.

## Positioning

Alternativa propia a Vehix, construida sobre el mismo mercado (talleres pequeños y medianos argentinos) pero con control total del código, costos y roadmap. Es el único sistema en el segmento con ciclo presupuesto→OT→cobro→factura ARCA integrado más portal QR del dueño del vehículo.

## Operating Context

- El taller típico tiene 1–5 mecánicos y 1 persona de administración.
- El panel de escritorio se usa en mostrador o back-office; las pantallas suelen ser de 1366×768 o 1920×1080.
- El mecánico usa el celular en el piso del taller con guantes, luz directa de sol o baja, a veces con aceite en la pantalla.
- El dueño del vehículo llega al portal desde un QR impreso en una tarjeta física pegada al auto.
- La facturación ARCA (afip/arca argentina) es un proceso formal, crítico; los errores tienen consecuencias legales.

## Capabilities and Constraints

- **Tres áreas distintas:** `/app/*` (escritorio, taller), `/m/*` (mobile-first, mecánico), `/*` (público, sin auth).
- **Multi-empresa:** cada taller es un tenant aislado; el tenant_id sale de la sesión, nunca de la URL.
- **Stack:** React 19 + Vite + Tailwind 4 (utility-first, sin CSS custom salvo `@import 'tailwindcss'`) + TanStack Query + React Hook Form + Zod.
- **Componentes compartidos** en `web/src/components/ui/`: `Button`, `Modal`/`EditDialog`, `FormField`/`FormSelect`/`FormTextarea`, `DataTable`, `Pagination`, `Tabs`, `Timeline`, `StatusBadge`/`EditableStatusBadge`, `SearchPicker`/`EntityPickers`, `RowMenu`, `AuthCard`/`AuthInput`, `PublicNav`, `dialogs`. Pantallas nuevas deben usarlos en vez de código inline.
- **Sin librería de componentes externa** (ni shadcn, MUI ni Radix; Watermelon se evaluó y no se vendoreó nada). Instalado: `motion`. Gráficos del Inicio: `recharts` en el chunk lazy del dashboard (D-41).
- **Estado visual actual:** sistema «The Boarding Pass» consolidado (UI-R2 + auditoría de UI de 26–30/10/2026): tokens `tc-*`, navy + carbon purple, sellos de estado, talón perforado, contraste WCAG AA y objetivos táctiles de 44 px. Detalle en `DESIGN.md`.
- **Idioma:** español rioplatense (Argentina). Moneda ARS.
- **Accesibilidad:** básica requerida (contraste WCAG AA, navegación por teclado en formularios críticos).

## Brand Commitments

Identidad vigente (D-26): se mantiene la paleta `tc-*`, esquinas rectas en la app (`--radius: 0`) y bordes redondeados solo en las pantallas de acceso y en las tablas (`DataTable`). Wordmark provisorio «TALLERCONTROL» con «CONTROL» en violeta.

## Evidence on Hand

- Código fuente completo: `web/src/`.
- La aplicación se levanta con `npm --prefix web run dev` (Vite en localhost:5173).
- La API corre en localhost:5233 (levantada por separado con `.\scripts\dev.ps1`).
- Guía de estilos: `DESIGN.md` (generada del código). Sin logo de marca todavía (el ícono es «TC» sobre azul `#0a1628`), sin Figma.
- Plan vigente del Inicio: `docs/plan-dashboard-inicio.md`.

## Product Principles

1. **Densidad al servicio de la velocidad:** el dueño del taller mira el sistema varias horas por día. La interfaz debe ser escaneable, no decorativa. La información llega sin clics extra.
2. **El mecánico nunca se pierde:** la vista `/m/*` es para el celular en condiciones difíciles. Grande, claro, directo al estado de la OT.
3. **Confianza para el cliente del taller:** el portal público transmite profesionalismo y transparencia. El dueño del vehículo debe sentir que su taller usa algo serio.
4. **Consistencia como respeto al usuario:** los componentes se comportan igual en todos los módulos. Nunca hay que adivinar dónde está el botón de guardar.
5. **Funcionalidad primero:** el diseño nunca oculta funciones existentes ni agrega complejidad visual que distraiga de la tarea.

## Accessibility & Inclusion

WCAG 2.1 AA mínimo en contraste de texto y elementos interactivos. Navegación por teclado funcional en formularios y modales. El portal público debe funcionar en celulares básicos con pantallas de 390px de ancho.
