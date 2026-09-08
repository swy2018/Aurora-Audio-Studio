#!/bin/bash
set -euo pipefail
aurora_root="$(cd "$(dirname "$0")/../../.." && pwd)"
aurora_dist="${AURORA_BUILD_ROOT:-$aurora_root/dist/macos-arm64}"
aurora_app="$aurora_dist/Aurora Audio Studio.app"
aurora_version="$(dotnet msbuild "$aurora_root/work/audio-studio/AuroraAudioStudio.Mac" -getProperty:Version -nologo)"
[[ "$aurora_version" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)(-beta\.([0-9]+))?$ ]] || { printf 'Expected a unified release version.\n' >&2; exit 1; }
aurora_stage=99
if [[ -n "${BASH_REMATCH[5]:-}" ]]; then
  aurora_stage=$((10#${BASH_REMATCH[5]}))
  ((aurora_stage >= 1 && aurora_stage <= 98)) || { printf 'Beta number must be 1..98.\n' >&2; exit 1; }
fi
aurora_build=$((10#${BASH_REMATCH[1]} * 1000000 + 10#${BASH_REMATCH[2]} * 10000 + 10#${BASH_REMATCH[3]} * 100 + aurora_stage))
export DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT=false
dotnet publish "$aurora_root/work/audio-studio/AuroraAudioStudio.Mac" -c Release -r osx-arm64 --self-contained true -o "$aurora_dist/publish"
mkdir -p "$aurora_app/Contents/MacOS" "$aurora_app/Contents/Resources" "$aurora_dist/Aurora.iconset"
ditto "$aurora_dist/publish" "$aurora_app/Contents/MacOS"
swiftc -O -target arm64-apple-macosx14.0 "$aurora_root/work/audio-studio/AuroraAudioStudio.Mac/Runtime/render-midi.swift" -o "$aurora_app/Contents/MacOS/Runtime/render-midi"
swiftc -O -target arm64-apple-macosx14.0 "$aurora_root/work/audio-studio/AuroraAudioStudio.Mac/Runtime/trash-item.swift" -o "$aurora_app/Contents/MacOS/Runtime/trash-item"
swiftc -O -target arm64-apple-macosx14.0 "$aurora_root/work/audio-studio/AuroraAudioStudio.Mac/Runtime/install-update.swift" -o "$aurora_app/Contents/MacOS/Runtime/install-update"
for aurora_size in 16 32 128 256 512; do
  sips -z "$aurora_size" "$aurora_size" "$aurora_root/work/audio-studio/AuroraAudioStudio/Assets/AuroraIcon.png" --out "$aurora_dist/Aurora.iconset/icon_${aurora_size}x${aurora_size}.png" >/dev/null
  aurora_double=$((aurora_size * 2))
  sips -z "$aurora_double" "$aurora_double" "$aurora_root/work/audio-studio/AuroraAudioStudio/Assets/AuroraIcon.png" --out "$aurora_dist/Aurora.iconset/icon_${aurora_size}x${aurora_size}@2x.png" >/dev/null
done
iconutil -c icns "$aurora_dist/Aurora.iconset" -o "$aurora_app/Contents/Resources/Aurora.icns"
cat > "$aurora_app/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>com.suwan.aurora.mac</string>
<key>CFBundleName</key><string>Aurora Audio Studio</string>
<key>CFBundleDisplayName</key><string>Aurora Audio Studio</string>
<key>CFBundleExecutable</key><string>Aurora</string>
<key>CFBundleIconFile</key><string>Aurora.icns</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>1.9.0</string>
<key>CFBundleVersion</key><string>19006</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSMicrophoneUsageDescription</key><string>由你发起录音时采集参考音频，用于本地配音与声音克隆。</string>
<key>LSMinimumSystemVersion</key><string>26.0</string>
<key>NSPrincipalClass</key><string>NSApplication</string>
<key>CFBundleDevelopmentRegion</key><string>zh_CN</string>
<key>CFBundleLocalizations</key><array><string>zh_CN</string><string>zh_TW</string><string>en</string><string>ja</string></array>
<key>NSAppTransportSecurity</key><dict><key>NSAllowsLocalNetworking</key><true/></dict>
</dict></plist>
PLIST
plutil -replace CFBundleShortVersionString -string "${aurora_version%%-*}" "$aurora_app/Contents/Info.plist"
plutil -replace CFBundleVersion -string "$aurora_build" "$aurora_app/Contents/Info.plist"
if [[ -n "${AURORA_TEST_DATA_ROOT:-}" ]]; then
  [[ "$AURORA_TEST_DATA_ROOT" = /* && "$AURORA_TEST_DATA_ROOT" != / ]] || { printf 'Test data root must be an absolute non-root path.\n' >&2; exit 1; }
  # Launch Services-scoped test environment, not global user settings (Apple LSEnvironment).
  plutil -replace CFBundleIdentifier -string com.suwan.aurora.mac.qa "$aurora_app/Contents/Info.plist"
  plutil -replace CFBundleName -string 'Aurora QA' "$aurora_app/Contents/Info.plist"
  plutil -replace CFBundleDisplayName -string 'Aurora QA' "$aurora_app/Contents/Info.plist"
  plutil -insert LSEnvironment -json '{}' "$aurora_app/Contents/Info.plist"
  plutil -insert LSEnvironment.AURORA_DATA_ROOT -string "$AURORA_TEST_DATA_ROOT" "$aurora_app/Contents/Info.plist"
  plutil -insert LSEnvironment.AURORA_KEEP_TEST_FILES -string 1 "$aurora_app/Contents/Info.plist"
fi
python3 "$aurora_root/work/audio-studio/tools/bundle-macos-tools.py" "$aurora_app"
if [[ -n "${AURORA_SIGN_IDENTITY:-}" ]]; then
  python3 "$aurora_root/work/audio-studio/tools/sign-macos.py" "$aurora_app" "$AURORA_SIGN_IDENTITY"
else
  codesign --force --deep --sign - "$aurora_app"
fi
codesign --verify --deep --strict "$aurora_app"
printf '%s\n' "$aurora_app"
