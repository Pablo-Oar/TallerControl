---
name: plan-ref
description: Consulta el plan de implementación de TallerControl por ID (REQ-xx, T-*, E2E-*, D-xx, X1–X12, RT-xx, F-xx, E0–E13) o por tema, y devuelve las reglas exactas con su fuente. Usar antes de implementar o cuando haya dudas de alcance o reglas de negocio.
---

# plan-ref

El plan está **fuera del repo**, en `../documentacion/analisis-vehix/Plan implementaicon/` (la carpeta se llama `implementaicon`).

## Dónde buscar según el ID

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

## Versión 2 (manda sobre V1)

Los `.md` 01–11 son de V1. Los cambios de V2 están en `V2/`:

| Qué buscás | Dónde |
|---|---|
| Qué cambió en el documento NN | `V2/14_cambios_por_documento_v2.md`, sección `## NN` |
| Regla nueva de un módulo, KPIs | `V2/13_reglas_nuevas_por_modulo_v2.md` |
| Evidencia contra Vehix (`V-01..V-80`) | `V2/12_registro_verificacion_v2.md` |
| Decisiones `D-15..D-21`, funcionalidades `F-53..F-58`, pruebas nuevas | `V2/14_...` (secciones 10, 01 y 08) |

Buscá siempre también en `V2/`; si V1 y V2 se contradicen, citá V2.

## Procedimiento

1. Con Grep buscá el ID exacto en la carpeta del plan **y en `V2/`** (`output_mode: content`, con contexto).
2. Leé la sección completa, no solo la línea (reglas y criterios de aceptación van juntos).
3. Cruzá con `11` para listar las pruebas asociadas y con `10` por si hay una decisión pendiente que afecte.
4. Respondé con: regla literal, fuente (`archivo` §sección), dependencias, pruebas asociadas y decisiones abiertas.

## Reglas

- Citá el documento; no parafrasees de memoria.
- Si el plan no cubre el caso o dos documentos se contradicen, decilo explícitamente y preguntá; no completes con suposiciones.
- Señalá si la funcionalidad es P1/P2 (no entra al MVP).
