# Allpaca

Eine Avalonia-12-Anwendung, die **alle Installationsquellen unter Bazzite** an
einem Ort sichtbar und verwaltbar macht:

- **Flatpak** (User + System)
- **Homebrew** (Formulae + Casks)
- **rpm-ostree** (gelayerte Pakete + OS-Updates)
- **Distrobox** (Container + Drill-down in Inhalte)
- **AppImage** (.desktop-integriert + lose Dateien)
- **pipx** (Python-CLI-Tools)

## Features

### Inventar
- Live-Befüllung pro Quelle, parallel + fehlertolerant
- Sortierung (Name / Größe / Quelle), Suche, Per-Quelle-Filter
- **Per-App-Icons** in der Liste (PNG + SVG via `Svg.Skia`, Sandbox-Cache von
  `/var/lib/flatpak/.../hicolor` nach `~/.cache/Allpaca/flatpak-system-icons/`)
- Duplikat-Hinweis (⚠ "auch verfügbar in Flatpak, AppImage")
- Update-Badge (↑) und farbiger Distrobox-Status-Pill (Up/Created/Exited)
- Flatpak-Runtimes per Toggle einblendbar

### Verwaltung
- Install/Uninstall/Update über ein Live-Log-Fenster mit Cancel + Exit-Code-Status
- Mehrfachauswahl + Batch (Flatpak/Homebrew nativ, AppImage/pipx iteriert)
- Bestätigungsdialog vor destruktiven Aktionen (Distrobox bekommt einen
  Sondertext, weil dort der ganze Container gelöscht wird)
- rpm-ostree-Mutationen via `pkexec`, mit Reboot-Hinweis nach Erfolg
- Distrobox-Drill-down: Pakete *innerhalb* eines Containers anzeigen, tolerant
  gegen dpkg/rpm/pacman/apk
- **Install per Such-UI** (🔍 "+ Installieren …") mit Per-Source-Toggle (Flatpak,
  Homebrew) und Confirm
- **„Untrusted-Tap"-Erkennung** für Homebrew-Casks aus `ublue-os/tap` mit
  One-Click-`brew trust`-Button im LogWindow

### Update-Check (im Hintergrund nach jedem Refresh)
- Flatpak: `flatpak remote-ls --updates`
- Homebrew: `brew outdated --json=v2`
- rpm-ostree: `rpm-ostree upgrade --check` → globaler OS-Banner mit
  „Jetzt aktualisieren"-Button (führt `pkexec rpm-ostree upgrade` aus)

### KI-Integration (v3)
Multi-Provider-Abstraktion: **Ollama** (Default, lokal), **OpenAI**, **Anthropic**,
**Google Gemini**. Provider/Endpoint/Modell werden persistiert; API-Key bleibt
bewusst in-memory (libsecret folgt).

- **Settings-Fenster** (⚙) mit Verbindungstest und dynamischer Ollama-Modellliste
  (`GET /api/tags`) plus kuratierter Pull-Empfehlung mit Live-Fortschritt
  (`POST /api/pull`-Stream)
- **🤖 Fehlerdiagnose** im LogWindow bei `State=Failed`: KI bekommt Title +
  ExitCode + die letzten 50 Log-Zeilen und schlägt einen Befehl vor
- **🧹 Aufräum-Analyse**: KI bekommt die Paketliste (Runtimes raus,
  Cross-Source-Duplikate vor-markiert) und liefert DUPLIKATE / WAISEN-VERDACHT /
  EVENTUELL ÜBERFLÜSSIG
- **🤖 NL-Suche** im Such-Fenster: „Tool zum Videoschneiden" →
  `org.kde.kdenlive` (Flatpak), `ffmpeg` (Homebrew) o. ä. mit Begründung

### Komfort
- Settings persistiert unter `$XDG_CONFIG_HOME/Allpaca/settings.json` (Sort,
  ShowRuntimes, Source-Filter, AI-Provider/Endpoint/Modell)
- Tastatur-Shortcuts: `Ctrl+R`/`F5` Refresh, `Ctrl+F` Suche, `Ctrl+I`
  Installieren, `Ctrl+,` Settings, `Esc` schließt Subfenster
- Empty-State-Texte (Loading / nichts installiert / kein Treffer / Filter zu)
- Alle Fenster resizable (manueller Edge-Resize in `ChromeWindow`, weil
  KDE/Wayland-`BorderOnly` oft keinen treffbaren Griff hat)

## Architektur

Jede Quelle implementiert `IPackageSource` (typisierter Wrapper um das
jeweilige CLI-Tool). Ein zentraler, **sandbox-bewusster `ProcessRunner`**
entscheidet, ob ein Kommando direkt oder über `flatpak-spawn --host` läuft —
damit funktioniert die Binary identisch nativ auf dem Host *und* in der
`dotnet10`-Distrobox.

```
IPackageSource ── FlatpakSource     flatpak list --columns=…
              ├─ HomebrewSource     brew info --json=v2 --installed
              ├─ RpmOstreeSource    rpm-ostree status --json (pkexec für Mutation)
              ├─ DistroboxSource    distrobox list (+ drill-down pm-probe)
              ├─ AppImageSource     .desktop + ~/Applications/, ~/Downloads/, …
              └─ PipxSource         pipx list --json

ProcessRunner     ── sandbox-bewusste Prozessausführung
SandboxDetector   ── erkennt Flatpak/Container → flatpak-spawn --host
PackageAggregator ── lädt jede Quelle parallel & fehlertolerant
IconLookup        ── PNG/SVG-Suche in hicolor + Sandbox-Cache
AppIcon           ── SkiaSharp-Renderer fürs Allpaca-Logo

Services/Ai:
  IAiAssistant + AiAssistantFactory + 4 Provider
  OllamaModelService (/api/tags + /api/pull stream)
  *PromptBuilder/*Parser pro Feature (Diagnose, Cleanup, Suggestion)
```

## Wichtige Designentscheidungen

- **Host-Kommandos:** flatpak/brew/rpm-ostree/distrobox/pipx liegen auf dem Host.
  Allpaca ist als **native Host-Binary** gedacht (nicht Flatpak-sandboxed).
  In einer Sandbox greift automatisch der `flatpak-spawn --host`-Wrapper.
- **brew-PATH:** In GUI-Sessions (KDE/Wayland) fehlt `brew` oft im PATH.
  `HomebrewSource` fällt auf `/home/linuxbrew/.linuxbrew/bin/brew` zurück;
  analog für pipx auf `~/.local/bin/pipx`.
- **Rechte:** Alle Lesepfade brauchen kein root. Für rpm-ostree-Mutationen
  läuft `pkexec` (kein Daemon).
- **KI ist additiv**: fällt der Provider aus, läuft Allpaca normal weiter.
  Defaults landen auf Ollama (datenschutzfreundlich, lokal).
- **App-Icon einheitlich**: programmatisch via SkiaSharp gerendert, auf allen
  Fenstern + im Titlebar + Taskbar.

## Bauen (in der dotnet10-Distrobox)

```bash
cd Allpaca
dotnet restore
dotnet build -c Release
dotnet run --project Allpaca       # oder die gebaute Binary auf dem Host starten
dotnet test                        # 118 Tests, alle parser/Logik pure-Funktional
```

> **Avalonia-Version:** steht in `Directory.Packages.props` auf `12.1.1` — 12.1
> bringt den nativen Wayland-Backend, was auf KDE Wayland (Bazzite) direkt
> spürbar ist. Alle Paketversionen liegen zentral in dieser Datei (Central
> Package Management), die Assembly-Version kommt aus dem Git-Tag (MinVer).

### Hinweis zum Ausführen
Allpaca läuft sowohl direkt auf dem Host als auch innerhalb einer
`dotnet10`-Distrobox. In der Sandbox sind alle Paket-CLI-Tools via
`flatpak-spawn --host` erreichbar, und der Icon-Cache mirrored die System-
Flatpak-Icons einmal pro Session ins geteilte Home — also keine fehlenden
Logos in der Liste.

## Was offen ist

Siehe `CLAUDE.md` §6 für die volle Roadmap. Kurz:

- KI-Streaming statt Single-Shot (Diagnose/Cleanup/Suggest werden live ausgegeben)
- libsecret-Persistenz für API-Keys (aktuell in-memory)
- Weitere Quellen: cargo install, npm -g, toolbx
- Toast-/Tray-Notification beim Update-Check
- Drag-and-drop AppImages

## Lizenz

MIT — siehe [LICENSE](LICENSE).
