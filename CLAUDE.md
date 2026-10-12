# TallerControl

SaaS multiempresa de gestión para talleres mecánicos (alternativa propia a Vehix).
Foco del MVP: **vehículo + historial, ciclo presupuesto → orden → cobro, portal del mecánico y QR seguro para el dueño**.

## Fuente de verdad: el plan

El plan vive **fuera del repo** (es documentación interna, no se sube): `../documentacion/analisis-vehix/Plan implementaicon/` (la carpeta se llama `implementaicon`, sin "m").
Antes de implementar algo, leer el documento que corresponde. No inventar reglas: si el plan no lo dice, preguntar.

**Versión vigente: V2** (`PlanImplementacionV2.html`). Los `.md` 01–11 de la carpeta son los de V1 y se leen **junto con** los cambios de V2; si se contradicen, manda V2:

| Archivo en `V2/` | Contiene |
|---|---|
| `14_cambios_por_documento_v2.md` | Qué cambia en cada documento 01–11 (sección `## NN`) |
| `13_reglas_nuevas_por_modulo_v2.md` | Reglas nuevas por módulo, prioridad, etapa y diccionario de KPIs |
| `12_registro_verificacion_v2.md` | 80 verificaciones contra capturas de Vehix (V-01..V-80) |
| `00_novedades_v2.md` | Resumen y decisiones D-15..D-21 |

| Doc | Contiene |
|---|---|
| 01 | Alcance, REQ-01..19, prioridades P0/P1/P2, contradicciones X1–X12 |
| 02 | Stack y dependencias a evitar |
| 03 | Arquitectura, módulos, diseño del QR |
| 04 | Modelo de datos y convenciones |
| 05 | Contratos de API |
| 06 | Diseño del frontend |
| 07 | Seguridad, matriz de permisos, aislamiento |
| 08 | Plan de pruebas (IDs `T-*`, `E2E-*`) |
| 09 | Roadmap por etapas E0–E13 |
| 10 | Riesgos y decisiones pendientes D-01..D-14 |
| 11 | Matriz requisito ↔ prueba |

Skill `plan-ref` para consultar un REQ, T-* o decisión por ID.

## Stack

ASP.NET Core 10 Web API (C#) · EF Core 10 + Npgsql · PostgreSQL 18 · React + TS (Vite) · Tailwind · TanStack Query · React Hook Form + Zod · Cloudflare R2 (MinIO en local) · QRCoder · PDF: QuestPDF o MigraDoc (D-05 pendiente) · Serilog.
Tests: xUnit, Testcontainers (PostgreSQL real, **no InMemory**), WebApplicationFactory, Vitest + Testing Library, Playwright.

## Estructura

```
src/TallerControl.Domain/          entidades, value objects, reglas (no referencia a nadie)
src/TallerControl.Application/     casos de uso por módulo, DTO, interfaces
src/TallerControl.Infrastructure/  EF Core, R2, email, PDF, QR
src/TallerControl.Api/             host: endpoints, middleware, SPA estática, OpenAPI
web/                               SPA React: áreas /app (taller), /m (mecánico), pública
tests/                             Domain.Tests, Application.Tests, Api.IntegrationTests, e2e/
docs/  scripts/  .github/workflows/
```

Application y Domain se organizan **por módulo** (`Work/`, `Qr/`, `Customers/`…), no por tipo técnico.
Estado actual: **E0 en curso**, la solución .NET y `web/` todavía no existen.

## Comandos

Disponibles desde E0 (actualizar esta sección al crearlos):

```
dotnet build / dotnet test / dotnet format
npm --prefix web run dev | build | lint | test
docker compose up -d        # PostgreSQL 18 + MinIO (requiere Docker Desktop)
```

## Reglas de arquitectura (no negociables)

- **Monolito modular.** Un módulo usa solo la interfaz pública de otro, nunca sus tablas ni su DbContext. Sin dependencias circulares.
- **Reglas de negocio solo en Domain/Application.** Ni en endpoints ni en el frontend (el front valida formato, nada más).
- **Sin MediatR ni AutoMapper.** Handlers simples (`CrearOrdenHandler`) y mapeo explícito. Tampoco FluentAssertions v8+.
- **Entidades con comportamiento** para invariantes críticos (`WorkOrder.CambiarEstado()`, `Payment.Anular()`).
- Operaciones multi-módulo en **una transacción**; efectos externos (email) por **outbox**.
- Sin microservicios, colas, Redis ni GraphQL en el MVP.

## Reglas de dominio y datos

- Código y tablas en **inglés** `snake_case`; la UI en **español**.
- PK `uuid` v7 (`Guid.CreateVersion7()`). Dinero `numeric(14,2)`/`decimal`, redondeo `MidpointRounding.AwayFromZero` por renglón. Cantidades `numeric(12,3)`.
- Fechas `timestamptz` en **UTC**; convertir a la zona del taller solo al mostrar (X6/X12).
- Toda tabla del taller lleva `tenant_id`; índices compuestos empiezan por `tenant_id`.
- **Borrado físico prohibido** en datos de negocio (FK `RESTRICT`); borrado lógico con motivo donde aplique.
- Enums como `text` + `CHECK`. Concurrencia optimista con `xmin`.
- Numeración correlativa por taller vía `document_sequences` con `SELECT … FOR UPDATE`.
- Totales **siempre calculados en el servidor**.
- **V2:** `document_sequences` con clave `(tenant_id, branch_id, kind)`, `stock_movements.branch_id` y `payments.account_id` (nulo) desde el MVP.
- **V2:** una sola definición de deuda/"por cobrar": suma de `balance` de órdenes **no canceladas**, igual en listados, ficha, dashboard y reportes.
- **V2:** cambiar el precio de un servicio o producto no altera renglones ya cargados.

## Seguridad (el frontend nunca es una barrera)

- El `tenant_id` sale de la sesión (`ITenantContext`); **ningún DTO acepta `tenantId`**. Recurso de otro taller = **404**, igual que inexistente.
- Filtro global `HasQueryFilter` en toda entidad del taller; FK del cuerpo se validan contra el taller.
- Permisos por política en el endpoint; reglas dependientes del recurso ("solo asignadas", transiciones) en el caso de uso. Matriz en `07` §3.
- Montos y costos que el rol no puede ver **no viajan** en el JSON (no "vacíos").
- Adjuntos: nunca aceptar un `fileKey` del cliente; URL firmadas (PUT 5 min, GET 10 min); validar magic bytes.
- Superficies públicas: DTO dedicados, tokens solo como hash, respuestas uniformes, `no-store`, `noindex`, rate limit.
- QR: la **patente nunca es suficiente**; siempre OTP o código del cliente. La tarjeta no lleva patente.
- Nunca loguear contraseñas, OTP, tokens ni DNI. Secretos fuera del repo (`dotnet user-secrets`, env).
- Auditoría de solo inserción (`audit_events`).

## Convenciones

- **Ramas:** trabajo diario en `Develop`; una rama `feature/<etapa>-<tema>` por bloque (p. ej. `feature/e0-backend-skeleton`) que vuelve a `Develop` por PR; `Develop` → `main` al cerrar cada etapa. Proponer la rama antes de empezar un bloque.
- **Commits:** Conventional Commits en inglés (`feat:`, `fix:`, `chore:`, `test:`, `docs:`, `refactor:`). Un cambio lógico por commit.
- **No commitear ni hacer push sin que el usuario lo confirme**, y nunca `--force` ni reescribir historia publicada.
- C#: file-scoped namespaces, Allman, `_camelCase` para campos privados, `IFoo` para interfaces (ver `.editorconfig`).
- Cada requisito implementado referencia su `REQ-xx` y tiene sus pruebas `T-*` (doc 08/11).
- Cambios de alcance pasan por la clasificación de `01` (P0/P1/P2); no sumar funcionalidades P1+ al MVP.

## Decisiones aún abiertas (no asumir)

D-02 host, D-03 proveedor PostgreSQL, D-04 email, D-05 librería PDF, D-07 stock negativo (recomendado: permitir con alerta),
D-01/D-12/D-13 antes de E8 (QR). Nuevas en V2: D-15 alcance por sucursal, D-16 modelo de saldo de caja, D-17 límites por plan,
D-18 Google en el panel, D-19 datos fiscales de compras, D-20 ventas de mostrador, D-21 kits.
Si una tarea depende de una, **preguntar** y registrar el valor provisorio.

## Entorno del usuario

Windows 10, PowerShell. Git con Git Credential Manager (HTTPS), remoto `Pablo-Oar/TallerControl`. `gh` aún sin autenticar.
