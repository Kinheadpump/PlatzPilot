from pathlib import Path
import shutil
import subprocess
import os
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
TARGET_DIR = SCRIPT_DIR.parent

def run_dotnet_clean():
    os.chdir(TARGET_DIR)
    print("🧹 Räume Projekt auf (lösche alle bin und obj Ordner)...")

    cmd = ["dotnet", "clean"]
    result = subprocess.run(cmd)
    if result.returncode != 0:
        sys.exit(result.returncode)
    
    deleted = 0

    for path in TARGET_DIR.rglob("*"):
        if path.is_dir() and path.name in {"bin", "obj"}:
            print(f"🧹 Entferne {path}")
            shutil.rmtree(path, ignore_errors=True)
            deleted += 1

    print(f"✅ {deleted} Ordner gelöscht")
    print("✨ Alles fertig! Du kannst jetzt einen sauberen Rebuild starten.")

if __name__ == "__main__":
    run_dotnet_clean()