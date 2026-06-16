#!/usr/bin/env bash
# Liest die Version aus der csproj, erstellt ein annotiertes Tag vX.Y.Z und pusht es.
# Der Push loest .github/workflows/release.yml aus (Win-ZIP, Linux-tar.gz, AppImage).
set -euo pipefail

CSPROJ="Allpaca/Allpaca.csproj"
VER="$(grep -oPm1 '(?<=<Version>)[^<]+' "$CSPROJ" || true)"
[ -z "$VER" ] && { echo "Keine <Version> in $CSPROJ gefunden."; exit 1; }
TAG="v$VER"

if [ -n "$(git status --porcelain)" ]; then
  echo "Arbeitsverzeichnis nicht sauber - bitte erst committen."; exit 1
fi
if git rev-parse "$TAG" >/dev/null 2>&1; then
  echo "Tag $TAG existiert bereits."; exit 1
fi

git tag -a "$TAG" -m "Release $TAG"
git push origin "$TAG"
echo "Tag $TAG gepusht - der Release-Workflow laeuft jetzt."
