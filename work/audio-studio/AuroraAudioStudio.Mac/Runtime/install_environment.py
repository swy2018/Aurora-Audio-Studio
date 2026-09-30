"""Create isolated native Mac runtimes. Invoked by Aurora's model manager."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import uuid
import re
from filelock import FileLock

PACKAGES = {
    "mt3": ["mt3-infer==0.2.0", "torch==2.8.0", "torchaudio==2.8.0", "torchvision==0.23.0", "transformers==4.45.2", "numpy<2", "setuptools<81"],
    "piano": ["piano-transcription-inference==0.0.6", "torch==2.8.0", "librosa==0.10.2.post1", "numpy<2", "setuptools<81"],
    "f5": ["f5-tts==1.1.22", "torch==2.10.0", "torchaudio==2.10.0", "torchcodec==0.10.0", "numpy<2", "setuptools<81", "faster-whisper==1.2.1"],
    "qwen": ["qwen-tts==0.1.1", "torch==2.8.0", "torchaudio==2.8.0", "gradio==5.49.1", "numpy<2", "setuptools<81"],
    "roformer": ["bs-roformer-infer==0.1.5", "torch==2.8.0", "numpy<2"],
    "transkun": ["transkun==2.0.1", "torch==2.8.0", "torchaudio==2.8.0", "numpy<2", "setuptools<81"],
    "whisper": ["faster-whisper==1.2.1", "soundfile"],
    "basic": ["basic-pitch[onnx]==0.4.0", "numpy<2", "setuptools<81"],
    "demucs": ["demucs==4.1.0", "torch==2.8.0", "soundfile"],
}

IMPORTS = {"ace":"acestep.acestep_v15_pipeline", "seed":"torch,librosa,gradio", "qwen":"qwen_tts", "roformer":"bs_roformer", "transkun":"transkun.transcribe", "whisper":"faster_whisper", "basic":"basic_pitch", "demucs":"demucs", "mt3":"mt3_infer", "piano":"piano_transcription_inference", "f5":"f5_tts.infer.utils_infer"}


def environment_path(root, family):
    base = root / "envs"
    pointer = base / (family + ".active")
    if pointer.is_symlink(): raise ValueError("Runtime pointer must not be a symlink")
    name = pointer.read_text(encoding="utf-8").strip() if pointer.exists() else family
    if not re.fullmatch(re.escape(family) + r"(?:-[0-9a-f]{32})?", name):
        raise ValueError("Invalid runtime selection")
    env = base / name
    if env.is_symlink() or env.resolve().parent != base.resolve(): raise ValueError("Invalid runtime directory")
    return env


def install(root, family, repair=False):
    uv = shutil.which("uv") or "/opt/homebrew/bin/uv"
    # A venv cannot safely be renamed after creation: scripts contain absolute paths.
    # https://docs.python.org/3.11/library/venv.html
    env = root / "envs" / (family + "-" + uuid.uuid4().hex)
    env.parent.mkdir(parents=True, exist_ok=True)
    with FileLock(str(env.parent / (family + ".lock")), timeout=0):
        environment_path(root, family)  # Reject unsafe existing selectors before changing anything.
        subprocess.run([uv, "venv", "--python", "3.11", str(env)], check=True)
        python = str(env / "bin/python")
        args = [uv, "pip", "install", "--python", python]
        if repair: args.append("--reinstall")
        if family == "ace":
            args += [str(root / "sources/ace-step"), "torch==2.10.0", "torchaudio==2.10.0", "torchvision==0.25.0"]
        elif family == "seed":
            requirements = (root / "sources/seed-vc/requirements-mac.txt").read_text().splitlines()
            # The upstream Mac file still includes CUDA indexes and nightly torch.
            args += [r for r in requirements if r and not r.startswith(("--", "torch", "resemblyzer"))]
            args += ["torch==2.5.1", "torchaudio==2.5.1", "torchvision==0.20.1", "setuptools<81", "gradio_client"]
        else:
            args += PACKAGES[family]
        if family == "seed":
            subprocess.run([uv, "pip", "uninstall", "--python", python, "resemblyzer", "typing"], check=True)
        subprocess.run(args, check=True)
        subprocess.run([uv, "pip", "check", "--python", python], check=True)
        subprocess.run([python, "-c", "import " + IMPORTS[family]], check=True, timeout=180)
        frozen = subprocess.check_output([uv, "pip", "freeze", "--python", python], text=True)
        (env / "aurora-packages.txt").write_text(frozen)
        (env / "aurora-runtime.json").write_text(json.dumps(dict(family=family, at=time.time(), python=python)))
        pointer = env.parent / (family + ".active")
        pending = env.parent / (family + ".active-" + uuid.uuid4().hex)
        pending.write_text(env.name, encoding="utf-8")
        pending.replace(pointer)

if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--root", type=Path, required=True)
    p.add_argument("--family", choices=[*PACKAGES, "ace", "seed"], required=True)
    a = p.parse_args()
    install(a.root, a.family)
