"""Package the existing local runtime, without network access or installation."""
import importlib.util
import shutil
import sys
import zipfile
from pathlib import Path

target = Path(sys.argv[1]) / "runtime"
if sys.version_info[:2] != (3, 12):
    raise RuntimeError("Build with a standard Windows CPython 3.12 x64 installation.")
target.mkdir(parents=True, exist_ok=True)
base = Path(sys.prefix)
for name in ("python.exe", "python3.dll", "python312.dll", "vcruntime140.dll", "vcruntime140_1.dll", "LICENSE.txt"):
    shutil.copy2(base / name, target / name)
shutil.copytree(base / "DLLs", target / "DLLs", dirs_exist_ok=True)
library = target / "python312.zip"
if not library.exists():
    with zipfile.ZipFile(library, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in (base / "Lib").rglob("*"):
            relative = path.relative_to(base / "Lib")
            if any(part in ("site-packages", "test", "tests", "__pycache__", "idlelib", "ensurepip", "tkinter", "turtledemo", "venv") for part in relative.parts):
                continue
            if path.is_file() and path.suffix != ".pyc":
                archive.write(path, str(relative))
packages = target / "packages"
packages.mkdir(exist_ok=True)
for name in ("pypdf", "pypdfium2", "pypdfium2_raw", "pypdfium2_cfg", "PIL"):
    spec = importlib.util.find_spec(name)
    if spec is None:
        raise RuntimeError("Missing PDF dependency: " + name)
    package = Path(spec.origin).parent
    shutil.copytree(package, packages / name, dirs_exist_ok=True, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
site_packages = base / "Lib" / "site-packages"
for pattern in ("pypdf-*.dist-info", "pypdfium2-*.dist-info", "pillow-*.dist-info", "pillow.libs"):
    for path in site_packages.glob(pattern):
        shutil.copytree(path, packages / path.name, dirs_exist_ok=True, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
(target / "python312._pth").write_text("python312.zip\nDLLs\npackages\n.\n", encoding="ascii")
print("Portable PDF runtime ready:", target)
