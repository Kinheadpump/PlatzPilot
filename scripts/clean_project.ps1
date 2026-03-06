Write-Host "🧹 Räume Projekt auf (lösche alle bin und obj Ordner)..." -ForegroundColor Cyan

Set-Location -Path "$PSScriptRoot\.."

dotnet clean

# Sucht und löscht radikal alle bin und obj Ordner (Windows-Weg)
Get-ChildItem -Path . -Include bin,obj -Recurse -Directory | Remove-Item -Force -Recurse

Write-Host "✨ Alles blitzblank! Du kannst jetzt einen sauberen Rebuild starten." -ForegroundColor Green