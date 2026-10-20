<#
.SYNOPSIS
  Reinicia solo la API local (sin tocar la SPA ni las migraciones) con la misma configuración que dev.ps1.
.DESCRIPTION
  Uso (desde la carpeta del repositorio):  .\scripts\dev-api.ps1
  Útil después de cambiar código de backend: cortar la API (Ctrl+C) y volver a correr este script.
  Lee .env (como dev.ps1) y define la cadena de conexión del rol de la aplicación y el almacenamiento S3 local (RustFS).
  Requiere que docker compose ya esté levantado (dev.ps1 lo hace la primera vez).
#>
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

if (-not (Test-Path .env)) { throw 'Falta .env: corré primero .\scripts\dev.ps1.' }

$cfg = @{}
foreach ($line in Get-Content .env) {
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$') { $cfg[$Matches[1]] = $Matches[2] }
}

$port = if ($cfg['POSTGRES_PORT']) { $cfg['POSTGRES_PORT'] } else { '5432' }
$db = if ($cfg['POSTGRES_DB']) { $cfg['POSTGRES_DB'] } else { 'tallercontrol' }

$env:ConnectionStrings__Default = "Host=127.0.0.1;Port=$port;Database=$db;Username=tallercontrol_app;Password=$($cfg['POSTGRES_APP_PASSWORD'])"
$env:ASPNETCORE_ENVIRONMENT = 'Development'

$storagePort = if ($cfg['STORAGE_API_PORT']) { $cfg['STORAGE_API_PORT'] } else { '9000' }
$env:Storage__ServiceUrl = "http://127.0.0.1:$storagePort"
$env:Storage__AccessKey = $cfg['STORAGE_ACCESS_KEY']
$env:Storage__SecretKey = $cfg['STORAGE_SECRET_KEY']
$env:Storage__Bucket = if ($cfg['STORAGE_BUCKET']) { $cfg['STORAGE_BUCKET'] } else { 'tallercontrol-dev' }

dotnet run --project src/TallerControl.Api --no-launch-profile --urls http://localhost:5233
