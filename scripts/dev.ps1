<#
.SYNOPSIS
  Levanta el entorno de desarrollo completo: PostgreSQL + almacenamiento S3 local, migraciones, la API y la SPA.
.DESCRIPTION
  Uso (desde la carpeta del repositorio):  .\scripts\dev.ps1
  Desde otra carpeta, con la ruta completa:  & "<ruta del repo>\scripts\dev.ps1"
  Si PowerShell bloquea la ejecución:  powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1

  - Crea .env desde .env.example si no existe (cambiá las contraseñas si querés).
  - Si .env no tiene POSTGRES_APP_PASSWORD, genera una clave y la agrega.
  - Levanta docker compose y espera a que esté "healthy".
  - Crea el rol tallercontrol_app (sin DELETE ni UPDATE sobre la auditoría) y le pone la clave de .env.
  - Aplica las migraciones con el dueño de la base (ConnectionStrings__Migrations).
  - Abre la API en otra ventana con el rol de aplicación (ConnectionStrings__Default) y deja la SPA aquí.
#>
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

if (-not (Test-Path .env)) {
    Copy-Item .env.example .env
    Write-Host 'Creé .env desde .env.example. Revisá las contraseñas locales si querés cambiarlas.'
}

# Lee .env (KEY=VALUE, ignora comentarios y líneas vacías).
function Read-DotEnv {
    $values = @{}
    foreach ($line in Get-Content .env) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$') { $values[$Matches[1]] = $Matches[2] }
    }
    return $values
}
$cfg = Read-DotEnv

# Un .env anterior puede no tener la clave del rol de la aplicación, o conservar la de la plantilla.
$placeholder = 'cambiar-esta-clave-de-la-app'
if (-not $cfg['POSTGRES_APP_PASSWORD'] -or $cfg['POSTGRES_APP_PASSWORD'] -eq $placeholder) {
    $chars = [char[]]('abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789'.ToCharArray())
    $bytes = New-Object byte[] 24
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $generated = -join ($bytes | ForEach-Object { $chars[$_ % $chars.Length] })

    $content = Get-Content .env -Raw
    if ($content -match '(?m)^\s*POSTGRES_APP_PASSWORD\s*=') {
        $content = [regex]::Replace($content, '(?m)^\s*POSTGRES_APP_PASSWORD\s*=.*$', "POSTGRES_APP_PASSWORD=$generated")
    } else {
        $content = $content.TrimEnd() + "`r`n`r`n# Clave del rol tallercontrol_app (generada por scripts/dev.ps1)`r`nPOSTGRES_APP_PASSWORD=$generated`r`n"
    }
    [IO.File]::WriteAllText((Join-Path (Get-Location) '.env'), $content, (New-Object System.Text.UTF8Encoding $false))
    Write-Host 'Generé POSTGRES_APP_PASSWORD en .env.'
    $cfg = Read-DotEnv
}

# db y storage son servicios de larga duración: se espera a que estén "healthy".
docker compose up -d --wait db storage
if ($LASTEXITCODE -ne 0) { throw 'docker compose no pudo levantar la base y el almacenamiento.' }

# storage-init es una tarea de una sola vez (crea el bucket y termina con 0): con --wait
# Compose la tomaría por un servicio caído, así que se corre aparte y se espera a que termine.
docker compose up --no-log-prefix storage-init
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el bucket de desarrollo.' }

$port = if ($cfg['POSTGRES_PORT']) { $cfg['POSTGRES_PORT'] } else { '5432' }
$user = if ($cfg['POSTGRES_USER']) { $cfg['POSTGRES_USER'] } else { 'tallercontrol' }
$db   = if ($cfg['POSTGRES_DB'])   { $cfg['POSTGRES_DB'] }   else { 'tallercontrol' }

# Rol de la aplicación (idempotente). El SQL es ASCII para viajar sin problemas por stdin.
$OutputEncoding = New-Object System.Text.UTF8Encoding $false
Get-Content -Raw scripts/db/create-app-role.sql |
    docker compose exec -T db psql -q -U $user -d $db -v ON_ERROR_STOP=1 -v "app_password=$($cfg['POSTGRES_APP_PASSWORD'])" -f -
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el rol de la aplicación.' }

# Migraciones con el dueño de la base.
$env:ConnectionStrings__Migrations = "Host=127.0.0.1;Port=$port;Database=$db;Username=$user;Password=$($cfg['POSTGRES_PASSWORD'])"
dotnet tool restore | Out-Null
dotnet ef database update --project src/TallerControl.Infrastructure --startup-project src/TallerControl.Api --context AppDbContext
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron aplicar las migraciones.' }

# La API corre con el rol de aplicación, no con el dueño.
$env:ConnectionStrings__Default = "Host=127.0.0.1;Port=$port;Database=$db;Username=tallercontrol_app;Password=$($cfg['POSTGRES_APP_PASSWORD'])"
$env:ASPNETCORE_ENVIRONMENT = 'Development'

# Almacenamiento S3 local (RustFS), sustituto de Cloudflare R2 en desarrollo.
$storagePort = if ($cfg['STORAGE_API_PORT']) { $cfg['STORAGE_API_PORT'] } else { '9000' }
$env:Storage__ServiceUrl = "http://127.0.0.1:$storagePort"
$env:Storage__AccessKey = $cfg['STORAGE_ACCESS_KEY']
$env:Storage__SecretKey = $cfg['STORAGE_SECRET_KEY']
$env:Storage__Bucket = if ($cfg['STORAGE_BUCKET']) { $cfg['STORAGE_BUCKET'] } else { 'tallercontrol-dev' }
# Opcional: para probar fotos desde un celular por el túnel (ver docs/deploy-produccion.md / vite.config.ts), STORAGE_PUBLIC_URL=https://tallercontrol.com.ar en .env
if ($cfg['STORAGE_PUBLIC_URL']) { $env:Storage__PublicUrl = $cfg['STORAGE_PUBLIC_URL'] }
# Avisos push al celular del mecánico (N-04). Generar con .\scripts\generate-vapid-keys.ps1 -WriteEnv
if ($cfg['PUSH_PUBLIC_KEY'] -and $cfg['PUSH_PRIVATE_KEY']) {
    $env:Push__PublicKey = $cfg['PUSH_PUBLIC_KEY']
    $env:Push__PrivateKey = $cfg['PUSH_PRIVATE_KEY']
    if ($cfg['PUSH_SUBJECT']) { $env:Push__Subject = $cfg['PUSH_SUBJECT'] }
}

if (-not (Test-Path web/node_modules)) { npm --prefix web ci }

# La API en otra ventana (hereda las variables de entorno de esta sesión).
Start-Process powershell -ArgumentList '-NoExit', '-Command', 'dotnet run --project src/TallerControl.Api --no-launch-profile --urls http://localhost:5233'

Write-Host 'API:  http://localhost:5233/health/ready'
Write-Host 'SPA:  http://localhost:5173'
npm --prefix web run dev