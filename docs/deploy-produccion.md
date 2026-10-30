# Guía de deploy en producción — TallerControl

Stack de producción: **Cloudflare Containers** (API + SPA) · **Cloudflare Hyperdrive** · **Neon PostgreSQL** · **Cloudflare R2** · **Cloudflare Turnstile**.

> Esta guía se hace una sola vez, antes del primer deploy a `main`. Para deploys posteriores, el workflow de GitHub Actions lo hace automáticamente.

---

## Índice

1. [Prerrequisitos](#1-prerrequisitos)
2. [Neon — base de datos PostgreSQL](#2-neon--base-de-datos-postgresql)
3. [Cloudflare R2 — CORS para producción](#3-cloudflare-r2--cors-para-producción)
4. [Cloudflare Turnstile — widget de producción](#4-cloudflare-turnstile--widget-de-producción)
5. [Cloudflare Hyperdrive — pool de conexiones](#5-cloudflare-hyperdrive--pool-de-conexiones)
6. [Dockerfile multi-stage](#6-dockerfile-multi-stage)
7. [wrangler.toml — configuración de Containers](#7-wranglertoml--configuración-de-containers)
8. [Secrets en Cloudflare Workers](#8-secrets-en-cloudflare-workers)
9. [Primer deploy manual (validación)](#9-primer-deploy-manual-validación)
10. [CI/CD con GitHub Actions](#10-cicd-con-github-actions)
11. [Checklist post-deploy](#11-checklist-post-deploy)
12. [Notas de arquitectura](#12-notas-de-arquitectura)

---

## 1. Prerrequisitos

| Qué | Dónde | Notas |
|-----|-------|-------|
| Cuenta Cloudflare con **Workers Paid** ($5/mes) | dash.cloudflare.com | Containers no está disponible en el plan free |
| Dominio `tallercontrol.com.ar` apuntando a Cloudflare | Ya hecho | DNS gestionado por Cloudflare |
| Cuenta Neon | neon.com | Free tier suficiente para empezar |
| Node.js 22 + npm | Local | Para Wrangler CLI |
| Docker Desktop | Local | Para builds locales |
| Wrangler v4+ | `npm install -g wrangler` | CLI de Cloudflare |

Verificar que Wrangler está autenticado:
```bash
wrangler login
wrangler whoami
```

---

## 2. Neon — base de datos PostgreSQL

### 2.1 Crear el proyecto

1. Ir a [neon.com](https://neon.com) → **New Project**
2. Nombre: `tallercontrol-prod`
3. Región: `aws-sa-east-1` (São Paulo — la más cercana a Argentina)
4. PostgreSQL version: **17** (Neon aún no tiene v18; compatible con nuestro esquema)

### 2.2 Crear roles y base de datos

En el **SQL Editor** de Neon, ejecutar:

```sql
-- Extensiones requeridas por TallerControl
CREATE EXTENSION IF NOT EXISTS citext;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Rol de la aplicación (sin DELETE/UPDATE en tablas append-only)
CREATE ROLE tallercontrol_app WITH LOGIN PASSWORD 'CAMBIAR_PASSWORD_APP';
GRANT CONNECT ON DATABASE neondb TO tallercontrol_app;
GRANT USAGE ON SCHEMA public TO tallercontrol_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO tallercontrol_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO tallercontrol_app;
```

> El script `scripts/db/create-app-role.sql` del repo también aplica. Adaptarlo si es necesario para Neon.

### 2.3 Obtener connection strings

En Neon dashboard → **Connect** → elegir:
- **Direct connection** (para migraciones): `postgresql://user:pass@ep-xxx.sa-east-1.aws.neon.tech/neondb?sslmode=require`
- **Pooled connection** (para la app): igual pero con `-pooler` en el hostname: `postgresql://user:pass@ep-xxx-pooler.sa-east-1.aws.neon.tech/neondb?sslmode=require`

Convertir a formato Npgsql (key-value):
```
# Migraciones (dueño, conexión directa)
Host=ep-xxx.sa-east-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=PASS;SSL Mode=Require

# App (rol restringido, conexión pooled)
Host=ep-xxx-pooler.sa-east-1.aws.neon.tech;Port=5432;Database=neondb;Username=tallercontrol_app;Password=PASS_APP;SSL Mode=Require
```

### 2.4 Aplicar migraciones

Descargar el artifact `efbundle-linux-x64` de la última CI (o generarlo localmente) y ejecutar:

```bash
./artifacts/efbundle \
  --connection "Host=ep-xxx.sa-east-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=PASS;SSL Mode=Require"
```

Verificar que las tablas existen:
```sql
SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename;
-- Debe listar: audit_events, branches, catalog_items, customers, ...
```

Revocar permisos en tablas append-only (el bundle ya lo hace, verificar):
```sql
SELECT has_table_privilege('tallercontrol_app', 'audit_events', 'UPDATE'); -- debe ser f
SELECT has_table_privilege('tallercontrol_app', 'stock_movements', 'UPDATE'); -- debe ser f
```

---

## 3. Cloudflare R2 — CORS para producción

El bucket R2 ya existe (configurado en dev). Solo falta agregar las reglas CORS para que el browser pueda subir archivos directamente con URLs prefirmadas.

En el dashboard de R2 → bucket `tallercontrol` → **Settings** → **CORS Policy**:

```json
[
  {
    "AllowedOrigins": ["https://tallercontrol.com.ar"],
    "AllowedMethods": ["GET", "PUT", "HEAD"],
    "AllowedHeaders": ["Content-Type", "Content-Length", "x-amz-meta-*"],
    "ExposeHeaders": ["ETag"],
    "MaxAgeSeconds": 3600
  }
]
```

> **Importante**: Nunca usar `"*"` en `AllowedOrigins` en producción. Las URLs prefirmadas ya incluyen autenticación pero el CORS debe ser restrictivo.

---

## 4. Cloudflare Turnstile — widget de producción

Hoy la app usa la test key `1x0000000000000000000000000000000AA` (siempre pasa). Para producción:

1. Ir a [dash.cloudflare.com](https://dash.cloudflare.com) → **Turnstile** → **Add site**
2. Nombre: `tallercontrol-prod`
3. Hostname: `tallercontrol.com.ar`
4. Widget type: **Managed** (recomendado)
5. Copiar **Site Key** (pública, va al frontend) y **Secret Key** (privada, va al backend)

Configurar en el frontend (`web/.env.production`):
```
VITE_TURNSTILE_SITE_KEY=0x4AAAAAAA...  (la site key real)
```

El backend ya recibe el token y lo valida en `TurnstileVerifier`. El secret key se agrega como variable de entorno del Container (ver §8).

---

## 5. Cloudflare Hyperdrive — pool de conexiones

Hyperdrive actúa como proxy entre los Containers y Neon, reutilizando conexiones y cacheando queries SELECT. No tiene costo adicional.

```bash
# Crear la configuración de Hyperdrive apuntando a Neon (pooled)
wrangler hyperdrive create tallercontrol-prod \
  --connection-string "postgresql://tallercontrol_app:PASS_APP@ep-xxx-pooler.sa-east-1.aws.neon.tech/neondb?sslmode=require"

# El comando devuelve un ID como: "12345abcde..."
# Guardarlo para usarlo en wrangler.toml
```

Verificar:
```bash
wrangler hyperdrive list
```

> **Nota**: Hyperdrive cachea queries SELECT por defecto (max-age 60s). Para TallerControl esto es seguro porque los datos críticos (saldos, estados) se leen después de escribir y usan el flujo normal. Si querés deshabilitar el caché: agregar `--caching-disabled`.

---

## 6. Dockerfile multi-stage

Crear `Dockerfile` en la raíz del repositorio:

```dockerfile
# ── Etapa 1: Build del frontend ──────────────────────────────────────────────
FROM node:22-alpine AS frontend-build
WORKDIR /web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
RUN npm run build
# Resultado en /web/dist

# ── Etapa 2: Build del backend ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build
WORKDIR /src
COPY TallerControl.slnx ./
COPY src/ ./src/
RUN dotnet restore TallerControl.slnx
RUN dotnet publish src/TallerControl.Api/TallerControl.Api.csproj \
    -c Release -r linux-x64 --self-contained false \
    -o /app/publish

# ── Etapa 3: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Copiar API publicada
COPY --from=backend-build /app/publish ./

# Copiar SPA compilada al directorio de archivos estáticos de la API
COPY --from=frontend-build /web/dist ./wwwroot

# Cloudflare Containers espera el puerto 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Usuario no-root (seguridad)
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

EXPOSE 8080
ENTRYPOINT ["dotnet", "TallerControl.Api.dll"]
```

> **SPA estática**: verificar que `Program.cs` tiene `app.UseStaticFiles()` y `app.MapFallbackToFile("index.html")` para el routing de React. Ya debería estar configurado.

Build local para probar:
```bash
docker build -t tallercontrol:local .
docker run -p 8080:8080 \
  -e ConnectionStrings__Default="..." \
  -e ASPNETCORE_ENVIRONMENT=Development \
  tallercontrol:local
```

---

## 7. wrangler.toml — configuración de Containers

Crear `wrangler.toml` en la raíz:

```toml
name = "tallercontrol"
main = "worker.js"           # Worker entrypoint que rutea al Container
compatibility_date = "2026-01-01"

[[containers]]
class_name = "TallerControlApp"
image = "./Dockerfile"

  [containers.instance_type]
  memory = "2GB"
  vcpus  = 1

  [containers.autoscaling]
  max_instances = 3
  min_instances = 0          # scale-to-zero (ahorra costos)

[[durable_objects.bindings]]
name        = "APP"
class_name  = "TallerControlApp"

[[migrations]]
tag              = "v1"
new_sqlite_classes = ["TallerControlApp"]

[[hyperdrive]]
binding = "HYPERDRIVE"
id      = "12345abcde..."    # ID del paso §5

[vars]
ASPNETCORE_ENVIRONMENT = "Production"
```

Crear `worker.js` (el Worker que enruta peticiones al Container):

```js
export { TallerControlApp } from './container-class.js';

export default {
  async fetch(request, env) {
    const id = env.APP.idFromName('tallercontrol');
    const container = env.APP.get(id);
    return container.fetch(request);
  }
};
```

> **Simplificación**: Cloudflare Containers usa un Durable Object como proxy. El Worker recibe la request y la reenvía al Container. El Hyperdrive se usa desde el Container via la variable de entorno `env.HYPERDRIVE.connectionString`.

**Pasar la connection string de Hyperdrive al Container en `Program.cs`**:

```csharp
// En Program.cs, si HYPERDRIVE_CONNECTION_STRING está seteado (Cloudflare Containers)
// usarlo como override del connection string de Npgsql.
var hyperdriveConn = builder.Configuration["HYPERDRIVE_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(hyperdriveConn))
{
    builder.Configuration["ConnectionStrings:Default"] = hyperdriveConn;
}
```

---

## 8. Secrets en Cloudflare Workers

Los valores sensibles NUNCA van en `wrangler.toml`. Se suben como secrets:

```bash
# Cadena de conexión a Neon (pooled, vía Hyperdrive o directo)
wrangler secret put ConnectionStrings__Default
# → pegar: Host=ep-xxx-pooler...;Username=tallercontrol_app;Password=...;SSL Mode=Require

# Clave de Data Protection (para cookies tc_qr — ver nota §12)
wrangler secret put DataProtection__Key

# Cloudflare Turnstile secret key
wrangler secret put Turnstile__SecretKey

# Email SMTP (Gmail con contraseña de aplicación)
wrangler secret put Email__Smtp__Password

# R2 / Storage
wrangler secret put Storage__AccessKey
wrangler secret put Storage__SecretKey
# (Solo si las URLs prefirmadas deben firmarse con otro host que el del API: dominio público del bucket)
# Storage__PublicUrl=https://<dominio-publico-del-bucket>   (variable, no secreto; en local con túnel: STORAGE_PUBLIC_URL en .env)

# Avisos push al celular del mecánico (claves VAPID; generar con .\scripts\generate-vapid-keys.ps1)
wrangler secret put Push__PublicKey
wrangler secret put Push__PrivateKey
# Push__Subject=mailto:soporte@tallercontrol.com.ar   (variable)

# (Opcional) Connection string de migraciones — solo para aplicar manualmente
wrangler secret put ConnectionStrings__Migrations
```

Listar secrets configurados (sin mostrar valores):
```bash
wrangler secret list
```

---

## 9. Primer deploy manual (validación)

```bash
# 1. Verificar que Docker Desktop está corriendo
# 2. Build y deploy
wrangler deploy

# 3. Verificar que el Container arrancó
wrangler tail

# 4. Probar health checks
curl https://tallercontrol.com.ar/health/live
curl https://tallercontrol.com.ar/health/ready
```

Si `/health/ready` devuelve 503: revisar connection string de Neon y que las migraciones se aplicaron.

---

## 10. CI/CD con GitHub Actions

Agregar un job `deploy` en `.github/workflows/ci.yml` que corra solo en push a `main`:

```yaml
  deploy:
    name: Deploy (Cloudflare Containers)
    needs: [backend, web]          # solo si CI pasa
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-node@v4
        with:
          node-version: 22

      - name: Install Wrangler
        run: npm install -g wrangler

      # El job 'backend' ya construyó el efbundle; aquí aplicamos migraciones a Neon
      - name: Download migrations bundle
        uses: actions/download-artifact@v4
        with:
          name: efbundle-linux-x64
          path: artifacts/

      - name: Apply migrations to Neon
        env:
          NEON_MIGRATIONS_CONN: ${{ secrets.NEON_MIGRATIONS_CONNECTION_STRING }}
        run: |
          chmod +x artifacts/efbundle
          ./artifacts/efbundle --connection "$NEON_MIGRATIONS_CONN"

      - name: Deploy to Cloudflare Containers
        env:
          CLOUDFLARE_API_TOKEN: ${{ secrets.CLOUDFLARE_API_TOKEN }}
          CLOUDFLARE_ACCOUNT_ID: ${{ secrets.CLOUDFLARE_ACCOUNT_ID }}
        run: wrangler deploy
```

**Secrets a configurar en GitHub** (Settings → Secrets → Actions):

| Secret | Valor |
|--------|-------|
| `CLOUDFLARE_API_TOKEN` | Token con permisos `Workers Scripts:Edit` + `Cloudflare Containers:Edit` |
| `CLOUDFLARE_ACCOUNT_ID` | ID de la cuenta Cloudflare (visible en el dashboard) |
| `NEON_MIGRATIONS_CONNECTION_STRING` | Connection string directo del dueño (sin pooler) |

Generar el API token en Cloudflare: **Profile → API Tokens → Create Token → Custom Token**.
Permisos mínimos: `Account > Workers Scripts > Edit`, `Account > Cloudflare Containers > Edit`, `Zone > DNS > Read`.

---

## 11. Checklist post-deploy

- [ ] `GET /health/live` devuelve 200
- [ ] `GET /health/ready` devuelve 200
- [ ] Login con owner funciona en `https://tallercontrol.com.ar`
- [ ] Subir logo en Settings (prueba R2 + presigned URL)
- [ ] Generar un QR, escanear y verificar portal del dueño (prueba Turnstile + cookie `tc_qr`)
- [ ] Crear un presupuesto y enviarlo por email (prueba Gmail SMTP)
- [ ] Crear un pago y abrir el PDF (prueba QuestPDF en producción)
- [ ] Entrar a `/m` con un mecánico en un celular (HTTPS), tocar «Activar avisos», asignarle una orden desde el panel y confirmar que llega el aviso (con la sesión iniciada; **cerrar sesión borra la suscripción**, D-38)
- [ ] La tarea horaria `MaintenanceWorker` está corriendo (presupuestos vencidos y recordatorios; revisar el log una hora después del arranque)
- [ ] Verificar SSL en [ssllabs.com/ssltest](https://www.ssllabs.com/ssltest/) — debe ser A+

---

## 12. Notas de arquitectura

### Data Protection API (cookies `tc_qr`)

El QR portal usa `Microsoft.AspNetCore.DataProtection` para firmar la cookie `tc_qr`. En Containers, si el Container se reinicia o escala horizontalmente las cookies firmadas con una instancia no las puede verificar otra.

**Fix recomendado antes del primer deploy**:

```csharp
// En Program.cs, agregar persistencia de claves en la base de datos
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("TallerControl");
```

Requiere instalar `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` y agregar la migración correspondiente.

### Scale-to-zero

Con `min_instances = 0`, el Container se apaga cuando no hay tráfico y arranca en el primer request (~500ms de cold start). Para un MVP está bien. Si el tiempo de arranque es inaceptable, cambiar a `min_instances = 1`.

### Logs

```bash
wrangler tail                    # logs en tiempo real
wrangler tail --format pretty    # formato legible
```

### Rollback

```bash
# Listar deployments
wrangler deployments list

# Volver a un deployment anterior
wrangler rollback <deployment-id>
```

### Email en producción

Gmail SMTP está limitado a ~500 envíos/día. Si el volumen crece, migrar a:
- **Resend** (100 emails/día gratis, API sencilla)
- **Brevo** (300 emails/día gratis)

Solo requiere cambiar `SmtpEmailSender` por la implementación del nuevo proveedor detrás de la interfaz `IEmailSender` — el resto del código no cambia.

---

## Referencias

- [Cloudflare Containers docs](https://developers.cloudflare.com/containers/)
- [Cloudflare Hyperdrive docs](https://developers.cloudflare.com/hyperdrive/)
- [Neon EF Core guide](https://neon.tech/docs/guides/dotnet-entity-framework)
- [Neon extensions (citext, pg_trgm)](https://neon.com/docs/extensions/pg-extensions)
- [Cloudflare Turnstile get started](https://developers.cloudflare.com/turnstile/get-started/)
- [cloudflare/wrangler-action (GitHub Actions)](https://github.com/cloudflare/wrangler-action)
- [.NET Cloudflare Containers template](https://github.com/kourain/.net-cloudflare-containers)
