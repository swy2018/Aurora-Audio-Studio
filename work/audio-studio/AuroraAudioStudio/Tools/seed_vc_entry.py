"""Aurora's tracked entry point; upstream code stays unmodified."""
from pathlib import Path
import runpy
import sys

root = Path(__file__).resolve().parent
sys.path.insert(0, str(root))
runpy.run_path(str(root / "app_svc.py"), run_name="__main__")
