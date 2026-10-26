#!/bin/zsh
# Builds pk3DS.app for this Mac: self-contained (no .NET install needed), ad-hoc signed, not notarized.
# Usage: pk3DS.Mac/build-app.sh [output folder]   (default: pk3DS.Mac/bin/app)
set -e
cd "${0:A:h}/.."
out="${1:-pk3DS.Mac/bin/app}"
app="$out/pk3DS.app"

dotnet publish pk3DS.Mac/pk3DS.Mac.csproj -c Release -r osx-arm64 --self-contained -o "$out/publish"
rm -rf "${app:?}"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$out/publish/." "$app/Contents/MacOS/"
cp pk3DS.Mac/Info.plist "$app/Contents/Info.plist"
codesign --force --deep --sign - "$app"
echo "Built $app"
