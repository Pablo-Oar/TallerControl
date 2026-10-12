# TallerControl

SaaS de gestión para talleres mecánicos: vehículos y su historial, el ciclo presupuesto → orden → cobro, portal del mecánico y tarjetas QR seguras para el dueño.
Alternativa propia a Vehix, multiempresa y con API JSON reutilizable.

## Stack

- **Backend:** ASP.NET Core 10 Web API (C#), EF Core 10 + Npgsql
- **Frontend:** React + TypeScript (Vite)
- **Base de datos:** PostgreSQL 18
- **Archivos:** Cloudflare R2 (RustFS, compatible con S3, en desarrollo local)

## Entorno local

Requisitos: Docker Desktop (con WSL 2), .NET SDK 10 y Node.js 22.

### Arranque con un comando

```powershell
.\scripts\dev.ps1
```

Crea `.env` si falta, levanta PostgreSQL y el almacenamiento, abre la API en otra ventana (`http://localhost:5233`) y deja la SPA en esta (`http://localhost:5173`, que reenvía `/api` y `/health` a la API).
Comprobación: `http://localhost:5233/health/live` y `/health/ready` deben responder 200.

### Paso a paso

Para correr la API a mano, la cadena de conexión no está en el repositorio: se pasa por variable de entorno
(`$env:ConnectionStrings__Default = "Host=127.0.0.1;Port=5432;Database=tallercontrol;Username=tallercontrol;Password=<la de .env>"`) o con `dotnet user-secrets`.

```powershell
copy .env.example .env      # cambiar las contraseñas locales
docker compose up -d        # PostgreSQL 18 + almacenamiento S3 local
docker compose ps           # db y storage deben figurar como "healthy"
docker compose down         # apagar; los datos se conservan (down -v los borra)
```

| Servicio | Dirección local | Notas |
|---|---|---|
| PostgreSQL 18 | `127.0.0.1:5432` | Usuario y base: `tallercontrol` (ver `.env`) |
| API S3 | `http://127.0.0.1:9000` | Bucket `tallercontrol-dev`, creado al levantar |
| Consola del almacenamiento | `http://127.0.0.1:9001` | Misma clave de acceso que la API |

### Pruebas y calidad

```powershell
dotnet build TallerControl.slnx
dotnet test TallerControl.slnx                          # las de integración usan Testcontainers: requieren Docker
dotnet format TallerControl.slnx --verify-no-changes
npm --prefix web run lint
npm --prefix web test
npm --prefix web run build
```

GitHub Actions (`.github/workflows/ci.yml`) corre lo mismo en cada push y PR a `Develop` y `main`, más un escaneo de secretos con gitleaks.

### Consulta de la base desde Claude Code (MCP, solo lectura)

`.mcp.json` y `dbhub.toml` declaran un servidor MCP de PostgreSQL ([DBHub](https://github.com/bytebase/dbhub) fijado en 1.5.0). Es de **solo lectura** y devuelve como máximo 200 filas; los cambios de esquema se hacen con migraciones.

- Usa el `.env` de la carpeta del proyecto: hay que haberlo creado y tener `docker compose up -d` corriendo.
- La contraseña de `POSTGRES_PASSWORD` no debe tener caracteres especiales (`@ : / ? #`), porque forma parte de la URL de conexión.
- Claude Code pide aprobar el servidor la primera vez que lo ve.

Los puertos solo escuchan en `127.0.0.1`. El almacenamiento local es un sustituto de desarrollo: MinIO dejó de publicar imágenes y se usa RustFS; CORS y URL prefirmadas se validan contra el bucket real de staging.

## Documentación

El plan de implementación (alcance, decisiones, roadmap) es documentación interna y no se versiona en este repositorio.
Las convenciones de trabajo del equipo están en [`CLAUDE.md`](CLAUDE.md).

## Estado

Etapa **E0** — preparación del repositorio y estructura (esqueleto de la API y de la SPA, entorno local y CI).

## Licencia

Software propietario. Todos los derechos reservados. Ver [LICENSE](LICENSE).
