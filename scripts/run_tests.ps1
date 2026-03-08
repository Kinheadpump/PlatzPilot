Write-Host "🚀 Starte Unit Tests für PlatzPilot..." -ForegroundColor Cyan

# Springe in den Hauptordner (egal von wo das Skript aufgerufen wird)
Set-Location -Path "$PSScriptRoot\.."

dotnet test PlatzPilot.Tests/PlatzPilot.Tests.csproj -c Release

Write-Host "✅ Testlauf beendet!" -ForegroundColor Green