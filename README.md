# Allpaca

Eine Avalonia-12-Anwendung, die **alle Installationsquellen unter Bazzite** an
einem Ort sichtbar macht: Flatpak, Homebrew, rpm-ostree-Layer, Distrobox-Container
und AppImages.

**v1 = Inventar** (read-only Auflistung aller Quellen). Verwaltung
(Installieren/Deinstallieren/Aktualisieren) ist im Code bereits abstrahiert und
folgt in v2.

## Architektur

Jede Quelle implementiert `IPackageSource` (typisierter Wrapper um das jeweilige
CLI-Tool). Ein zentraler, **sandbox-bewusster `ProcessRunner`** entscheidet, ob ein
Kommando direkt oder über `flatpak-spawn --host` läuft – damit funktioniert die
Binary identisch nativ auf dem Host *und* in deiner `dotnet10`-Distrobox.

```
IPackageSource ── FlatpakSource     (flatpak list --columns=…)
              ├─ HomebrewSource     (brew info --json=v2 --installed)
              ├─ RpmOstreeSource    (rpm-ostree status --json)
              ├─ DistroboxSource    (distrobox list)
              └─ AppImageSource     (.desktop-Scan + Dateisystem)

PackageAggregator ── lädt jede Quelle parallel & fehlertolerant
MainWindowViewModel ── Filter, Suche, Live-Befüllung
ChromeWindow ── eigenes Fenster-Chrome (wie Magnat/NetScanner)
```

## Wichtige Designentscheidungen

- **Host-Kommandos:** flatpak/brew/rpm-ostree/distrobox liegen auf dem Host. Allpaca
  ist als **native Host-Binary** gedacht (nicht Flatpak-sandboxed). In einer Sandbox
  greift automatisch der `flatpak-spawn --host`-Wrapper.
- **brew-PATH:** In GUI-Sessions (KDE/Wayland) fehlt `brew` oft im PATH. `HomebrewSource`
  fällt deshalb auf `/home/linuxbrew/.linuxbrew/bin/brew` zurück.
- **Rechte:** Alle v1-Lesepfade brauchen kein root. Für v2-Mutationen an rpm-ostree
  bzw. System-Flatpaks ist `pkexec` vorgesehen (kein Daemon).
- **Distrobox:** v1 listet Container. Pakete *innerhalb* der Container (enter + pm list)
  kommen als Drill-down in v2.

## Bauen (in der dotnet10-Distrobox)

```bash
cd Allpaca
dotnet restore
dotnet build -c Release
dotnet run --project Allpaca       # oder die gebaute Binary auf dem Host starten
```

> **Avalonia-Version:** In `Allpaca.csproj` sind die Avalonia-Pakete als `12.0.0`
> vorbelegt. Stell das auf die exakte 12.x um, die du in Magnat/NetScanner nutzt,
> falls abweichend.

### Hinweis zum Ausführen
Zum echten Testen die App **auf dem Host** starten (direkt oder via `distrobox-host-exec`),
damit alle Quellen sichtbar sind. Läuft sie in der Distrobox, greift der Host-Wrapper –
das Dateisystem-Scanning der AppImages sieht dann aber nur das (geteilte) Home.

## v2-Roadmap

- Verwaltung verdrahten: Install/Uninstall/Update je Quelle mit Live-Log-Fenster
- `pkexec`-Flow für rpm-ostree (inkl. Reboot-Hinweis)
- Distrobox-Drill-down (Pakete pro Container)
- Update-Check (brew outdated / flatpak remote-ls --updates)
- Flatpak-Runtimes optional einblenden
- Mehrfachauswahl + Batch-Operationen
