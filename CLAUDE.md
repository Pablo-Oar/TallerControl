# TallerControl

SaaS multiempresa de gestión para talleres mecánicos (alternativa propia a Vehix).
Foco del MVP: **vehículo + historial, ciclo presupuesto → orden → cobro, portal del mecánico y QR seguro para el dueño**.

## Fuente de verdad: el plan

El plan vive **fuera del repo** (es documentación interna, no se sube): `../documentacion/analisis-vehix/Plan implementaicon/` (la carpeta se llama `implementaicon`, sin "m").
Antes de implementar algo, leer el documento que corresponde. No inventar reglas: si el plan no lo dice, preguntar.

**Documento a seguir: `PlanImplementacionV2.html`** (V2, 12/10/2026, "base de desarrollo"). Consolida los documentos 00–14 en un solo archivo: cada uno es una `<section id="doc-NN">` y los cambios de V2 van en un bloque `v2-delta` dentro de cada sección. Los IDs (`REQ-xx`, `T-*`, `D-xx`, `V-xx`…) son anclas `id="…"`. Buscar con Grep por `id="doc-04"` o por el ID y leer con offset (las líneas son largas).
Los `.md` 01–11 y `V2/*.md` de la carpeta son el mismo contenido en pedazos (V1 + deltas) y sirven solo como atajo de búsqueda; **si difieren del HTML, manda el HTML**. `PlanImplementacion.html` (V1) queda como histórico: no usarlo.

Equivalencias, por si hay que ubicar algo en los `.md`:

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

ASP.NET Core 10 Web API (C#) · EF Core 10 + Npgsql · PostgreSQL 18 · React + TS (Vite) · Tailwind · TanStack Query · React Hook Form + Zod · Cloudflare R2 (RustFS en local; MinIO ya no publica imágenes) · QRCoder · PDF: QuestPDF o MigraDoc (D-05 pendiente) · Serilog.
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
Estado actual: **E0 y E1 completas; E2 completa en `feature/e2-settings`, falta el PR a Develop**. Luego E3 (clientes y vehículos). Existen el entorno local (`docker-compose.yml`, `.env.example`) y la solución .NET (`TallerControl.slnx`: 4 proyectos + 3 de pruebas, `/health/live` y `/health/ready`). Persistencia (E1): `AppDbContext` con aislamiento por taller, auditoría automática de entidades `IAudited` (`audit_events`, solo inserción), outbox (`IOutbox`), avisos y numeración por sucursal (`IDocumentNumberGenerator`, siempre dentro de la transacción del documento). `web/` tiene el SPA completa con auth (React 19 + Vite + Tailwind 4, TanStack Query, React Hook Form + Zod, ruteo con tres áreas, guards `RequireAuth`, Vitest + Playwright). CI en `.github/workflows/ci.yml` (build, format, test, lint, gitleaks) y arranque con `.\scripts\dev.ps1`. Observabilidad y errores (E1): Serilog (JSON a consola en producción, `TraceId` y `tenant_id` en cada evento, rutas saneadas por `LogSanitizer`) y errores RFC 7807 con `traceId` (`ApiExceptionHandler`: `NotFoundException`/`TenantViolationException` → 404, `BusinessRuleException` → 422, `ConflictException`/unicidad → 409, `ConcurrencyException`/`DbUpdateConcurrencyException` → 412, el resto → 500 sin detalles). El orden del pipeline está en `UseApiErrorPipeline()`. CI genera el *migrations bundle* y lo aplica en un PostgreSQL limpio.
La API usa `ConnectionStrings:Default` con el rol **`tallercontrol_app`** (sin `DELETE` ni `UPDATE` sobre `audit_events`); las migraciones (`dotnet ef`, bundle) usan el dueño por `ConnectionStrings:Migrations`. Contraseñas por `dotnet user-secrets` o variables de entorno, nunca en el repo. El rol se crea con `scripts/db/create-app-role.sql` antes de migrar (`dev.ps1` lo hace); una tabla nueva de solo inserción debe revocarle `UPDATE` en su migración.

**E2 — lo implementado (bloques 1–6, bloque 6 en `feature/e2-settings`, sin mergear):**
- Dominio: `User`, `Membership`, `Invitation`, `PasswordResetToken`, roles `TenantRole`, permisos `Permission`, `MembershipStatus`.
- Auth API: `POST /api/v1/auth/login` (cookie + antiforgery), `/logout`, `GET /me`, `/forgot-password`, `/reset-password`.
- Invitaciones: `POST /api/v1/invitations` (Owner), `POST /api/public/v1/invitations/{token}/accept`.
- Miembros: `GET /api/v1/users`, `PATCH /api/v1/users/{id}` (concurrencia por `xmin`, `If-Match`).
- Settings (bloque 6, REQ-04): `GET/PUT /api/v1/settings` (solo Owner, `settings.manage` — la matriz de doc 07 §3 manda sobre el "O, A" de doc 05), concurrencia con versión compuesta `"{tenantVersion}.{settingsVersion}"` (`SettingsVersion`, dos `xmin` en una transacción). Logo: `POST /api/v1/settings/logo/upload-url` (valida contentType/tamaño declarados, clave determinística `tenants/{tenantId}/logo`) + `POST /api/v1/settings/logo/confirm` (HEAD + magic bytes antes de persistir `logo_file_key`; flujo de confirmación agregado porque el contrato de doc 05 no lo preveía). `IObjectStorage`/`S3ObjectStorage` (AWSSDK.S3) para R2/RustFS, URLs prefirmadas PUT 5 min / GET 10 min. IVA inválido → 422 (BusinessRuleException, no el 400 literal de doc 08 T-SET-02: se mantuvo la convención ya establecida). Migración `TenantSettingsFields` agrega `address`/`phone` a `tenants` (el contrato los pedía; antes solo existían en `branches`). `DevelopmentSeeder` corregido: creaba `Tenant` sin su `TenantSettings` 1:1.
- Frontend: `LoginPage`, `ForgotPasswordPage`, `ResetPasswordPage`, `AcceptInvitationPage`, `RequireAuth`, `AuthContext`, `TallerLayout`, `/app/settings/users` (listar, editar, invitar), `/app/settings/general` (REQ-04: datos del taller, IVA, vigencia, logo con preview).
- **T-PERM-ALL y T-ISO-ALL** (doc 08, `tests/TallerControl.Api.IntegrationTests/Permissions/`): generados por reflexión sobre `EndpointDataSource` (no una lista a mano). T-PERM-ALL descubre todo endpoint `/api/v1/*` con política `perm:*` y lo prueba contra Owner/AdminStaff/Mechanic; T-ISO-ALL hace lo mismo para los que tienen parámetro de ruta (hoy solo `PATCH /api/v1/users/{membershipId}`) contra un recurso de otro taller, y falla con mensaje explícito si aparece un endpoint con `{id}` nuevo que no sepa resolver (fuerza a sumarle el recurso sembrado correspondiente).
- Tests: 155 .NET (43 Domain + 1 Application + 111 Integration, incluye T-SET-01..03, T-ISO-02/03, T-PERM-ALL, T-ISO-ALL) + 12 Vitest; Playwright configurado (E2E-01 requiere entorno).

**E2 — pendiente para cerrar:**
- PR de `feature/e2-settings` → Develop (criterio de cierre ya verificado: matriz de permisos 100% y aislamiento entre talleres, por T-PERM-ALL/T-ISO-ALL).

## Comandos

Disponibles desde E0 (actualizar esta sección al crearlos):

```
dotnet build TallerControl.slnx / dotnet test TallerControl.slnx / dotnet format TallerControl.slnx --verify-no-changes
dotnet ef migrations add <Nombre> --project src/TallerControl.Infrastructure --startup-project src/TallerControl.Api --output-dir Persistence/Migrations   # tras dotnet tool restore
# los tests de integración levantan PostgreSQL con Testcontainers: requieren Docker Desktop
npm --prefix web run dev | build | lint | test
npx playwright test                                 # E2E (requiere entorno local levantado)
npx playwright test --headed                        # E2E con navegador visible
.\scripts\dev.ps1           # todo el entorno: .env si falta, docker, API (:5233) y SPA (:5173)
copy .env.example .env     # primera vez; luego completar las contraseñas locales
docker compose up -d        # PostgreSQL 18 + RustFS/S3 en 127.0.0.1 (requiere Docker Desktop)
```

**Base de datos por MCP (`postgres`, solo lectura):** `.mcp.json` + `dbhub.toml`. Sirve para inspeccionar esquema y migraciones con `search_objects` y `execute_sql` (solo `SELECT`, 200 filas). **Nunca** se usa para modificar datos: los cambios van por migraciones de EF Core. Requiere el entorno local levantado y un `.env` creado por el usuario.

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
- Fechas `timestamptz` en **UTC**, como `DateTimeOffset` (Npgsql rechaza offset distinto de 0) y reloj `TimeProvider`; convertir a la zona del taller solo al mostrar (X6/X12).
- Toda tabla del taller lleva `tenant_id`; índices compuestos empiezan por `tenant_id`.
- **Borrado físico prohibido** en datos de negocio (FK `RESTRICT`); borrado lógico con motivo donde aplique.
- Enums como `text` en `snake_case` + `CHECK` (convención en `AppDbContext`, no a mano). Concurrencia optimista con `xmin` (`IVersioned`).
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

D-02 host, D-03 proveedor PostgreSQL, D-05 librería PDF, D-07 stock negativo (recomendado: permitir con alerta),
D-01/D-12/D-13 antes de E8 (QR). Nuevas en V2: D-16 modelo de saldo de caja, D-17 límites por plan,
D-18 Google en el panel, D-19 datos fiscales de compras, D-20 ventas de mostrador, D-21 kits.

**Decididas:** D-15 (12/10/2026): clientes y catálogo compartidos dentro del taller; caja, stock y numeración por sucursal.
**D-04 (provisoria, 12/10/2026):** el email sale por **Gmail** (SMTP con contraseña de aplicación) detrás de una interfaz `IEmailSender` y siempre desde el outbox, para poder cambiar de proveedor sin tocar el negocio. Revisar antes de E8/E9: el límite de Gmail (≈500 envíos/día) y la entregabilidad del OTP del QR (el plan pide probar 20 envíos reales).
Si una tarea depende de una, **preguntar** y registrar el valor provisorio.

## Entorno del usuario

Windows 10, PowerShell, 12 GB de RAM (WSL limitado a 4 GB en `.wslconfig`). Docker Desktop operativo. Git con Git Credential Manager (HTTPS), remoto `Pablo-Oar/TallerControl`; `gh` autenticado.
