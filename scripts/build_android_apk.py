from pathlib import Path
import subprocess
import os
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
TARGET_DIR = SCRIPT_DIR.parent

def run_dotnet_build():
    os.chdir(TARGET_DIR)
    print("📦 Generiere Android APK (Release) für PlatzPilot...")

    cmd = ["dotnet", "publish", "PlatzPilot.csproj", "-f", "net10.0-android", "-c", "Release", "-p:AndroidPackageFormat=apk"]
    result = subprocess.run(cmd)
    if result.returncode != 0:
        sys.exit(result.returncode)

    print("✅ APK erfolgreich generiert!")
    print("📍 Du findest die Datei unter: bin\\Release\\net10.0-android\\publish\\")

if __name__ == "__main__":
    run_dotnet_build()