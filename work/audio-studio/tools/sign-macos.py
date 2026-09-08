#!/usr/bin/env python3
"""Sign the flat Avalonia bundle, all MacOS components before the apphost."""
import pathlib
import subprocess
import sys
import time


def sign_command(command):
    # Apple can transiently reject a timestamp request midway through a bundle.
    # Retry only that observed failure; never drop --timestamp or skip a file.
    for attempt in range(3):
        result = subprocess.run(command, capture_output=True, text=True)
        if result.returncode == 0:
            if result.stderr: print(result.stderr, end="", file=sys.stderr)
            return
        if "The timestamp service is not available" not in result.stderr or attempt == 2:
            if result.stderr: print(result.stderr, end="", file=sys.stderr)
            raise subprocess.CalledProcessError(result.returncode, command, result.stdout, result.stderr)
        print("Apple timestamp temporarily unavailable; retrying this component.", flush=True)
        time.sleep(2 * (attempt + 1))


def sign(app: pathlib.Path, identity: str) -> None:
    apphost = app / "Contents/MacOS/Aurora"
    if not apphost.is_file():
        raise SystemExit("Expected an Aurora application bundle")
    entitlements = pathlib.Path(__file__).with_name("macos-entitlements.plist")
    command = ["codesign", "--force", "--sign", identity, "--timestamp", "--options", "runtime"]
    count = 0
    # Apple's MacOS resource rule treats DLLs and runtime scripts as nested code too.
    # Signing the apphost itself resolves to the whole bundle, so leave it for last.
    for path in sorted((app / "Contents/MacOS").rglob("*"), key=lambda item: len(item.parts), reverse=True):
        if path == apphost or path.is_symlink() or not path.is_file():
            continue
        sign_command([*command, str(path)])
        count += 1
    # The .NET apphost needs JIT. Keep debugger and library-validation exceptions off.
    sign_command([*command, "--entitlements", str(entitlements), str(app)])
    subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
    print(f"Signed {count} nested components and the app bundle")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: sign-macos.py APP DEVELOPER_ID_IDENTITY")
    sign(pathlib.Path(sys.argv[1]).resolve(), sys.argv[2])
