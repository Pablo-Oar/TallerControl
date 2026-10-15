# TallerControl

SaaS multiempresa de gestión para talleres mecánicos (alternativa propia a Vehix).
Foco del MVP: **vehículo + historial, ciclo presupuesto → orden → cobro, portal del mecánico y QR seguro para el dueño**.

## Fuente de verdad: el plan

El plan vive **fuera del repo** (es documentación interna, no se sube): `../documentacion/analisis-vehix/Plan implementaicon/` (la carpeta se llama `implementaicon`, sin "m").
Antes de implementar algo, leer el documento que corresponde. No inventar reglas: si el plan no lo dice, preguntar.

**Documento a seguir: `PlanImplementacionV3.html`** (V3, 15/10/2026, "estado real del proyecto"). Reemplaza a V2 como referencia principal. Estructura por secciones `<section id="doc-NN">`:

| doc | Contiene |
|---|---|
| doc-00 | Resumen V3 y diferencias con V2 |
| doc-01 | Estado por etapa (E0–E10 completas, E11–E13 pendientes) |
| doc-02 | Decisiones D-01..D-22 con estado y valor elegido |
| doc-03 | Stack confirmado con versiones reales |
| doc-04 | Arquitectura implementada (capas, módulos, interfaces por entidad) |
| doc-05 | Modelo de datos (17 migraciones en orden) |
| doc-06 | API implementada (todos los endpoints) |
| doc-07 | Frontend implementado (todas las pantallas por área) |
| doc-08 | Tests implementados (IDs por módulo, E2E specs) |
| doc-09 | Roadmap pendiente E11–E13 + agenda de turnos sin etapa |
| doc-10 | Decisiones abiertas para E11+ |
| doc-11 | Patrones y convenciones establecidos en la práctica |

Los IDs de V2 (`REQ-xx`, `T-*`, `D-xx`) siguen siendo válidos y están referenciados en V3.
`PlanImplementacionV2.html` queda como histórico: no usarlo como referencia activa.
`PlanImplementacion.html` (V1) también queda como histórico.
Los `.md` de la carpeta `V2/` sirven solo como atajo de búsqueda de los IDs originales.

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
Estado actual: **E0–E10 completas, mergeadas a Develop (PR #28, 15/10/2026). Sigue E11.** Existen el entorno local (`docker-compose.yml`, `.env.example`) y la solución .NET (`TallerControl.slnx`: 4 proyectos + 3 de pruebas, `/health/live` y `/health/ready`). Persistencia (E1): `AppDbContext` con aislamiento por taller, auditoría automática de entidades `IAudited` (`audit_events`, solo inserción), outbox (`IOutbox`), avisos y numeración por sucursal (`IDocumentNumberGenerator`, siempre dentro de la transacción del documento). `web/` tiene el SPA completa con auth (React 19 + Vite + Tailwind 4, TanStack Query, React Hook Form + Zod, ruteo con tres áreas, guards `RequireAuth`, Vitest + Playwright). CI en `.github/workflows/ci.yml` (build, format, test, lint, gitleaks) y arranque con `.\scripts\dev.ps1`. Observabilidad y errores (E1): Serilog (JSON a consola en producción, `TraceId` y `tenant_id` en cada evento, rutas saneadas por `LogSanitizer`) y errores RFC 7807 con `traceId` (`ApiExceptionHandler`: `NotFoundException`/`TenantViolationException` → 404, `BusinessRuleException` → 422, `ConflictException`/unicidad → 409, `ConcurrencyException`/`DbUpdateConcurrencyException` → 412, el resto → 500 sin detalles). El orden del pipeline está en `UseApiErrorPipeline()`. CI genera el *migrations bundle* y lo aplica en un PostgreSQL limpio.
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

**E3 — lo implementado (`feature/e3-customers-vehicles`, sin mergear):**
- Dominio: `Customer` (soft delete, xmin, PhoneNormalizer E.164, VatCondition, DocType), `Vehicle` (soft delete, xmin, patente normalizada, guardia de odómetro), `IAuditable`, `ISoftDeletable`.
- AppDbContext: filtro combinado tenant+soft-delete (`HasQueryFilter` único), auto-timestamps (`IAuditable`), guardia de borrado físico en `PrepareChanges`.
- Application: handlers CRUD + restore para ambos módulos; `Page<T>`; stubs balance/órdenes (E4) e historial (E4); `ICustomerRepository`, `IVehicleRepository`.
- Infrastructure: `EfCustomerRepository` (ILike, citext, GIN trgm), `EfVehicleRepository`; configuraciones con índices parciales únicos (patente, VIN, doc).
- API: `CustomersEndpoints`, `VehiclesEndpoints`; corrección de odómetro via `IAuthorizationService` (vehicles.odometer.correct).
- Migration `CustomersVehicles`: tablas `customers` y `vehicles`, GIN trgm en nombre, índices parciales.
- Tests: 194 .NET (64 Domain + 130 Integration; T-CUS-01..08, T-VEH-01..09, T-ISO-01, T-PERM-ALL/T-ISO-ALL expandidos) + 12 Vitest.
- Frontend: `api/customers.ts`, `api/vehicles.ts`, `CustomersPage`, `CustomerFormModal`, `VehiclesPage`, `VehicleFormModal`; nav en `TallerLayout`; `asyncUtilTimeout` subido a 3000ms en setup.

**E4 — lo implementado (`feature/e4-catalog-stock`, mergeado a Develop 13/10/2026):**
- Dominio: `CatalogItem` (soft delete, xmin, invariantes de precio/stock, D-07: stock negativo permitido con alerta), `StockMovement` (append-only, REVOKE UPDATE al rol app), enums `CatalogItemKind` / `StockMovementReason`.
- Application: `SearchCatalogHandler`, `GetCatalogItemHandler`, `CreateCatalogItemHandler`, `UpdateCatalogItemHandler`, `DeleteCatalogItemHandler`, `RestoreCatalogItemHandler`, `GetStockMovementsHandler`, `ManualStockAdjustmentHandler`; `ICatalogRepository`; `CatalogDtos`. El handler de ajuste verifica existencia del ítem **antes** de resolver la sucursal (garantiza 404 en T-ISO-ALL). Fallback a sucursal principal via `GetMainBranchIdAsync` (MVP: no hay selector de sucursal aún — `IBranchContext.BranchId` siempre es null en `SessionTenantContext`).
- Infrastructure: `EfCatalogRepository` (ILike/citext, `GetMainBranchIdAsync` consulta `branches` donde `IsMain == true`); `CatalogItemConfiguration` (GIN trgm en nombre, índice único parcial por SKU, `numeric(12,3)` para cantidades); `StockMovementConfiguration`.
- API: `CatalogEndpoints` — 8 endpoints: `GET /api/v1/catalog`, `GET /api/v1/catalog/{id}`, `POST /api/v1/catalog`, `PUT /api/v1/catalog/{id}` (xmin/If-Match), `DELETE /api/v1/catalog/{id}`, `POST /api/v1/catalog/{id}/restore`, `POST /api/v1/catalog/{id}/stock-adjustments`, `GET /api/v1/catalog/{id}/stock-movements`. Permisos: `catalog.read`, `catalog.write`, `stock.adjust`.
- Migration `CatalogStock`: tablas `catalog_items` + `stock_movements`; `REVOKE UPDATE ON stock_movements FROM tallercontrol_app`.
- Tests: 220 .NET (77 Domain + 1 Application + 142 Integration; T-CAT-01..06, T-STK-01..02, T-PERM-ALL/T-ISO-ALL expandidos con `ForeignCatalogItemId` en `PermissionMatrixWebApplicationFactory`; `Branch.CreateMain` en todas las factories) + 12 Vitest. Nota: el test E2E de routes es intermitente por tiempo de setup de jsdom — `asyncUtilTimeout` en 5000ms.
- Frontend: `api/catalog.ts`, `CatalogPage` (filtros tipo/activos, paginación, badge stock bajo), `CatalogFormModal` (xmin/If-Match, campos condicionales por `trackStock`), `StockAdjustmentModal` (cantidad +/−, nota obligatoria). Fix CI: `z.coerce.number()` → `z.number()` + `valueAsNumber: true` (input type de zod coerce es `unknown`, incompatible con zodResolver). Nav "Catálogo" en `TallerLayout`; ruta `/app/catalog` en `TallerArea`.
- Skills nuevas: `resumen-sesion` (cierre de etapa con subagente Explore + actualiza CLAUDE.md); `start-etapa` mejorada con Paso 0 de snapshot de codebase vía Explore.

**E5 — lo implementado (`feature/e5-work-orders`, 13/10/2026):**
- Dominio: `WorkOrder` (máquina de estados Pending→InProgress/WaitingParts/Completed/Delivered/Cancelled, soft-cancel, invariantes de precio, `balance` calculado), `WorkOrderItem` (renglón con net/vat/total), `WorkOrderStatusChange` (append-only), `Attachment` (adjuntos polimórficos owner_type+owner_id), enums `WorkOrderStatus`, `WorkOrderPriority`, `WorkOrderItemKind`, `StockMovementReason.WorkOrder`.
- Application: handlers CRUD completo de OTs + `CreateWorkOrderHandler` (transaction via `IWorkOrderTransaction`, descuento de stock, actualización odómetro), `ChangeWorkOrderStatusHandler`, `AssignMechanicHandler` (self-assign para mecánicos via `workorders.assign_self`), `UpdateMechanicNotesHandler`, `GetWorkOrderHistoryHandler`, `GetWorkOrderPdfHandler`; handlers de adjuntos (`RequestAttachmentUploadUrl`, `ConfirmAttachment`, `ListAttachments`, `UpdateAttachment`, `DeleteAttachment`); interfaces `IWorkOrderRepository`, `IAttachmentRepository`, `IWorkOrderPdfGenerator`, `IWorkOrderTransaction`.
- Infrastructure: `EfWorkOrderRepository` (fallback sucursal principal, `SqlQuery<decimal>` con alias `"Value"` para columna `balance`), `EfAttachmentRepository`; configuraciones EF; `WorkOrderPdfGenerator` (QuestPDF Community 2024.12.1, `LicenseType.Community`, crédito en pie de página); migración `WorkOrdersAndAttachments` (tablas `work_orders`, `work_order_items`, `work_order_status_changes`, `attachments`; `REVOKE UPDATE/DELETE ON work_order_status_changes`; columna generada `balance`).
- API: `WorkOrdersEndpoints` (9 endpoints; `HasAnyPermissionRequirement`/`HasAnyPermissionHandler` para OR-logic en assign), `AttachmentsEndpoints`; `UnauthorizedAccessException` → 403 en `ApiExceptionHandler`.
- Transacción en creación: `IWorkOrderTransaction` (interfaz en Application, implementada en Infrastructure) para que `DocumentNumberGenerator` pueda ejecutar `SELECT FOR UPDATE` dentro de una transacción activa.
- Tests: 238 .NET (77 Domain + 1 Application + 160 Integration; T-WO-01..14, T-ATT-01..07, T-PERM-05/07, T-PERM-ALL/T-ISO-ALL expandidos con `ForeignWorkOrderId`/`ForeignAttachmentId`) + 12 Vitest. Test `Sin_sucursal_en_sesion_usa_la_sucursal_principal` reemplaza el caso que esperaba excepción (MVP fallback).
- Frontend taller: `workOrders.ts`, `attachments.ts`, `WorkOrdersPage` (lista paginada con filtro estado), `WorkOrderDetailPage` (detalle completo, ítems condicionales por precio), `WorkOrderFormModal` (búsqueda de cliente/vehículo, catalog picker, items con `useFieldArray`), `ChangeStatusModal` (transiciones válidas por estado, motivo de cancelación).
- Frontend mecánico (`/m`): `MechanicWorkOrdersPage` (cards mobile-first con filtros de estado), `MechanicWorkOrderDetailPage` (vista sin precios, notas inline editables, transiciones del mecánico); `MecanicoArea` actualizada con rutas.
- PDF: `GET /api/v1/work-orders/{id}/pdf` → bytes PDF con layout A4 (info cliente/vehículo/mecánico, ítems, totales opcionales, historial abreviado, crédito QuestPDF en pie).

**E6 — lo implementado (`feature/e6-quotes`, 14/10/2026):**
- Dominio: `Quote` (máquina de estados Draft→Sent→Approved/PartiallyApproved/Rejected/Expired/Converted/Cancelled, totales calculados `SubtotalNet`/`DiscountAmount`/`VatAmount`/`Total`/`ApprovedTotal`, `GlobalDiscountPct` 0–100%), `QuoteItem` (renglón con kind/net/vat/total, `RequiredForApproval`), `QuoteResponse` + `QuoteResponseItem` (historial de respuestas, `IsCurrent`), `PublicLink` (token 256 bits; solo hash SHA-256 en BD; `TargetType`/`TargetId`; revocable; expira), enums `QuoteStatus`, `QuoteItemKind`, `PublicLinkTargetType`.
- Application: handlers CRUD de presupuestos (`Create`, `Get`, `Search`, `Update`, `Delete`, `Send`, `Reopen`, `Cancel`, `Convert`), `GetQuotePdfHandler`, `GetQuoteResponsesHandler`; handlers públicos (`GetPublicQuoteHandler`, `SubmitPublicResponseHandler`, `GetPublicWorkOrderHandler`); `GetWorkOrderPublicLinkHandler` (revoca links activos previos, genera token fresco de 180 días); interfaces `IQuoteRepository`, `IPublicLinkRepository`, `IQuotePdfGenerator`; DTOs dedicados para superficie pública (sin `InternalNotes`, sin datos del taller).
- Infrastructure: `EfQuoteRepository` (ILike/citext, expira automáticamente quotes con `ValidUntil < now` al listar), `EfPublicLinkRepository` (`FindByTokenHashAsync` con `IgnoreQueryFilters`; `FindActiveByTargetAsync` con tenant filter activo); configuraciones EF; `QuotePdfGenerator` (QuestPDF Community); migración `QuotesAndPublicLinks` (tablas `quotes`, `quote_items`, `quote_responses`, `quote_response_items`, `public_links`; `REVOKE UPDATE/DELETE ON quote_responses`).
- API: `QuotesEndpoints` (11 endpoints: CRUD + send/reopen/cancel/convert + responses + PDF); `PublicEndpoints` (3 anónimos: GET presupuesto, POST respuesta, GET OT); endpoint filter `WithPublicSecurity` que fija `Cache-Control: no-store` / `X-Robots-Tag: noindex` / `Referrer-Policy: no-referrer` e intercepta `NotFoundException` → 404 uniforme; `GET /api/v1/work-orders/{id}/public-link` siempre revoca y emite token nuevo (token base64url solo en respuesta, nunca persiste). Base64Url: `Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_')`.
- Tests: 265 .NET (87 Domain + 1 Application + 177 Integration; T-QUO-01..12, T-PUB-01..06, T-DOC-01, T-PERM-ALL/T-ISO-ALL expandidos con `ForeignQuoteId` en `PermissionMatrixWebApplicationFactory`) + 12 Vitest. Fix: `testTimeout` de Vitest subido a 15 s (cadena lazy / → /app → /login es más pesada con cada etapa).
- Frontend taller: `api/quotes.ts`, `QuotesPage` (lista paginada con filtro estado), `QuoteFormModal` (búsqueda cliente/vehículo, catalog picker, items con `useFieldArray`), `QuoteDetailPage` (detalle + acciones send/reopen/cancel/convert/PDF/enlace OT + respuestas del cliente); nav "Presupuestos" en `TallerLayout`; rutas `/app/quotes` y `/app/quotes/:id` en `TallerArea`.
- Frontend público: `PublicQuotePage` (`/p/:token`, vista + formulario aprobación/rechazo), `PublicWorkOrderPage` (`/o/:token`, estado de la OT); rutas en `PublicoArea`. `contentHash` en `PublicQuoteDto` permite al frontend verificar que no hubo cambios entre visualización y firma.
- Estado actual: **E6 completa, mergeada a Develop (PR #23, 14/10/2026). Sigue E7.**

**E7 — lo implementado (`feature/e7-payments`, mergeado a Develop 14/10/2026):**
- Dominio: `Payment` (invariante monto > 0, estado Valid/Voided, `Anular()`, token hash 256 bits, idempotencia, AccountId nulo D-16), enums `PaymentMethod`/`PaymentStatus`; `WorkOrder.AplicarPago()` y `AnularPago()` (guarda D-09 ya existía).
- Application: `CreatePaymentHandler` (idempotencia por `FindByIdempotencyKeyAsync`, token público base64url, `IWorkOrderTransaction` para atomicidad, número vía `DocumentKind.Payment`), `VoidPaymentHandler` (409 si ya anulado, restaura `PaidAmount`), `GetPaymentsHandler`, `GetPaymentPdfHandler`, `GetPublicPaymentHandler` (sin datos del taller); `IPaymentRepository`, `IPaymentPdfGenerator`; DTOs `PaymentDto`/`WorkOrderPaymentsDto`/`PublicPaymentDto`.
- Infrastructure: `EfPaymentRepository` (`FindByTokenHashAsync` con `IgnoreQueryFilters()` para acceso público), `PaymentConfiguration` (índice único parcial para idempotencia, índice por token hash), `PaymentPdfGenerator` (QuestPDF Community, join payments/work_orders/customers/vehicles).
- API: `PaymentsEndpoints` (4 endpoints: GET pagos, POST pago, POST void, GET PDF); `PublicEndpoints` actualizado con `GET /api/public/v1/payments/{token}`; `Program.cs` con `MapPaymentsEndpoints`.
- Migración `Payments`: tabla `payments`, índices.
- Tests: 279 .NET (92 Domain + 1 Application + 186 Integration; T-PAY-01..06, T-DOC-03, T-PUB-07, T-PERM-ALL/T-ISO-ALL expandidos con `ForeignPaymentId` en `PermissionMatrixWebApplicationFactory`) + 12 Vitest. Corrección: `FindByTokenHashAsync` necesita `IgnoreQueryFilters()` (tenant context nulo en acceso público).
- Frontend: `api/payments.ts`, `PublicPaymentPage` (`/c/:token`), sección de cobros en `WorkOrderDetailPage` (lista, crear, anular, PDF), `CreatePaymentModal`, `VoidPaymentDialog`. Ruta `/c/:token` en `PublicoArea`.

**E8 — lo implementado (`feature/e8-qr`, mergeado a Develop PR #25, 14/10/2026):**
- Dominio: `QrCard` (máquina de estados Free→Assigned↔Lost, cualquier→Revoked irreversible; `Assign()`, `Unassign()`, `MarkLost()`, `UnmarkLost()`, `Revoke(reason)` con guard de motivo vacío), `OwnerVerification` (desafío OTP email o código Base32 del cliente; hash SHA-256 del token, expira 15 min), enums `QrCardStatus`; `QrCodeGenerator` (Base32 Crockford 10 chars, alfabeto `0-9A-HJKMNPQRSTVWXYZ`, ~50 bits de entropía); índice único parcial `(tenant_id, vehicle_id) WHERE status IN ('assigned','lost')` — un solo card activo por vehículo.
- Application: `GenerateQrBatchHandler` (genera N tarjetas libres, número vía `DocumentKind.QrCard`), `AssignQrCardHandler` (busca tarjeta por código, verifica estado Free, verifica vehículo del taller), `UnassignQrCardHandler`, `MarkQrCardLostHandler`, `RevokeQrCardHandler`; `GetQrCardHandler`, `SearchQrCardsHandler`; handlers públicos: `ScanQrCodeHandler` (solo valida que existe; 202 uniforme para Free/Assigned/Lost; 404 para Revoked/inexistente), `StartOwnerChallengeHandler` (busca por placa+código; respuesta 202 siempre para no revelar existencia; emite OTP o acepta código Base32 del cliente según D-01), `ValidateOwnerOtpHandler` (valida hash, emite cookie `tc_qr` 30 min firmada con Data Protection API), `GetOwnerVehicleHandler` (lee cookie, devuelve datos del vehículo + historial abreviado); `GetQrPrintHandler` (PDF tarjeta o PNG QR); `IQrCardRepository`, `IOwnerVerificationRepository`; `QrOptions` (BaseUrl, D-12).
- Infrastructure: `EfQrCardRepository`, `EfOwnerVerificationRepository` (ambos con `IgnoreQueryFilters()` en métodos públicos sin tenant context); `QrCardConfiguration` (índice único parcial por vehículo, índice por código); `QrCardPdfGenerator` (QuestPDF Community, tarjeta A6 sin patente per REQ-17 CA-3, QR embebido); `TurnstileVerifier` (Cloudflare anti-bot; test key `1x0000000000000000000000000000000AA` siempre pasa); `Microsoft.AspNetCore.DataProtection.Abstractions` (no el paquete completo, evita dep transitiva vulnerable en `System.Security.Cryptography.Xml`).
- API: `QrCardsEndpoints` (9 endpoints autenticados: batch, assign, unassign, mark-lost, revoke, search, get, print/PDF, print/PNG); `PublicEndpoints` actualizado (3 anónimos: GET scan, POST challenge, POST validate-otp, GET owner-vehicle); cookie `tc_qr` HttpOnly/Secure/SameSite=Strict/30min; `WithPublicSecurity` en todos los QR públicos.
- Migración `QrCardsAndVerifications`: tablas `qr_cards` y `owner_verifications`; índice único parcial por vehículo; `REVOKE UPDATE/DELETE ON owner_verifications` (append-only).
- Tests: 314 .NET (106 Domain + 1 Application + 207 Integration; T-QR-01..23, T-DOC-04, T-PERM-ALL/T-ISO-ALL expandidos con `ForeignQrCardId` en `PermissionMatrixWebApplicationFactory`) + 12 Vitest. Fix clave: cookie `tc_qr` tiene `Secure=true` — en tests HTTP el `CookieContainer` no la reenvía; se extrae manualmente del header `Set-Cookie` y se inyecta como header `Cookie` en la petición siguiente. Fixes de CI post-merge: `AssignQrCardHandler` usaba `CustomerExistsAsync` para verificar vehículo (404 incorrecto → corregido a `FindByIdAsync`); `?status=free` no era parseado por ASP.NET como enum (corregido a string + `Enum.TryParse`); `Page<T>` constructor en orden incorrecto (fijo); `TurnstileVerifier` no podía ser bypaseado con `UseSetting` (reemplazado por stub `AlwaysPassTurnstileVerifier` via DI en `QrWebApplicationFactory`); `ValidateOwnerOtpHandler` y `GetOwnerVehicleHandler` usaban `FindByIdAsync` en rutas públicas sin tenant → devolvía null (`HasQueryFilter` por `tenant_id`); corregido agregando `FindByIdPublicAsync` con `IgnoreQueryFilters()` a `IQrRepository`/`EfQrRepository`.
- Frontend taller: `api/qrCards.ts`, `QrCardsPage` (`/app/qr`): lista, filtros, modal generación de lote, modal asignación por código, marcar perdida/encontrada, revocar (irreversible, requiere motivo), link PDF; nav "Tarjetas QR" en `TallerLayout` (Owner y AdminStaff solamente).
- Frontend público: `PublicQrPage` (`/q/:code`): portal del dueño — 3 pasos (resultado del scan → formulario placa+código → ingreso OTP); `OwnerVehiclePage` (`/q/:code/mi-vehiculo`): vista del vehículo con historial (requiere cookie `tc_qr`; redirige al desafío si expira o falla). Rutas en `PublicoArea`. Turnstile embebido en el desafío.
- Decisiones tomadas: D-01 (opción a: código Base32 de 8 chars como alternativa al OTP email), D-12 (URL base `https://tallercontrol.com.ar/q/{code}` configurable vía `QrOptions:BaseUrl`), D-13 (Cloudflare Turnstile).

**E9 — lo implementado (PRs #26 y #27, 15/10/2026, mergeados a Develop):**
- Bloque 1 — Data Protection persistida en BD (`PersistKeysToDbContext<AppDbContext>()`): migración `DataProtectionKeys` (manual + Designer file con `[DbContext(typeof(AppDbContext))]`); tabla `data_protection_keys`. Resuelve el pendiente crítico de deploy.
- Bloque 2 — Rate limiting (doc 07 §6): `AddRateLimiter` con sliding window 100 req/min por IP; excluye `/health/*`.
- Bloque 3 — Security headers (doc 07 §11): `UseApiSecurityHeaders()` — `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy`, `Cache-Control: no-store` en `/api/*`.
- Bloque 4 — Dashboard (T-DASH-01, T-DASH-02): `GET /api/v1/dashboard` (inline policy AND workorders.read + payments.read) — 4 KPIs: `totalReceivable` (shadow property `balance`), `openWorkOrders`, `pendingQuotes`, `lowStockItems`. Campo `home` en `/me` (`/app` o `/m` según rol). `DashboardPage.tsx`.
- Bloque 5 — Tests V2-delta: T-PERM-08 (domain), T-AUTH-09 (mecánico → `home=/m`), T-PERM-09 (mecánico sin `prices.view` no ve montos), T-QR-31 (escaneo público sin datos del proveedor).
- Bloque 6 — E2E-02..08 (Playwright): `tests/e2e/helpers.ts` + 7 specs — E2E-02 (cliente→vehículo→OT→historial), E2E-03 (presupuesto→aprobación parcial→OT, 2 contextos), E2E-04 (mecánico móvil→autoasignación→completar), E2E-05 (pagos→entrega→PDF), E2E-06 (QR batch→asignación→portal público→Base32→dueño), E2E-07 (OT con producto→cancelar→stock repuesto, balance 0), E2E-08 (mecánico sin Pagos, `/app/*`→`/m`). Todos saltan con `E2E_RUN!=1`.
- Bloque 7 — D-17 plan tier: `Tenant.PlanCode = "free"` (default), expuesto en `GET /me → MeResponse.PlanCode`; migración `TenantPlanCode` (columna `plan_code text NOT NULL DEFAULT 'free'`).
- D-11 — Aviso de privacidad: `PrivacidadPage.tsx` en `/privacidad` (Ley 25.326 argentina); link en pie de `PublicQrPage`.
- Tests: 321 .NET (107 Domain + 1 Application + 213 Integration) + 12 Vitest + 8 E2E specs (env-dependent).
- Endpoints: 14 archivos (`DashboardEndpoints` nuevo respecto a E8).
- Migraciones totales: 15 (`DataProtectionKeys` + `TenantPlanCode` nuevas).

**E10 — lo implementado (`feature/e10-self-registration`, mergeado a Develop PR #28, 15/10/2026):**
- Dominio: `EmailVerificationToken` (token hash SHA-256 de 64 bytes, expira 24 h, `MarkAsUsed()`, `IsExpired()`); `User.EmailVerified` + `User.TenantId?` (nulo hasta verificar); método `User.MarkEmailVerified()`.
- Application: `RegisterTenantHandler` (crea `User` + `Tenant` + `Branch` principal + `TenantSettings` + `Membership` + token de verificación + `OutboxMessage email.email_verification`; respuesta uniforme 201 aunque el email ya exista — sin revelar existencia); `VerifyEmailHandler` (marca token como usado, activa `User.EmailVerified`); `IRegistrationRepository` (`FindUserByEmailAsync`, `FindTokenByHashAsync`); `RegistrationDtos`.
- Infrastructure: `EfRegistrationRepository`; `EmailVerificationTokenConfiguration` (FK `HasOne<User>().WithMany()`, índice único en `token_hash`); migración `Registration` (tabla `email_verification_tokens`); migración `EmailVerificationFk` (alinea nombre de FK con convención de EF: `fk_email_verification_tokens_user_user_id`, agrega índice `ix_email_verification_tokens_user_id`).
- API: `RegistrationEndpoints` — 2 endpoints: `POST /api/public/v1/register` (sin auth, sin CSRF, rate-limited, `WithPublicSecurity`) y `GET /api/public/v1/verify-email?token=…` (sin auth); `PublicSecurityExtensions` (mueve `WithPublicSecurity` helper a clase propia); `Program.cs` con `MapRegistrationEndpoints`.
- Login: `LoginHandler` bloquea login si `!user.EmailVerified` → `EmailNotVerifiedException` → 401 con `{ code: "email_not_verified" }`; `AuthExceptions.EmailNotVerifiedException`; `ApiExceptionHandler` mapea al 401 con código.
- Rate limiting: excepción para `/api/public/v1/register` y `/api/public/v1/verify-email` (misma política 100 req/min/IP).
- Tests: 327 .NET (107 Domain + 1 Application + 219 Integration; T-REG-01..06) + 12 Vitest. Fix clave: `EmailVerificationTokenConfiguration` requería `HasOne<User>().WithMany().HasForeignKey(t => t.UserId)` para que EF ordene correctamente el INSERT (User antes que EmailVerificationToken en el mismo SaveChanges).
- Frontend: `LandingPage.tsx` (`/` — página de bienvenida pública con links a Ingresar y Registrarse), `RegisterPage.tsx` (`/registro` — formulario nombre del taller, nombre personal, email, contraseña; on success muestra aviso de verificación pendiente), `VerifyEmailPage.tsx` (`/verificar-email?token=…` — llama al endpoint y muestra éxito o error); rutas en `PublicoArea`; `LoginPage` con link a `/registro`. `api/registration.ts` con `registerTenant` y `verifyEmail`.
- Routing: `/` ya no redirige a `/app`, muestra `LandingPage`; `/app` sin sesión sigue redirigiendo a `/login` vía `RequireAuth`.
- Bugfixes post-merge (commit `6fcbde8`): `scripts/dev.ps1` agrega `--context AppDbContext` a ef database update (dos DbContexts desde E10); `VerifyEmailPage` captura `ApiError` 400 y muestra el mensaje del servidor en lugar del genérico; `TallerLayout.handleLogout` mueve `navigate('/login')` al bloque `finally` para redirigir aunque `logout()` lance error de antiforgery.
- Estado actual: **E0–E10 completas, mergeadas a Develop (PR #28, 15/10/2026). E11 en `feature/e11-cash-purchases` (B1–B11 implementados, pendiente merge y PR).**

**E11 — lo implementado (`feature/e11-cash-purchases`, 15/10/2026, pendiente merge):**
- Dominio: `CashAccount` (B/O, xmin, soft-delete), `CashSession` (open/closed, append-NOT), `CashMovement` (append-only, REVOKE UPDATE), `CashAccountKind`, `CashSessionStatus`, `CashMovementDirection`, `CashMovementReason`; `Supplier` (xmin, soft-delete, CUIT normalizado, VatCondition); `Purchase` (Draft→Confirmed/Cancelled, D-19: `PointOfSale`/`InvoiceNumber` nullables, `IsExcludedFromCiti`, `PurchaseItem`); `CounterSale` (Draft→Confirmed/Cancelled, E11-C: sin comprobante fiscal, `CounterSaleItem`); permisos `CashRead`/`CashOpen`/`CashClose`/`CashAdjust`/`SuppliersRead`/`SuppliersWrite`/`PurchasesRead`/`PurchasesWrite`/`CounterSalesRead`/`CounterSalesWrite`; `StockMovementReason` ampliado (Purchase, PurchaseCancel, CounterSale, CounterSaleCancel); `DocumentKind` ampliado (Purchase, CounterSale, CashSession).
- Application: handlers CRUD + open/close/session/movements para Cash; CRUD + restore para Suppliers; CRUD + confirm/cancel para Purchases (crea StockMovements al confirmar); CRUD + confirm/cancel para CounterSales (descuenta stock al confirmar); `ICashRepository`, `ISupplierRepository`, `IPurchaseRepository`, `ICounterSaleRepository`; `CreatePaymentHandler` integra `CashMovement` al pagar OTs cuando hay `AccountId`.
- Infrastructure: `EfCashRepository`, `EfSupplierRepository`, `EfPurchaseRepository`, `EfCounterSaleRepository`; 8 configuraciones EF; migración `CashSuppliersPurchases` (8 tablas: `cash_accounts`, `cash_sessions`, `cash_movements`, `suppliers`, `purchases`, `purchase_items`, `counter_sales`, `counter_sale_items`; REVOKE UPDATE ON `cash_movements`; CHECK constraints actualizados).
- API: `CashEndpoints` (9 endpoints), `SuppliersEndpoints` (6), `PurchasesEndpoints` (6), `CounterSalesEndpoints` (6); 4 using directives + 4 Map* en `Program.cs`.
- Tests: 8 factories nuevas + tests de integración T-CAJ-01..04, T-SUP-01..03, T-PUR-01..05, T-CSAL-01..03; `PermissionMatrixWebApplicationFactory` + `PermissionMatrixTests` expandidos con 4 nuevos Foreign IDs (CashAccount, Supplier, Purchase, CounterSale).
- Frontend: `web/src/lib/api/cash.ts`, `suppliers.ts`, `purchases.ts`, `counterSales.ts`; `CashPage` (`/app/cash`), `SuppliersPage` (`/app/suppliers`), `PurchasesPage` (`/app/purchases`), `CounterSalesPage` (`/app/counter-sales`); nav Caja/Proveedores/Compras/Mostrador en `TallerLayout` (Owner y AdminStaff); rutas en `TallerArea`.
- Decisiones aplicadas: D-16 (movimientos append-only, CashSession no append-only), D-19 (campos CITI nullable, `IsExcludedFromCiti`), E11-C (CounterSale sin comprobante fiscal).

**Post-E8 — bugfixes en Develop (14–15/10/2026, commits `27d0d01` / `3395d74` / `b87f7a7` / `ab4dee8` / `a229bcb` / `e46bd2c` / `09a8c7d`):**
- fix(api): `CatalogItemKind`, `WorkOrderStatus` y `QuoteStatus` como `string?` + `Enum.TryParse(ignoreCase: true)` en todos los endpoints de búsqueda — ASP.NET Core 10 minimal APIs son case-sensitive para enum query params.
- fix(api): `DevelopmentSeeder` crea `Branch.CreateMain` para tenants que no la tenían; middleware `ForwardedHeaders` para Cloudflare tunnel; defaults en `appsettings.Development.json`.
- fix(web): `activeOnly ? true : null` en CatalogPage (false enviaba solo inactivos); `CatalogItemKind` en minúsculas (snake_case_lower del API); búsqueda de clientes como dropdown flotante absoluto; header `X-CSRF-TOKEN` (era `X-XSRF-TOKEN`); `allowedHosts: ['tallercontrol.com.ar']` en Vite.
- fix(test): `HealthEndpointsTests` — `UseSetting("Seed:Owner:Email", "")` para desactivar `DevelopmentSeeder` en factory sin Testcontainers completo (sin citext → `NotSupportedException`). Causa: `appsettings.Development.json` ahora tiene defaults `Seed:Owner:*`.
- Patrón documentado: cualquier enum query param en minimal APIs → `string?` + `Enum.TryParse(ignoreCase: true, ...)`.
- docs: `docs/deploy-produccion.md` — guía completa Cloudflare Containers + Neon PostgreSQL + Hyperdrive. Pendiente crítico: `PersistKeysToDbContext<AppDbContext>()` antes del primer deploy.

## Infraestructura de producción

Stack objetivo: **Cloudflare Containers** (API + SPA) · **Cloudflare Hyperdrive** · **Neon PostgreSQL** · **Cloudflare R2** · **Cloudflare Turnstile**.
Guía completa paso a paso: `docs/deploy-produccion.md` (Dockerfile, wrangler.toml, secrets, GitHub Actions CD, checklist).
**Pendiente antes del primer deploy**: ~~persistir claves de Data Protection en BD~~ **resuelto en E9 bloque 1**. Verificar migración `DataProtectionKeys` en el bundle antes del primer deploy.

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

D-02 host, D-03 proveedor PostgreSQL, D-05 librería PDF, D-07 stock negativo (**decidida en E4: se permite con alerta, `IsLowStock` en `CatalogItem`**).
Nuevas en V2: D-17 límites por plan, D-18 Google en el panel, D-20 ventas de mostrador, D-21 kits.

**Decididas:** D-15 (12/10/2026): clientes y catálogo compartidos dentro del taller; caja, stock y numeración por sucursal.
**D-04 (provisoria, 12/10/2026):** el email sale por **Gmail** (SMTP con contraseña de aplicación) detrás de una interfaz `IEmailSender` y siempre desde el outbox, para poder cambiar de proveedor sin tocar el negocio. Revisar antes de E8/E9: el límite de Gmail (≈500 envíos/día) y la entregabilidad del OTP del QR (el plan pide probar 20 envíos reales).
**D-16 (14/10/2026):** modelo de caja = **un libro de movimientos por cuenta** (opción a); la sesión es un corte del libro, no un contenedor de saldo independiente. Implementar en E11 con tablas `cash_accounts` + `cash_movements` (append-only, mismo patrón que `stock_movements`). `payments.account_id` nulo hasta E11; si es nulo, el pago no genera movimiento de caja.
**D-19 (14/10/2026):** datos fiscales de compras = **guardar `point_of_sale`, `invoice_number`, `perceptions`, `retentions` en la tabla `purchases` desde que se crea en E11** (opción a). Campos nullable excepto perceptions/retentions (default 0). Compra sin punto de venta/número queda fuera del CITI con aviso (T-PUR-03).
**D-01 (14/10/2026, E8):** verificación del dueño del vehículo = **código de acceso Base32 de 8 chars** (opción a) como alternativa al OTP email; se almacena solo el hash SHA-256 en `OwnerVerification.OtpHash`; el cliente lo envía como "OTP" por la ruta `access_code`.
**D-12 (14/10/2026, E8):** URL base del QR = **`https://tallercontrol.com.ar/q/{code}`**, configurable vía `QrOptions:BaseUrl`.
**D-13 (14/10/2026, E8):** anti-bot del portal QR = **Cloudflare Turnstile**; test key `1x0000000000000000000000000000000AA` siempre pasa (usado en integración).
Si una tarea depende de una, **preguntar** y registrar el valor provisorio.

## Entorno del usuario

Windows 10, PowerShell, 12 GB de RAM (WSL limitado a 4 GB en `.wslconfig`). Docker Desktop operativo. Git con Git Credential Manager (HTTPS), remoto `Pablo-Oar/TallerControl`; `gh` autenticado.
