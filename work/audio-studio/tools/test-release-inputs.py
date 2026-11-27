"""Offline release checks. No model installation or user-state mutation."""
import argparse
import base64
import csv
import hashlib
import io
import tempfile
from pathlib import Path
from unittest.mock import patch
import zipfile
import macos_bundle_policy as mac


def verify_wheel(original, derived):
    with zipfile.ZipFile(original) as source, zipfile.ZipFile(derived) as target:
        old = "basic_pitch-0.4.0.dist-info/"
        new = "basic_pitch-0.4.0+aurora.onnx.1.dist-info/"
        compared = 0
        for name in source.namelist():
            if name.endswith("/") or name in (old + "METADATA", old + "RECORD"):
                continue
            other = new + name[len(old):] if name.startswith(old) else name
            assert source.read(name) == target.read(other), "Changed upstream code/model/license: " + name
            compared += 1
        rows = list(csv.reader(io.StringIO(target.read(new + "RECORD").decode())))
        assert {r[0] for r in rows} == set(target.namelist()), "RECORD does not cover every file"
        for name, digest, length in rows:
            if name == new + "RECORD":
                assert digest == length == ""
                continue
            data = target.read(name)
            assert int(length) == len(data)
            expected = base64.urlsafe_b64encode(hashlib.sha256(data).digest()).decode().rstrip("=")
            assert digest == "sha256=" + expected, name
        metadata = target.read(new + "METADATA").decode()
        headers = metadata.split("\n\n", 1)[0]
        assert "Requires-Dist: onnxruntime>=1.17,<2" in headers
        assert "Requires-Dist: tensorflow" not in headers
        print(f"PASS {compared} upstream code/model/license files are byte-identical; all {len(rows)} RECORD rows validated")


def policy_checks():
    with patch.object(mac.subprocess, "check_output", return_value="x86_64 arm64\n"):
        assert mac.verify_architecture(Path("fixture"), "arm64") == ["x86_64", "arm64"]
    with patch.object(mac.subprocess, "check_output", return_value="x86_64\n"):
        try: mac.verify_architecture(Path("fixture"), "arm64")
        except RuntimeError: pass
        else: raise AssertionError("Wrong architecture accepted")
    with patch.object(mac.subprocess, "check_output", return_value="uv 0.12.23\n"):
        mac.verify_version("uv", Path("fixture"), "0.12.23")
        try: mac.verify_version("uv", Path("fixture"), "0.12.2")
        except RuntimeError: pass
        else: raise AssertionError("Version prefix accepted")
    root = Path(tempfile.mkdtemp(prefix="aurora-release-policy-"))
    output, licenses = root / "ca.pem", root / "licenses"
    package = root / "fixture.whl"
    with zipfile.ZipFile(package, "w") as wheel:
        wheel.writestr("certifi/cacert.pem", b"public fixture certificate")
        wheel.writestr("certifi-1.dist-info/LICENSE", b"public fixture license")
    sha = mac.sha256(package)
    size = package.stat().st_size
    package.rename(root / (sha + ".whl"))
    specification = dict(sha256=sha, version="fixture", url="https://invalid.invalid", size=size)
    with patch.object(mac.urllib.request, "build_opener", side_effect=AssertionError("Verified cache must not download")):
        mac.prepare_certificates(output, licenses, root, specification)
    assert output.read_bytes() == b"public fixture certificate"
    assert (licenses / "LICENSE-0").read_bytes() == b"public fixture license"
    print("PASS Mac pinned version/architecture checks and hash-verified public certificate reuse")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("original", type=Path)
    parser.add_argument("derived", type=Path)
    args = parser.parse_args()
    verify_wheel(args.original, args.derived)
    policy_checks()
