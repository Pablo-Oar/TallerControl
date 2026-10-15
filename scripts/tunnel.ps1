#!/usr/bin/env pwsh
# Arranca el Cloudflare Tunnel hacia localhost:5173 (SPA dev server).
# Usar junto con .\scripts\dev.ps1 en otra terminal.
& "C:\tools\cloudflared\cloudflared.exe" tunnel --config "C:\Users\arauj\.cloudflared\config.yml" run
