"""Release-input gates; no installation, signing or global trust-store changes."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import urllib.request
import zipfile


def lock():
    return json.loads(Path(__file__).with_name("macos-runtime-lock.json").read_text())


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def verify_architecture(path, architecture):
    actual = subprocess.check_output(["lipo", "-archs", str(path)], text=True).split()
    if architecture not in actual:
        raise RuntimeError(f"Native component lacks {architecture}: {path} ({actual})")
    return actual


def verify_version(name, path, expected):
    argument = "-version" if name in ("ffmpeg", "ffprobe") else "--version"
    first = subprocess.check_output([str(path), argument], text=True, timeout=30).splitlines()[0]
    if not re.search(r"(?<![\d.])" + re.escape(expected) + r"(?![\d.])", first):
        raise RuntimeError(f"{name} differs from the accepted toolchain: expected {expected}, got {first}. Review and update the lock only after acceptance.")
    return first


def prepare_certificates(destination, licenses, cache, specification):
    cache.mkdir(parents=True, exist_ok=True)
    archive = cache / (specification["sha256"] + ".whl")
    if not archive.is_file() or archive.stat().st_size != specification["size"] or sha256(archive) != specification["sha256"]:
        pending = archive.with_suffix(".partial")
        # One direct request for a small, pinned public package; never copy the
        # builder's system CA file or alter its proxy/trust configuration.
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        with opener.open(specification["url"], timeout=60) as response, pending.open("wb") as output:
            received = 0
            while chunk := response.read(65536):
                received += len(chunk)
                if received > specification["size"]:
                    raise RuntimeError("Public certificate package exceeds its pinned size")
                output.write(chunk)
        if pending.stat().st_size != specification["size"] or sha256(pending) != specification["sha256"]:
            raise RuntimeError("Public certificate package checksum mismatch; unverified file retained")
        pending.replace(archive)
    with zipfile.ZipFile(archive) as wheel:
        certificate = wheel.read("certifi/cacert.pem")
        notices = [name for name in wheel.namelist() if name.endswith("/LICENSE")]
        if not notices: raise RuntimeError("Public certificate package lacks its license")
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(certificate)
        licenses.mkdir(parents=True, exist_ok=True)
        for index, name in enumerate(notices): (licenses / ("LICENSE-" + str(index))).write_bytes(wheel.read(name))
    return dict(version=specification["version"], package_sha256=specification["sha256"], bundle_sha256=sha256(destination))
