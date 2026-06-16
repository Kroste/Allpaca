#!/usr/bin/env bash
# Baut aus dem linux-x64-Publish eine portable AppImage.
set -euo pipefail

VERSION="${1:?Tag/Version fehlt (Arg 1)}"
PUBLISH_DIR="${2:?Publish-Verzeichnis fehlt (Arg 2)}"
HERE="$(cd "$(dirname "$0")" && pwd)"

APPDIR="Allpaca.AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"

cp -r "$PUBLISH_DIR"/* "$APPDIR/usr/bin/"
cp "$HERE/AppRun"          "$APPDIR/AppRun"
chmod +x "$APPDIR/AppRun"
cp "$HERE/allpaca.desktop" "$APPDIR/allpaca.desktop"
cp "$HERE/allpaca.svg"     "$APPDIR/allpaca.svg"

# appimagetool holen (continuous build)
if [ ! -x appimagetool ]; then
  wget -q -O appimagetool \
    "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
  chmod +x appimagetool
fi

export ARCH=x86_64
OUT="Allpaca-${VERSION}-x86_64.AppImage"
./appimagetool --appimage-extract-and-run "$APPDIR" "$OUT"
echo "AppImage erstellt: $OUT"
