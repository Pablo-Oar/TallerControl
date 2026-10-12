---
name: plan-ref
description: Consulta el plan de implementación de TallerControl por ID (REQ-xx, T-*, E2E-*, D-xx, X1–X12, RT-xx, F-xx, E0–E13) o por tema, y devuelve las reglas exactas con su fuente. Usar antes de implementar o cuando haya dudas de alcance o reglas de negocio.
---

# plan-ref

El plan está **fuera del repo**, en `../documentacion/analisis-vehix/Plan implementaicon/` (la carpeta se llama `implementaicon`).

**Fuente única: `PlanImplementacionV2.html`.** Consolida los documentos 00–14; cada uno es `<section id="doc-NN">` con sus cambios V2 en un bloque `v2-delta`, y los IDs son anclas (`id="REQ-05"`, `id="T-SET-03"`, `id="D-15"`, `id="V-11"`). Las líneas son muy largas: buscá con Grep (`-o` o `head_limit`) para obtener el número de línea y leé con `offset`/`limit`. Los `.md` de la carpeta (y `V2/`) son el mismo contenido partido y solo sirven de atajo; si difieren, manda el HTML. No usar `PlanImplementacion.html` (V1, histórico).

## Dónde buscar según el ID

La tabla indica la sección del HTML (`doc-NN`); el `.md` equivalente es un atajo.

| ID / tema | Documento |
|---|---|
| `REQ-xx`, `F-xx`, `X1–X12`, prioridad P0/P1/P2 | `01_alcance_y_mvp.md` |
| Stack, librerías, hosting, backups | `02_decisiones_tecnologicas.md` |
| Módulos, dependencias, QR, flujos | `03_arquitectura_propuesta.md` |
| Tablas, columnas, convenciones | `04_modelo_datos.md` |
| Endpoints, contratos, errores | `05_contratos_api.md` |
| Pantallas, componentes, rutas SPA | `06_diseno_frontend.md` |
| Permisos, aislamiento, rate limits, secretos | `07_seguridad_y_permisos.md` |
| `T-*`, `E2E-*` | `08_plan_de_pruebas.md` |
| Etapas `E0–E13`, estimaciones | `09_roadmap_de_desarrollo.md` |
| `D-xx`, `RT-xx`, `RF-xx`, `I-xx` | `10_riesgos_y_decisiones_pendientes.md` |
| Requisito ↔ pruebas | `11_matriz_requisitos_pruebas.md` |

## Secciones propias de V2

| Qué buscás | Sección del HTML |
|---|---|
| Resumen y decisiones D-15..D-21 | `doc-00` (Novedades) |
| Qué cambió en el documento NN | bloque `v2-delta` (`id="v2-NN"`) dentro de `doc-NN` |
| Regla nueva de un módulo, KPIs | `doc-13` (Reglas nuevas por módulo) |
| Evidencia contra Vehix (`V-01..V-80`) | `doc-12` (Registro de verificación) |

El HTML es la base de desarrollo; si un `.md` lo contradice, citá el HTML.

## Procedimiento

1. Con Grep buscá el ID exacto en `PlanImplementacionV2.html` (ancla `id="…"`) y leé alrededor con `offset`/`limit`.
2. Leé la sección completa, no solo la línea (reglas y criterios de aceptación van juntos).
3. Cruzá con `11` para listar las pruebas asociadas y con `10` por si hay una decisión pendiente que afecte.
4. Respondé con: regla literal, fuente (`archivo` §sección), dependencias, pruebas asociadas y decisiones abiertas.

## Reglas

- Citá el documento; no parafrasees de memoria.
- Si el plan no cubre el caso o dos documentos se contradicen, decilo explícitamente y preguntá; no completes con suposiciones.
- Señalá si la funcionalidad es P1/P2 (no entra al MVP).
