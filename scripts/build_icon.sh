#!/usr/bin/env bash
# Rendert das Allpaca-Logo nach Allpaca/Assets/allpaca.png.
#
# Anders als in den übrigen Kroste-Projekten gibt es hier KEIN Pillow-Skript, das
# das Motiv nachbaut: Allpaca zeichnet sein Logo zur Laufzeit in
# Allpaca/Services/AppIcon.cs (SkiaSharp) für Fenster, Tray und Info-Fenster. Ein
# zweites Motiv in Python würde über kurz oder lang davon abdriften. Dieses Skript
# ruft deshalb den vorhandenen Renderer auf.
#
# Eine .ico wird bewusst nicht erzeugt -- Allpaca ist Linux-only, ein
# Windows-Exe-Icon hätte keinen Abnehmer. Das AppImage nutzt
# packaging/linux/allpaca.svg.
set -euo pipefail

HERE="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$HERE/Allpaca/Assets/allpaca.png}"

dotnet run --project "$HERE/Allpaca/Allpaca.csproj" -c Release -- --export-icon "$OUT"
echo "Icon geschrieben: $OUT"
