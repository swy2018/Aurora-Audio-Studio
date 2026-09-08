#!/bin/bash
set -euo pipefail
aurora_root="$(cd "$(dirname "$0")/../../.." && pwd)"
: "${AURORA_SIGN_IDENTITY:?Set a Developer ID Application signing identity}"
aurora_dist="${AURORA_BUILD_ROOT:?Set a new, empty version-specific build directory}"
if [[ -n "${AURORA_TEST_DATA_ROOT:-}" ]]; then
  printf 'Refusing to package an isolated QA application as a release.\n' >&2
  exit 1
fi
if [[ -e "$aurora_dist" ]]; then
  printf 'Build directory already exists; choose a new directory. Nothing was removed.\n' >&2
  exit 1
fi
bash "$aurora_root/work/audio-studio/tools/build-macos.sh"
aurora_app="$aurora_dist/Aurora Audio Studio.app"
aurora_stage="$aurora_dist/dmg-content"
aurora_version="$(dotnet msbuild "$aurora_root/work/audio-studio/AuroraAudioStudio.Mac" -getProperty:Version -nologo)"
aurora_dmg="$aurora_dist/Aurora-Audio-Studio-$aurora_version-arm64.dmg"
mkdir -p "$aurora_stage"
ditto "$aurora_app" "$aurora_stage/Aurora Audio Studio.app"
ln -s /Applications "$aurora_stage/Applications"
cp "$aurora_root/docs/macOS-user-guide.md" "$aurora_stage/使用说明.md"
hdiutil create -volname 'Aurora Audio Studio' -srcfolder "$aurora_stage" -format UDZO "$aurora_dmg"
codesign --force --sign "$AURORA_SIGN_IDENTITY" --timestamp "$aurora_dmg"
codesign --verify --strict "$aurora_dmg"
hdiutil verify "$aurora_dmg"
printf 'Signed, NOT YET NOTARIZED: %s\n' "$aurora_dmg"
