"""Download only declared inference assets, recording revisions and SHA-256 receipts.

The staging directory survives cancellation; an installed version is only replaced
after all declared files have been downloaded and hashed. No model code is executed.
"""
import argparse
import concurrent.futures
import fnmatch
import hashlib
import json
import os
from pathlib import Path
import shutil
import time
import urllib.request

os.environ.setdefault("HF_HUB_DISABLE_TELEMETRY", "1")
os.environ.setdefault("HF_XET_HIGH_PERFORMANCE", "0")
from huggingface_hub import HfApi, hf_hub_download
from filelock import FileLock
from huggingface_hub.utils import tqdm

CATALOG = json.loads(Path(__file__).with_name("catalog.json").read_text())


def emit(kind, **kw):
    print(json.dumps(dict(kind=kind, **kw), ensure_ascii=False), flush=True)


def digest(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(8 * 1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def validate_required(folder, spec):
    def require(path):
        if not path.resolve().is_relative_to(folder.resolve()) or not path.is_file() or path.stat().st_size == 0:
            raise RuntimeError("模型必需文件缺失或无效: " + str(path))
    for name in spec.get("required", []):
        require(folder / name)
    if spec.get("download_only"):
        for index in folder.rglob("*.index.json"):
            require(index)
            weight_map = json.loads(index.read_text(encoding="utf-8")).get("weight_map", {})
            for name in set(weight_map.values()):
                require(index.parent / name)


def download(root, model, update=False):
    spec = CATALOG[model]
    base = root / "models" / model
    if not base.resolve().is_relative_to(root.resolve()) or base.is_symlink():
        raise ValueError("Invalid model directory")
    for version in ("current", "staging", "previous", "history"):
        if (base / version).is_symlink():
            raise ValueError("Model version must not be a symlink")
    base.mkdir(parents=True, exist_ok=True)
    with FileLock(str(base / ".lock"), timeout=0):
        current = base / "current"
        if not current.exists() and (base / "previous/receipt.json").exists():
            (base / "previous").rename(current)
        receipt_file = current / "receipt.json"
        old = json.loads(receipt_file.read_text()) if receipt_file.exists() else None
        staging = base / "staging"
        staging.mkdir(exist_ok=True)
        assets, files = [], []
        for asset in spec["assets"]:
            previous = next((a for a in (old or {}).get("assets", []) if a.get("repo") == asset["repo"]), None)
            revision = None if update or not previous else previous["revision"]
            repo_type = asset.get("repo_type", "model")
            get_info = HfApi().space_info if repo_type == "space" else HfApi().model_info
            info = get_info(asset["repo"], revision=revision, files_metadata=True)
            record = dict(repo=asset["repo"], revision=info.sha)
            if repo_type != "model": record["repo_type"] = repo_type
            assets.append(record)
            patterns = asset.get("patterns", ["*"])
            selected = [s for s in info.siblings if any(fnmatch.fnmatch(s.rfilename, p) for p in patterns) and s.rfilename != ".gitattributes"]
            if not selected:
                raise RuntimeError("No matching files: " + asset["repo"])
            for s in selected:
                files.append((asset, info.sha, s))
        urls = spec.get("urls", [])
        assets.extend(dict(url=u["url"], revision=u["md5"]) for u in urls)
        total = sum(s.size or 0 for _, _, s in files) + sum(u["size"] for u in urls)
        bundled = []
        for relative in spec.get("bundled", []):
            source = root / "envs" / spec["family"] / "lib/python3.11/site-packages" / relative
            if not source.is_file():
                raise RuntimeError("请先安装运行环境: " + spec["family"])
            bundled.append(source)
            total += source.stat().st_size
        expected_paths = {str(Path(a["target"]) / s.rfilename) for a, _, s in files} | {"bundled/"+p.name for p in bundled}
        expected_paths.update(u["target"] for u in urls)
        if old and old.get("assets") == assets and {f["path"] for f in old["files"]} == expected_paths:
            if all((current/f["path"]).is_file() and (current/f["path"]).stat().st_size == f["size"] and digest(current/f["path"]) == f["sha256"] for f in old["files"]):
                validate_required(current, spec)
                emit("completed", model=model, total=total, message="模型文件完整，已复用本地版本。")
                return old
        if shutil.disk_usage(root).free < total + 1024**3:
            raise RuntimeError("磁盘剩余空间不足，需要为模型和下载缓存预留空间。")
        records, done = [], 0
        for asset in urls:
            relative = Path(asset["target"])
            if relative.is_absolute() or ".." in relative.parts:
                raise ValueError("Invalid model file path")
            dest = staging / relative
            dest.parent.mkdir(parents=True, exist_ok=True)
            partial = dest.with_suffix(dest.suffix + ".partial")
            offset = partial.stat().st_size if partial.exists() else 0
            if offset != asset["size"]:
                request = urllib.request.Request(asset["url"], headers={"Range": f"bytes={offset}-"} if offset else {})
                with urllib.request.urlopen(request, timeout=120) as response:
                    append = offset > 0 and response.status == 206
                    if not append: offset = 0
                    with partial.open("ab" if append else "wb") as stream:
                        last = 0
                        while block := response.read(1024 * 1024):
                            stream.write(block); offset += len(block)
                            if time.monotonic() - last > 1:
                                last = time.monotonic()
                                emit("progress", model=model, stage="下载 " + relative.name, completed=done+offset, total=total)
            with partial.open("rb") as stream:
                checksum = hashlib.file_digest(stream, "md5").hexdigest()
            if partial.stat().st_size != asset["size"] or checksum != asset["md5"]:
                partial.unlink()
                raise RuntimeError("文件大小或 MD5 校验不一致: " + relative.name)
            partial.replace(dest)
            records.append(dict(path=str(relative), size=dest.stat().st_size, sha256=digest(dest)))
            done += dest.stat().st_size
        for source in bundled:
            dest = staging / "bundled" / source.name
            dest.parent.mkdir(exist_ok=True)
            shutil.copy2(source, dest)
            records.append(dict(path="bundled/"+source.name, size=dest.stat().st_size, sha256=digest(dest)))
            done += dest.stat().st_size
        for asset, revision, entry in files:
            relative = Path(asset["target"]) / entry.rfilename
            dest = staging / relative
            if relative.is_absolute() or ".." in relative.parts:
                raise ValueError("Invalid model file path")
            expected = entry.lfs.sha256 if entry.lfs else None
            prior = next((f for f in (old or {}).get("files", []) if f["path"] == str(relative)), None)
            old_file = current / relative
            if not dest.exists() and prior and old_file.is_file() and expected and prior["sha256"] == expected:
                # Never mutate an installed file through a hard link.
                if old_file.stat().st_size == entry.size and digest(old_file) == prior["sha256"]:
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(old_file, dest)
            emit("progress", model=model, stage="下载 " + entry.rfilename, completed=done, total=total)
            class TransferProgress(tqdm):
                def display(self, *args, **kwargs):
                    if time.monotonic() - getattr(self, "last_emit", 0) >= 1:
                        self.last_emit = time.monotonic()
                        emit("progress", model=model, stage="下载 " + entry.rfilename,
                            completed=done + getattr(self, "n", 0), total=total)
            if not expected or not dest.is_file() or dest.stat().st_size != entry.size or digest(dest) != expected:
                downloaded = hf_hub_download(asset["repo"], entry.rfilename, revision=revision,
                    repo_type=asset.get("repo_type", "model"), local_dir=str(staging / asset["target"]), force_download=dest.exists(), tqdm_class=TransferProgress)
                dest = Path(downloaded)
            sha = digest(dest)
            if expected and sha != expected:
                raise RuntimeError("SHA-256 校验不一致: " + entry.rfilename)
            records.append(dict(path=str(relative), size=dest.stat().st_size, sha256=sha))
            done += dest.stat().st_size
        validate_required(staging, spec)
        receipt = dict(model=model, installed_at=time.time(), family=spec["family"], assets=assets, files=records)
        (staging / "receipt.json").write_text(json.dumps(receipt, indent=2))
        previous = base / "previous"
        if current.exists():
            if previous.exists():
                history = base / "history"
                history.mkdir(exist_ok=True)
                previous.rename(history / str(time.time_ns()))
            current.rename(previous)
        try:
            staging.rename(current)
        except BaseException:
            if previous.exists() and not current.exists():
                previous.rename(current)
            raise
        emit("completed", model=model, total=total)
        return receipt


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--root", type=Path, required=True)
    p.add_argument("--models", nargs="+", choices=CATALOG, required=True)
    p.add_argument("--update", action="store_true")
    args = p.parse_args()
    args.root.mkdir(parents=True, exist_ok=True)
    def run(m):
        try:
            download(args.root, m, args.update)
            return True
        except Exception as e:
            emit("error", model=m, message=str(e))
            return False
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        results = list(pool.map(run, args.models))
    raise SystemExit(0 if all(results) else 1)
