<#
.SYNOPSIS
  Genera el par de claves VAPID que usa el servidor para enviar avisos push (N-04).

.DESCRIPTION
  Sin parámetros imprime las dos claves para copiarlas donde corresponda (variables de entorno / secretos del hosting).
  Con -WriteEnv las agrega al archivo .env (PUSH_PUBLIC_KEY, PUSH_PRIVATE_KEY, PUSH_SUBJECT), que leen dev.ps1 y
  dev-api.ps1, sin mostrar la clave privada. Si ya existen, no las pisa salvo que uses -Force: cambiar las claves
  invalida todas las suscripciones push existentes.

.EXAMPLE
  .\scripts\generate-vapid-keys.ps1 -WriteEnv
#>
[CmdletBinding()]
param(
    [switch]$WriteEnv,
    [switch]$Force,
    [string]$Subject = 'mailto:soporte@tallercontrol.com.ar'
)

$ErrorActionPreference = 'Stop'

function ConvertTo-Base64Url([byte[]]$bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$envFile = Join-Path $PSScriptRoot '..\.env'

if ($WriteEnv) {
    if (-not (Test-Path $envFile)) { throw 'Falta .env: corré primero .\scripts\dev.ps1.' }
    if (-not $Force -and (Select-String -Path $envFile -Pattern '^PUSH_PRIVATE_KEY=' -Quiet)) {
        Write-Host 'Ya hay claves VAPID en .env. Usá -Force para reemplazarlas (se invalidan las suscripciones existentes).'
        return
    }
}

$ecdsa = [System.Security.Cryptography.ECDsa]::Create([System.Security.Cryptography.ECCurve]::CreateFromFriendlyName('nistP256'))
$p = $ecdsa.ExportParameters($true)
$public = ConvertTo-Base64Url ([byte[]](@(0x04) + $p.Q.X + $p.Q.Y))
$private = ConvertTo-Base64Url $p.D

if ($WriteEnv) {
    $lines = Get-Content $envFile | Where-Object { $_ -notmatch '^PUSH_(PUBLIC_KEY|PRIVATE_KEY|SUBJECT)=' }
    $lines += "PUSH_PUBLIC_KEY=$public"
    $lines += "PUSH_PRIVATE_KEY=$private"
    $lines += "PUSH_SUBJECT=$Subject"
    Set-Content -Path $envFile -Value $lines -Encoding UTF8
    Write-Host 'Claves VAPID guardadas en .env. Reiniciá la API (.\scripts\dev-api.ps1).'
    Write-Host "Clave pública: $public"
}
else {
    Write-Host "Push__PublicKey  = $public"
    Write-Host "Push__PrivateKey = $private"
    Write-Host "Push__Subject    = $Subject"
    Write-Host ''
    Write-Host 'Guardalas como variables de entorno / secretos del hosting. La clave privada no va al repositorio.'
}
