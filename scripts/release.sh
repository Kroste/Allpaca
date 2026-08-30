#!/usr/bin/env bash
# Kroste-Release: prüft den Git-Zustand, erstellt einen annotierten Tag vX.Y.Z
# und pusht ihn (löst die Release-Action aus).
# Versionsquelle: <Version> aus Directory.Build.props/csproj falls vorhanden
# (NetScanner-Stil), sonst MinVer-Stil: letzter Tag + Bump-Abfrage.
set -euo pipefail

VERSION=$(grep -rhoP '(?<=<Version>)[^<]+' Directory.Build.props */*.csproj 2>/dev/null | head -1 || true)

if [ -z "$VERSION" ]; then
  LAST=$(git describe --tags --abbrev=0 --match 'v*' 2>/dev/null || echo v0.0.0)
  LAST=${LAST#v}
  IFS=. read -r MA MI PA <<< "$LAST"
  SUGGEST="${MA}.${MI}.$((PA+1))"
  # "|| true": bei nicht-interaktivem Aufruf (Pipe, CI) liefert read am
  # Eingabe-Ende einen Exit-Code != 0, und "set -e" würde das Skript hier
  # kommentarlos beenden - ohne Tag und ohne eine einzige Zeile Ausgabe.
  read -r -p "Neue Version [${SUGGEST}]: " VERSION || true
  VERSION=${VERSION:-$SUGGEST}
fi

if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "FEHLER: '$VERSION' ist keine gültige SemVer-Version (X.Y.Z)." >&2
  exit 1
fi
TAG="v${VERSION}"

if [ -n "$(git status --porcelain)" ]; then
  echo "FEHLER: Es gibt uncommittete Änderungen. Erst committen." >&2
  exit 1
fi
# Nur den AKTUELLEN Branch prüfen: "--branches" schaut auf alle lokalen Branches,
# und ein alter, nie gepushter WIP-Branch hätte das Release blockiert, obwohl main
# sauber ist.
if [ -n "$(git log '@{upstream}'..HEAD --oneline 2>/dev/null)" ]; then
  echo "FEHLER: Es gibt ungepushte Commits auf diesem Branch. Erst pushen." >&2
  exit 1
fi
if git rev-parse "$TAG" >/dev/null 2>&1; then
  echo "HINWEIS: Tag $TAG zeigt aktuell auf $(git log --oneline -1 "$TAG")" >&2
  answer=""
  read -r -p "Tag $TAG existiert bereits. Löschen und neu setzen? [j/N] " answer || true
  if [ "${answer,,}" != "j" ]; then
    echo "Abgebrochen."
    exit 0
  fi
  git tag -d "$TAG"
  git push origin ":refs/tags/$TAG" || true
fi

git tag -a "$TAG" -m "Release $TAG"
git push origin "$TAG"
echo "Tag $TAG auf $(git log --oneline -1 HEAD) gesetzt und gepusht."
echo "Die Release-Action baut jetzt die Pakete."
