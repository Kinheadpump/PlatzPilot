from pathlib import Path
import subprocess
import os
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
TARGET_DIR = SCRIPT_DIR.parent

def run_dotnet_test():
    os.chdir(TARGET_DIR)
    print("🚀 Starte Unit Tests für PlatzPilot...")

    cmd = ["dotnet", "test", "PlatzPilot.Tests/PlatzPilot.Tests.csproj", "-c", "Release"]
    result = subprocess.run(cmd)
    if result.returncode != 0:
        sys.exit(result.returncode)

    print("✅ Testlauf beendet!")

if __name__ == "__main__":
    run_dotnet_test()