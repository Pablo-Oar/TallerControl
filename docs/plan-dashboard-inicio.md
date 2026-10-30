# Plan: rediseño del Inicio (`/app`) como centro de comando

Estado: **plan aprobado el 30/10/2026, sin código todavía.** Decisiones D-39..D-42 abajo. Se implementa en PR chicos, cada uno con CI en verde antes del merge.

## Objetivo

El dueño o administrativo abre el sistema y en 5 segundos entiende: **cuánta plata hay, qué autos están trabados y qué clientes no respondieron.** Pantalla de *operar* (no de persuadir): escaneable, densa, accionable. Mantiene la identidad «The Boarding Pass» (tokens `tc-*`, cupones con perforación). Rojo = no pagó nada, naranja = pago parcial (mismo criterio que «Pendiente» en las órdenes, `pendingTone` en `lib/styles.ts`).

Audiencia: Dueño y Administrativo (el Inicio exige `workorders.read` **y** `payments.read`; el Mecánico y el Contador no lo ven).

## Punto de partida (inventario al 30/10/2026)

- Pantalla: un solo archivo, `web/src/areas/taller/DashboardPage.tsx` (KPIs, «Últimas órdenes», «Próximos turnos», guía de inicio).
- API: `GET /api/v1/dashboard` → `DashboardDto(TotalReceivable, OpenWorkOrders, PendingQuotes, LowStockItems)`; cálculo en `EfDashboardRepository` (4 consultas).
- Datos que ya existen sin tablas nuevas: ingresos por día en la zona horaria del taller (`reports/revenue`), saldo y sesión de caja (`cash/accounts`), `quotes.sent_at`/`total`, `work_order_status_changes.changed_at`, turnos (`appointments`), `catalog_items` (stock), `notifications`.
- No existe: fecha de vencimiento de órdenes/cobros, conteo de órdenes por estado, «último cambio de estado» agregado.
- Librerías: `motion` instalado; **no hay librería de gráficos ni de íconos**.

## Decisiones

| ID | Decisión (30/10/2026) |
|---|---|
| D-39 | «Facturas vencidas» no existe (no hay vencimiento). Se muestra **«X órdenes con saldo hace más de N días»**, contando desde la entrega (o la creación si no se entregó). N configurable (D-42). |
| D-40 | «Reenviar por WhatsApp» = botón que abre `wa.me` con el mensaje armado (el envío automático, P-03, sigue fuera de alcance). Reenviar el enlace de un presupuesto **genera uno nuevo e invalida el anterior** (`POST /quotes/{id}/public-link`); la UI lo avisa. |
| D-41 | Gráfico de ingresos de 7 días con una **librería moderna: `recharts`**, cargada solo en el chunk lazy del dashboard (no suma al chunk principal; D-27 sigue en ≤ 125 kB gzip). Colores del sistema (`tc-purple`), sin tooltip por defecto. |
| D-42 | Umbrales **configurables por taller** (Dueño, `settings.manage`): `hot_quote_hours` (presupuesto «caliente», por defecto 48, rango 1–720), `stuck_order_days` (orden trabada, por defecto 3, rango 1–60) y `receivable_aging_days` (saldo antiguo, por defecto 30, rango 1–365). Columnas nuevas en `tenant_settings` + migración + campos en `/app/settings/general`. |

## Estructura de la pantalla

1. **Fila 1 — KPIs (4):** Ingresos de hoy (caja activa) · Por cobrar (con aviso de antigüedad, D-39) · Órdenes activas desglosadas (en proceso / esperando repuestos / listas para entregar) · Presupuestos pendientes (cantidad y monto potencial).
2. **Fila 2 — Acciones rápidas:** Nueva orden · Nuevo presupuesto · Registrar cobro · Venta de mostrador (cada una solo con su permiso).
3. **Columna izquierda — pulso operativo:** «Taller en vivo» (orden, vehículo y patente, mecánico, estado; resalta las trabadas más de `stuck_order_days`) y «Turnos de hoy» (con «Convertir en orden»; avisa los turnos sin mecánico asignado).
4. **Columna derecha — comercial y alertas:** «Presupuestos calientes» (enviados hace más de `hot_quote_hours` sin respuesta, con botón WhatsApp, D-40) · estado de la caja (abierta, quién, saldo) · stock bajo (top 5, con «Comprar»).
5. **Pie:** gráfico de ingresos de los últimos 7 días (D-41). La guía de inicio se **colapsa a una línea** cuando está completa y se oculta al superar 10 órdenes.
6. **Celular:** una columna; KPIs de a 2; el resto apilado.

Cada widget tiene sus estados cargando / vacío / error y se oculta si el rol no tiene el permiso. Lo que el rol no puede ver **no viaja** en la respuesta (sección nula). «Tiempo real» = refresco cada 60 s y al volver a la pestaña (sin WebSockets: el plan prohíbe colas y Redis).

## Paso a paso

| # | PR | Contenido | Pruebas |
|---|---|---|---|
| 1 | Backend: dashboard v2 | `GET /api/v1/dashboard` ampliado: ingresos de hoy y de 7 días (zona horaria del taller), órdenes por estado + trabadas, presupuestos calientes, turnos de hoy, caja (solo con `cash.read`), stock crítico, antigüedad del saldo. Secciones nulas sin permiso. | T-DASH-03.. (permisos por rol, aislamiento entre talleres, zona horaria, bordes de umbrales). Revisión: `security-reviewer`, `architecture-reviewer`. |
| 2 | Umbrales por taller | Migración (3 columnas en `tenant_settings`, validación de rangos en el dominio), `GET/PUT /settings`, campos en `/app/settings/general`. | T-SET-.. (rangos, solo Dueño). |
| 3 | Base del frontend | Grilla del dashboard, `KpiCard`, `WidgetShell` (3 estados), consulta con refresco de 60 s, `recharts` en chunk lazy. | Vitest. |
| 4 | KPIs + acciones rápidas | Fila 1 y fila 2. | Vitest (permisos por rol). |
| 5 | Columna operativa | Taller en vivo + Turnos de hoy. | Vitest. |
| 6 | Columna comercial | Presupuestos calientes (WhatsApp `wa.me`), caja y stock. | Vitest. |
| 7 | Gráfico + guía | Gráfico de 7 días; colapso de la guía de inicio. | Vitest. |
| 8 | Pulido | `impeccable` adapt / audit / polish en 1440, 820 y 390 px; contraste y teclado; E2E Playwright del Inicio. | E2E. |
| 9 | Cierre | CLAUDE.md, DESIGN.md y este plan al día. | — |

## No negociables que aplican

- Reglas de negocio en Domain/Application; el front solo presenta. Totales y umbrales se calculan en el servidor.
- Sin tablas nuevas salvo las 3 columnas de D-42; sin MediatR/AutoMapper; sin índices nuevos (ver decisión del 27/10/2026: medir primero).
- Recurso o dato de otro taller = no existe (aislamiento por `HasQueryFilter`).
- Toda página nueva va con `React.lazy`; `text-slate-*` no se usa (tokens `tc-text*`).
