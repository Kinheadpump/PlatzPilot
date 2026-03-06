Write-Host "📦 Generiere Android APK (Release) für PlatzPilot..." -ForegroundColor Cyan

Set-Location -Path "$PSScriptRoot\.."

dotnet publish PlatzPilot.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk

Write-Host "✅ APK erfolgreich generiert!" -ForegroundColor Green
Write-Host "📍 Du findest die Datei unter: bin\Release\net10.0-android\publish\" -ForegroundColor Yellow