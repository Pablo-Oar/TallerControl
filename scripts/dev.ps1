<#
.SYNOPSIS
  Levanta el entorno de desarrollo completo: PostgreSQL + almacenamiento S3 local, la API y la SPA.
.DESCRIPTION
  Uso (desde cualquier carpeta):  .\scripts\dev.ps1
  - Crea .env desde .env.example si no existe (cambiá las contraseñas si querés).
  - Levanta docker compose y espera a que esté "healthy".
  - Arma ConnectionStrings__Default con los valores de .env y lo pasa a la API.
  - Abre la API en otra ventana y deja la SPA (Vite) en esta.
#>
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

if (-not (Test-Path .env)) {
    Copy-Item .env.example .env
    Write-Host 'Creé .env desde .env.example. Revisá las contraseñas locales si querés cambiarlas.'
}

# Lee .env (KEY=VALUE, ignora comentarios y líneas vacías).
$cfg = @{}
foreach ($line in Get-Content .env) {
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$') { $cfg[$Matches[1]] = $Matches[2] }
}

docker compose up -d --wait
if ($LASTEXITCODE -ne 0) { throw 'docker compose no pudo levantar el entorno.' }

$port = if ($cfg['POSTGRES_PORT']) { $cfg['POSTGRES_PORT'] } else { '5432' }
$user = if ($cfg['POSTGRES_USER']) { $cfg['POSTGRES_USER'] } else { 'tallercontrol' }
$db   = if ($cfg['POSTGRES_DB'])   { $cfg['POSTGRES_DB'] }   else { 'tallercontrol' }
$env:ConnectionStrings__Default = "Host=127.0.0.1;Port=$port;Database=$db;Username=$user;Password=$($cfg['POSTGRES_PASSWORD'])"
$env:ASPNETCORE_ENVIRONMENT = 'Development'

if (-not (Test-Path web/node_modules)) { npm --prefix web ci }

# La API en otra ventana (hereda las variables de entorno de esta sesión).
Start-Process powershell -ArgumentList '-NoExit', '-Command', 'dotnet run --project src/TallerControl.Api --no-launch-profile --urls http://localhost:5233'

Write-Host 'API:  http://localhost:5233/health/ready'
Write-Host 'SPA:  http://localhost:5173'
npm --prefix web run dev