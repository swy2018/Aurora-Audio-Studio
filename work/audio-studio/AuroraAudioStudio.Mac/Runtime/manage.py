"""Model lifecycle commands. All mutations are scoped to catalog-owned paths."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
from filelock import FileLock
from download_models import CATALOG, digest, download, emit, validate_required
from install_environment import install

SOURCES = {"ace": ("ace-step", "https://github.com/ace-step/ACE-Step-1.5.git"), "seed": ("seed-vc", "https://github.com/Plachtaa/seed-vc.git")}
IMPORTS = {"ace":"acestep.acestep_v15_pipeline", "seed":"torch,librosa,gradio", "qwen":"qwen_tts", "roformer":"bs_roformer", "transkun":"transkun.transcribe", "whisper":"faster_whisper", "basic":"basic_pitch", "demucs":"demucs"}
IMPORTS.update(mt3="mt3_infer", piano="piano_transcription_inference", f5="f5_tts.infer.utils_infer")


def trash_model(base):
    helper = Path(__file__).with_name("trash-item")
    if not helper.is_file(): raise RuntimeError("缺少 Mac 回收站组件，请重新安装 Aurora。")
    subprocess.run([str(helper), str(base)], check=True)


def environment(root, family, repair=False):
    if family in SOURCES:
        name, url = SOURCES[family]
        source = root / "sources" / name
        source.parent.mkdir(parents=True, exist_ok=True)
        if not (source / ".git").exists():
            subprocess.run(["git", "clone", "--depth", "1", url, str(source)], check=True)
    marker = root / "envs" / family / "aurora-runtime.json"
    if repair or not marker.exists():
        emit("progress", stage="安装 Mac 运行环境", model=family)
        install(root, family, repair=repair)


def _check(root, model, deep=True):
    family = CATALOG[model]["family"]
    current = root / "models" / model / "current"
    receipt = json.loads((current / "receipt.json").read_text())
    if not receipt.get("files"):
        raise RuntimeError("模型安装记录没有权重文件，请修复模型。")
    for f in receipt["files"]:
        relative = Path(f["path"])
        if relative.is_absolute() or ".." in relative.parts:
            raise ValueError("Invalid receipt path")
        p = current / relative
        if not p.is_file() or p.stat().st_size != f["size"] or (deep and digest(p) != f["sha256"]):
            raise RuntimeError("文件缺失或损坏: " + f["path"])
        emit("progress", model=model, stage="校验 " + f["path"])
    validate_required(current, CATALOG[model])
    if family == "subtitle-edit":
        from subtitle_edit import verify, APP
        verify(current / APP)
        (current / "health.json").write_text(json.dumps(dict(at=time.time(), files=True, imports=False, inference=False)))
        emit("completed", model=model, message="Subtitle Edit 文件、签名与 Apple 公证检查通过。")
        return
    if CATALOG[model].get("download_only"):
        (current / "health.json").write_text(json.dumps(dict(at=time.time(), files=True, imports=False, inference=False)))
        emit("completed", model=model, message="模型文件校验通过；与 Windows 原版相同，此组件仅提供模型管理。")
        return
    python = root / "envs" / family / "bin/python"
    subprocess.run([str(python), "-c", "import " + IMPORTS[family]], check=True, timeout=180)
    if family == "transkun":
        subprocess.run([str(python), "-c", "from importlib.resources import files; p=files('transkun')/'pretrained/2.0.pt'; assert p.is_file() and p.stat().st_size>50000000"],check=True)
    (current / "health.json").write_text(json.dumps(dict(at=time.time(), files=True, imports=True, inference=False)))
    emit("completed", model=model, message="文件校验与运行环境检查通过；生成质量以实际任务为准。")


def check(root, model, deep=True):
    try:
        return _check(root, model, deep)
    except Exception as error:
        current = root / "models" / model / "current"
        if model in CATALOG and current.is_dir() and current.resolve().is_relative_to(root.resolve()):
            (current / "health.json").write_text(json.dumps(dict(at=time.time(), failed=True, message=str(error)), ensure_ascii=False))
        raise


def action(root, model, command):
    if model not in CATALOG:
        raise ValueError("此模型尚无 Mac 安装适配器。")
    root = root.expanduser().resolve()
    base = root / "models" / model
    base.mkdir(parents=True, exist_ok=True)
    if not base.resolve().is_relative_to(root) or base.is_symlink():
        raise ValueError("模型目录不能指向模型根目录之外。")
    for name in ["current", "previous", "staging"]:
        if (base / name).is_symlink():
            raise ValueError("模型版本目录不能是符号链接。")
    family = CATALOG[model]["family"]
    if family == "subtitle-edit" and command not in ("check", "updates"):
        from subtitle_edit import require_closed
        require_closed(base)
    if command in ("install", "repair", "update"):
        if not CATALOG[model].get("download_only"):
            environment(root, family, repair=command=="repair")
        try:
            if family == "subtitle-edit":
                from subtitle_edit import install as install_editor
                with FileLock(str(base / ".lock"), timeout=0):
                    install_editor(base, update=command=="update")
            else:
                download(root, model, update=command=="update")
            check(root, model)
            if command == "update":
                (base / "current/update-status.json").write_text(json.dumps(dict(at=time.time(), available=False, message="模型更新完成。", updates=[])))
        except Exception as error:
            if command == "update" and (base / "current").exists():
                (base / "current/update-status.json").write_text(json.dumps(dict(at=time.time(), available=True, message="更新未完成，可重试：" + str(error)), ensure_ascii=False))
            raise
    elif command == "check":
        with FileLock(str(base / ".lock"), timeout=0):
            check(root, model)
    elif command == "updates":
        if family == "subtitle-edit":
            from subtitle_edit import updates
            updates(base)
            return
        from huggingface_hub import HfApi
        receipt = json.loads((base / "current/receipt.json").read_text())
        changes = [a["repo"] for a in receipt["assets"] if "repo" in a and
            (HfApi().space_info if a.get("repo_type") == "space" else HfApi().model_info)(a["repo"]).sha != a["revision"]]
        message = "发现更新：" + ", ".join(changes) if changes else "模型权重已是当前版本。"
        result = dict(at=time.time(), available=bool(changes), message=message, updates=changes)
        (base / "current/update-status.json").write_text(json.dumps(result, ensure_ascii=False))
        emit("completed", model=model, message=message, updates=changes)
    elif command == "rollback":
        with FileLock(str(base / ".lock"), timeout=0):
            previous, current, temporary = base / "previous", base / "current", base / "swap"
            if not (previous / "receipt.json").exists():
                raise RuntimeError("没有可回退的版本。")
            if temporary.exists():
                raise RuntimeError("检测到未完成的版本切换，请先修复。")
            current.rename(temporary)
            try:
                previous.rename(current)
                check(root, model)
            except BaseException:
                if current.exists():
                    current.rename(previous)
                temporary.rename(current)
                raise
            temporary.rename(previous)
    elif command == "uninstall":
        with FileLock(str(base / ".lock"), timeout=0):
            trash_model(base)
            emit("completed", model=model, message="模型已移到废纸篓，共享运行环境和创作结果已保留。")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--root", type=Path, required=True)
    p.add_argument("--model", required=True)
    p.add_argument("--action", choices=["install","repair","check","updates","update","rollback","uninstall"],required=True)
    a = p.parse_args()
    try:
        action(a.root, a.model, a.action)
    except Exception as e:
        emit("error", model=a.model, message=str(e))
        raise SystemExit(1)
