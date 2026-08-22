# CLAUDE.md — Allpaca

Projektgedächtnis für Claude Code. **Vor jeder Änderung lesen.** Diese Datei sammelt
die hart erkauften Avalonia-12-Lektionen aus Magnat, NetScanner und DTM, damit wir
nicht zweimal in dieselbe Grube fallen.

---

## 1. Was ist Allpaca

Avalonia-12-Desktop-App, die **alle Installationsquellen unter Bazzite** an einem Ort
sichtbar macht und verwaltet: Flatpak, Homebrew, rpm-ostree-Layer, Distrobox-Container,
AppImage.

- **v1 (aktuell):** Inventar, read-only Auflistung aller Quellen.
- **v2+:** Verwaltung (install/uninstall/update) und KI-Unterstützung — siehe Roadmap (§6/§7).

---

## 2. Stack & harte Konventionen

- **.NET 10 / C# / Avalonia 12**, MVVM via **CommunityToolkit.Mvvm**, Logging via **NLog**.
- **Plattform: Linux-only.** Bewusste Entscheidung (2026-06-17): Allpaca ist ein
  Bazzite-Helfer und wird NICHT auf Windows/macOS portiert. Die Release-Action hat
  deshalb **keinen Windows-Job** — sie baut nur Linux-tar.gz + AppImage. Wenn ein
  CLI-Frontend dazukommt, dann ebenfalls Linux-fokussiert (Bazzite/Fedora Atomic).
- **Versionierung über MinVer, kein manuelles `<Version>` mehr.** Die Version kommt
  aus dem Git-Tag (`v1.6.0` → Assembly `1.6.0`); `Directory.Build.props` setzt
  `MinVerTagPrefix=v`. Ein Release entsteht durch `bash scripts/release.sh`
  (fragt die neue Version ab, taggt, pusht).
- **Zentrale Build-Konfiguration:** `Directory.Build.props` (net10.0, Nullable,
  `TreatWarningsAsErrors`, MinVer) und `Directory.Packages.props` (Central Package
  Management — der einzige Ort für Paketversionen). Die csproj-Dateien enthalten
  **keine** `Version`-Attribute an `PackageReference`.
- **Alle Fenster erben von `ChromeWindow`** (Custom Chrome, randlos, resizable, sauberes
  Shutdown, auflösungsbewusst). Niemals direkt von `Window`.
- **Aktuelle Avalonia-12-APIs verwenden** — siehe §3. Keine veralteten WPF-/alt-Avalonia-Muster.
- Kommentare/Doku/Commit-Messages auf **Deutsch, du-Form**. GitHub-Account: **`Kroste`**.
- Build/Run passiert in der **`dotnet10`-Distrobox**; getestet wird auf dem **Host**
  (sonst sind die Paket-Tools nicht sichtbar — siehe §5).
- **Privatsphäre:** KI-Features laufen bevorzugt lokal über **Ollama** (§7). Keine Cloud-Pflicht.

### 2.1 Projektstandards (gelten für ALLE Projekte, hier umgesetzt)
- **VS Code:** immer `.vscode/launch.json` + `.vscode/tasks.json` beilegen, inkl. eines
  **`clean-hard`**-Tasks (löscht bin/obj rekursiv vom Datenträger — gegen hängenden
  Avalonia-XAML-Cache). `.vscode/` wird **eingecheckt**.
- **Tests:** immer ein eigenes Testprojekt (`Allpaca.Tests`, **xunit.v3**). Reine Logik
  (Parser, Defaults) wird per `InternalsVisibleTo` testbar gemacht.
  ⚠️ xunit.v3 läuft auf der **Microsoft.Testing.Platform**; das .NET-10-SDK kennt den
  alten VSTest-Pfad nicht mehr. Deshalb steht der Opt-in in `global.json`
  (`"test": { "runner": "Microsoft.Testing.Platform" }`) und das Testprojekt ist
  `<OutputType>Exe</OutputType>`. Ohne beides bricht `dotnet test` ab.
- **CI:** `.github/workflows/ci.yml` baut und testet bei jedem Push auf `main` und
  bei jedem PR. Nach dem Push den Status prüfen (`gh run list --repo Kroste/Allpaca`).
- **Release:** GitHub-Action (`.github/workflows/release.yml`), die auf Tag `v*.*.*`
  **Linux-tar.gz + AppImage** baut (kein Windows — siehe Linux-only oben). **Node 24**
  im Linux-Job. Auslösung über den VS-Code-Task `release (tag + push)` bzw.
  `scripts/release.sh`.
- **KI:** Multi-Provider-Abstraktion (`Services/Ai`) für **ChatGPT, Claude, Gemini, Ollama**
  — Provider/Endpoint/Modell/Key konfigurierbar, Ollama als Default (§7).
- **InfoBox (Pflicht):** jedes Projekt hat ein Info-/About-Fenster (`Views/InfoWindow`) mit
  App-Name, Version (aus Assembly), Kurzbeschreibung, GitHub-Link und **Buy-me-a-coffee-Button**.
  ⚠️ GitHub-Slug und BMC-Handle in `InfoWindow.axaml.cs` auf die echten Werte setzen.

---

## 3. Avalonia 12 — Fallstricke & gelernte Lektionen

> Das ist der wichtigste Abschnitt. Jeder Eintrag steht hier, weil er uns schon mal
> Zeit gekostet hat.

### 3.1 TextBox-Platzhalter
- ❌ `Watermark="..."`  → existiert in unserem Avalonia 12 nicht / verhält sich anders.
- ✅ **`PlaceholderText="..."`**

### 3.2 ItemsControl / ListBox / ComboBox befüllen
- ❌ `Items="{Binding ...}"` (der `Items`-Setter ist weg/deprecated).
- ✅ **`ItemsSource="{Binding ...}"`**

### 3.3 Custom Window Chrome (randloses Fenster) — v12-API
**In Avalonia 12 wurde `Window.ExtendClientAreaChromeHints` (inkl. Enum
`ExtendClientAreaChromeHints.NoChrome`) komplett ENTFERNT** (Issues #21160/#21212,
v12-Breaking-Changes-Doc). Wer das alte v11-Muster nutzt → `CS0103`. Das funktionierende
Muster lebt in `ChromeWindow`:

```csharp
ExtendClientAreaToDecorationsHint = true;
ExtendClientAreaTitleBarHeightHint = -1;
WindowDecorations = WindowDecorations.BorderOnly;   // NICHT .None!
CanResize = true;
```

- **Falle:** `WindowDecorations.None` entfernt die **nativen Resize-Griffe** an den Kanten.
  `BorderOnly` blendet nur die gezeichnete Titelleiste aus und behält das Resizing → genau
  das, was wir wollen.
- Enum heißt `WindowDecorations` (Namespace `Avalonia.Controls`).
- Titelleiste im XAML selbst bauen, Drag via `BeginMoveDrag(e)` im `PointerPressed`.
- Min/Max/Close rufen die `protected`-Handler aus `ChromeWindow`.

### 3.3b DevTools-Paket umbenannt (v12)
**`Avalonia.Diagnostics` ist in v12 ENTFERNT** und durch **`AvaloniaUI.DiagnosticsSupport`**
ersetzt (Debug-only, z. B. `2.2.1`). `Avalonia.Diagnostics` endet auf NuGet bei `11.3.x` →
`NU1102`, wenn man `12.0.0` referenziert. Niemals auf `11.3.x` pinnen — das ist
binärinkompatibel mit Avalonia 12.

```xml
<PackageReference Include="AvaloniaUI.DiagnosticsSupport" Version="2.2.1"
                  Condition="'$(Configuration)' == 'Debug'" />
```

### 3.4 Fenster passen sich nicht an die Auflösung an  ← häufiger Fehler
Symptom: Fenster öffnet größer als der (kleinere) Monitor, Buttons unerreichbar.

Regeln:
- **Keine fixe Riesengröße** hart setzen. Sinnvolle `Width/Height` + `MinWidth/MinHeight`.
- **`WindowStartupLocation="CenterScreen"`** (in `ChromeWindow` bereits gesetzt).
- **`ChromeWindow.ClampToWorkingArea()`** läuft automatisch in `OnOpened` und deckelt die
  Größe auf den Arbeitsbereich des aktiven Screens.
- **Achtung Einheiten:** `Window.Width/Height` sind **DIPs**, `Screen.WorkingArea` ist in
  **physischen Pixeln**. Immer durch `Screen.Scaling` teilen, sonst rechnet man auf
  HiDPI (deine 5120×1440-Panels) komplett falsch:
  ```csharp
  var maxW = screen.WorkingArea.Width / screen.Scaling;   // DIPs
  ```
- `Screens.ScreenFromVisual(this)` erst nach `OnOpened` zuverlässig (Fenster braucht Handle).

### 3.5 Theme / FluentTheme
- ❌ `<FluentTheme Mode="Dark"/>` — die `Mode`-Property ist raus.
- ✅ `<FluentTheme/>` + auf `Application`-Ebene **`RequestedThemeVariant="Dark"`**.

### 3.6 Compiled Bindings (Standard hier)
- `AvaloniaUseCompiledBindingsByDefault=true` in der csproj.
- **Jede View UND jedes DataTemplate braucht `x:DataType`** — sonst rote Wellen / Laufzeitfehler.
  ```xml
  <DataTemplate x:DataType="vm:PackageItemViewModel"> ... </DataTemplate>
  ```
- Null-Checks ohne eigenen Converter:
  `Converter={x:Static conv:ObjectConverters.IsNotNull}` mit
  `xmlns:conv="clr-namespace:Avalonia.Data.Converters;assembly=Avalonia.Base"`.
- Bool-Negation direkt in der Bindung erlaubt: `IsVisible="{Binding !IsAvailable}"`.

### 3.7 CommunityToolkit.Mvvm
- Klassen mit `[ObservableProperty]`/`[RelayCommand]` müssen **`partial`** sein.
- Feld `_searchText` → generierte Property **`SearchText`** (PascalCase, ohne Unterstrich).
- `[RelayCommand] private async Task RefreshAsync()` → Command **`RefreshCommand`**.
- Computed Properties an Quell-Properties hängen:
  ```csharp
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(CountSummary))]
  private int _visibleCount;
  ```
- Hook bei Property-Änderung: `partial void OnSearchTextChanged(string value)`.

### 3.8 Threading & ObservableCollection
- **`ObservableCollection` nur auf dem UI-Thread mutieren.** Sonst Layout-Crash.
- In ViewModel-Methoden, die UI-Collections anfassen, **kein `ConfigureAwait(false)`** —
  die Continuation soll auf dem UI-Thread landen.
- Hintergrundarbeit (Prozess-I/O) läuft in `ProcessRunner` mit `ConfigureAwait(false)`;
  Ergebnisse fließen erst danach zurück. Bei Bedarf explizit
  `Dispatcher.UIThread.Post(...)`.

### 3.9 Styles & Selektoren (CSS-artig, NICHT WPF-Trigger)
```xml
<Style Selector="Button.accent:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="#26A267"/>
</Style>
```
- Klassen via `Classes="accent danger"`. Pseudo: `:pointerover`, `:selected`, `:disabled`.
- Template-Teile über `/template/ ContentPresenter` ansprechen.

### 3.9b FluentTheme „klaut" Button.accent → Background über /template/ erzwingen
FluentTheme bringt selbst einen `Button.accent`-Style mit eigenem ControlTemplate mit.
Dessen ContentPresenter ist **nicht** per TemplateBinding an `Button.Background` gehängt
— dein eigener `<Setter Property="Background" Value="#2BB673"/>` am Button-Element wird
schlicht ignoriert, der Button bleibt Fluent-Blau. Genau diese Falle hat den
„Aktualisieren"-Button in Allpaca v1.1.5 versteckt blau gerendert.

Fix: Background für alle relevanten Zustände direkt am ContentPresenter im Template
setzen, nicht am Button-Element:

```xml
<Style Selector="Button.accent /template/ ContentPresenter">
    <Setter Property="Background" Value="#2BB673"/>
</Style>
<Style Selector="Button.accent:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="#26A267"/>
</Style>
<Style Selector="Button.accent:pressed /template/ ContentPresenter">
    <Setter Property="Background" Value="#1F8E5A"/>
</Style>
```

`Foreground` / `Padding` / `CornerRadius` / `FontWeight` dürfen weiter direkt am Button
stehen — die hängen im Fluent-Template via TemplateBinding. Dieselbe Falle gilt
sinngemäß für jede eigene Button-Klasse, sobald FluentTheme einen gleichnamigen Style
mitliefert. **Faustregel:** wenn deine `Background`-Setter wirkungslos sind, ist die
Antwort fast immer `/template/ ContentPresenter`.

### 3.10 InitializeComponent / XAML-Loader
- In **Views**: `public MyWindow() { InitializeComponent(); }` — die Methode wird vom
  Source-Generator erzeugt, **nicht** selbst definieren.
- Nur in **`App.axaml.cs`**: `AvaloniaXamlLoader.Load(this)`.

### 3.11 Fonts
- `WithInterFont()` in `Program.cs` braucht das Paket **`Avalonia.Fonts.Inter`** — sonst
  Build-Fehler. (Schon referenziert.)

### 3.11b SkiaSharp-Linie: Svg.Skia MUSS zu Avalonia passen
`Svg.Skia` bringt die **managed** SkiaSharp mit, Avalonia die **nativen** Assets.
Driften die auseinander, bleibt der Build grün und die App stirbt beim ersten
Skia-Aufruf (also beim ersten Fenster, weil `AppIcon` das Logo rendert):

```
The version of the native libSkiaSharp library (119.0) is incompatible with this
version of SkiaSharp. Supported versions are in the range [148.0, 149.0).
```

Real passiert beim Sprung auf Avalonia 12.1.1: `Svg.Skia 5.2.x` zieht
SkiaSharp 4.148, Avalonia 12.1 liefert die nativen Assets in 3.119.
**Deshalb steht Svg.Skia auf `5.1.1`** (letzte Version auf der 3.119er Linie).
Vor jedem Bump von Avalonia ODER Svg.Skia gegenprüfen:

```bash
dotnet list Allpaca/Allpaca.csproj package --include-transitive | grep -i skia
# SkiaSharp und SkiaSharp.NativeAssets.Linux MÜSSEN dieselbe Linie haben
```

Nebenwirkung: auf der 3.119er Linie gibt es `SKPathBuilder` noch nicht, und die
`SKPath`-Bau-Methoden sind dort auch nicht deprecated — `AppIcon` baut die Pfade
also direkt über `SKPath`.

### 3.12 DataGrid (falls je gebraucht)
- Eigenes Paket `Avalonia.Controls.DataGrid` **plus** Theme-Include:
  `<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"/>`.
- Für Allpaca v1 bewusst **kein** DataGrid — gestyltes `ListBox` reicht und sieht sauberer aus.

---

## 4. Projektarchitektur

```
IPackageSource ── FlatpakSource     flatpak list --columns=…
              ├─ HomebrewSource     brew info --json=v2 --installed
              ├─ RpmOstreeSource    rpm-ostree status --json  (mutationen via pkexec)
              ├─ DistroboxSource    distrobox list --no-color (+ drill-down pm-probe)
              ├─ AppImageSource     .desktop-Scan + Dateisystem
              └─ PipxSource         pipx list --json

ServiceRegistration ── DI-Komposition (ServiceCollection); einzige Stelle, an der
                       verdrahtet wird, wer wen bekommt
GlobalExceptionHandler ── AppDomain + TaskScheduler + Dispatcher → NLog Fatal/Error
MaskingLayoutRenderer ── NLog ${masked}: entfernt API-Keys aus jeder Log-Zeile
UpdateService   ── GitHub-Release-Check + echtes Self-Update (AppImage/tar.gz)
TrayController  ── Minimieren → Tray, Menü Anzeigen/Beenden
ProcessRunner   ── zentrale, sandbox-bewusste Prozessausführung
SandboxDetector ── erkennt Flatpak/Container → flatpak-spawn --host
PackageAggregator ── lädt jede Quelle parallel & fehlertolerant
IconLookup      ── PNG/SVG-Suche in hicolor + Sandbox-Cache
AppIcon         ── SkiaSharp-Renderer fürs Allpaca-Logo

MainWindowViewModel ── Filter, Suche, Live-Befüllung, Sort, Batch, Cleanup-Trigger
ChromeWindow ── Fenster-Basis (Chrome, Auflösungs-Clamp, Edge-Resize, Esc-to-Close)

Sub-Fenster:
- LogWindow              → Stream-Operation (Install/Uninstall/Update/Trust), KI-Diagnose
- ConfirmWindow          → ShowDialog<bool> mit isDestructive-Toggle
- SearchWindow           → flatpak/brew search + 🤖-KI-Suggestions
- SettingsWindow         → AI-Provider/Endpoint/Model + Ollama-Modell-Liste + Pull
- OllamaPullWindow       → POST /api/pull mit Fortschrittsanzeige
- ContainerInspectorWindow → Distrobox-Drill-down (read-only)
- CleanupAnalysisWindow  → KI bekommt Paketliste, schlägt Duplikate/Waisen vor
- InfoWindow             → About + GitHub + BMC-Button

Services/Ai:
- IAiAssistant + Factory + 4 Provider (OpenAiCompatible für Ollama+OpenAI, Anthropic, Gemini)
- *PromptBuilder/Parser pro Feature (Diagnose, Cleanup, Suggestion); pur statisch + testbar
- OllamaModelService (/api/tags + /api/pull stream)
```

**Leitplanken:**
- Neue Quelle = neue `IPackageSource`-Implementierung. Sonst nichts anfassen.
- **Niemals** Host-Binaries direkt via `Process.Start` aufrufen — immer über `ProcessRunner`,
  damit der `flatpak-spawn --host`-Pfad greift.
- `brew` per `ResolveAsync` auflösen (PATH **oder** `/home/linuxbrew/.linuxbrew/bin/brew`),
  weil GUI-Sessions den Brew-PATH oft nicht erben.
- Mutationen mit Root (`rpm-ostree`, System-Flatpak) laufen über **`pkexec`**, kein Daemon.

---

## 5. Build, Test & Release

```bash
cd Allpaca
dotnet restore
dotnet build -c Release
dotnet run --project Allpaca

dotnet test                       # Testprojekt (xunit.v3 auf Microsoft.Testing.Platform)
bash scripts/release.sh           # taggt vX.Y.Z + push → löst Release-Action aus
```

VS-Code-Tasks: `build` (Default), `test`, `clean`, `clean-hard`, `rebuild`, `release (tag + push)`.

- **Avalonia-Version** steht in `Directory.Packages.props` auf `12.1.1` (12.1 bringt
  den nativen **Wayland-Backend** — relevant, weil auf Bazzite/KDE Wayland entwickelt
  wird; der Sprung von 12.0.0 auf 12.0.4 war seinerzeit NU1903 / GHSA-xrw6-gwf8-vvr9,
  gepatchtes Tmds.DBus.Protocol). Magnat/NetScanner ggf. mit angleichen.
- **`TreatWarningsAsErrors` ist an.** Beim Sprung auf SkiaSharp 3.x sind die
  `SKPath`-Bau-Methoden deprecated — `AppIcon` baut Pfade daher über
  `SKPathBuilder` + `Detach()`.
- **Zum echten Test auf dem Host starten** (oder `distrobox-host-exec`). In der Distrobox
  greift zwar der Host-Wrapper, aber das AppImage-Dateisystem-Scanning sieht dann nur das
  geteilte Home.
- Logs: `${ApplicationData}/Allpaca/logs/` — auf Linux also **`~/.config/Allpaca/logs/`**
  (nicht `~/.local/share`, das ist `LocalApplicationData`).
- **NLog schreibt ab `Trace` in die Datei**, ab `Info` auf die Konsole, 14 Tage Archiv.
- **Secret-Masking ist Pflicht und aktiv:** das Layout nutzt `${masked}` aus
  `Allpaca.Logging.MaskingLayoutRenderer` (Provider-Keyformate `sk-…`/`sk-ant-…`/
  `AIza…`, dazu `api_key=`, `x-api-key:`, `Authorization: Bearer`, `?key=`).
  ⚠️ **Der Renderer registriert sich über einen `[ModuleInitializer]`, nicht in
  `Program.Main`.** Kennt NLog das `${masked}` nicht, verschluckt es den Rest der
  Zeile und im Log steht eine **leere Message**. Genau das ist passiert, als die
  Registrierung nur in `Main` stand: die App loggte korrekt, der Testprozess (kein
  `Main`) schrieb lauter Zeilen ohne Text.

---

## 6. Roadmap

### v1.x — Inventar-Feinschliff  ✅ komplett
- [x] Leere Detailfelder ausblenden (Converter `StringConverters.IsNotNullOrEmpty`).
- [x] Spaltensortierung (Name / Größe / Quelle) + Sortier-Umschalter.
- [x] Flatpak-**Runtimes** optional einblenden (Toggle), aktuell nur `--app`.
- [x] Distrobox-**Status** farbig (running/created/exited).
- [x] Duplikat-Hinweis (gleiche App als Flatpak *und* AppImage).

### v2 — Verwaltung  ✅ komplett
- [x] Install/Uninstall/Update je Quelle mit **Live-Log-Fenster** (Flatpak, rpm-ostree,
      Homebrew, Distrobox, AppImage, pipx).
- [x] **`pkexec`-Flow** für rpm-ostree inkl. **Reboot-Hinweis** (Caps: `RequiresReboot`).
- [x] Bestätigungsdialog vor destruktiven Aktionen (Distrobox-Sondertext, Batch-Liste).
- [x] **Distrobox-Drill-down:** Pakete *innerhalb* eines Containers (enter + pm-Probe,
      tolerant gegen dpkg/rpm/pacman/apk). Bisher read-only; install/uninstall in der
      Container-Inspector-UI bleibt optional.
- [x] Fehler-Handling: ProcessRunner yieldet abschließende Marker-Zeile mit Exit-Code,
      LogWindow kippt rot bei != 0.

### v2.x — Komfort  ✅ komplett
- [x] **Update-Check:** `brew outdated --json=v2`, `flatpak remote-ls --updates`,
      `rpm-ostree upgrade --check` (OS-Banner). Badge „↑ Update" pro Eintrag.
- [x] **Mehrfachauswahl + Batch** (Flatpak/Homebrew nativ, AppImage/pipx Default-Iteration).
- [x] **Settings-Persistenz** unter `$XDG_CONFIG_HOME/Allpaca/settings.json` (Sort,
      ShowRuntimes, Source-Filter, AI-Provider/Endpoint/Model).

### v3 — KI-Unterstützung (§7)  ✅ Kern-Trilogie durch
- [x] Multi-Provider-Gerüst: `IAiAssistant` + Ollama/OpenAI/Claude/Gemini + Factory.
- [x] UI-Verdrahtung + Settings (Provider/Endpoint/Key) + Verbindungstest. Ollama-Modelle
      dynamisch via `/api/tags`, Pull mit Live-Fortschritt (`POST /api/pull`), kuratierte
      Empfehlungsliste.
- [x] Natürlichsprachige Suche/Empfehlung (SearchWindow „🤖 Mit KI" mit striktem
      PROVIDER|ID|REASON-Output-Format).
- [x] Aufräum-Analyse (Waisen/Duplikate/„brauchst du das noch?") in eigenem Fenster.
- [x] Fehlerdiagnose aus Operation-Logs (LogWindow „Analysieren"-Button).
- [x] **Streaming** statt Single-Shot — `IAsyncEnumerable<string> CompleteStreamAsync` in
      allen Providern (OpenAI/Ollama/Anthropic/Gemini via SSE), LogWindow + CleanupAnalysis
      schreiben live in den Antworttext; SearchWindow buffered (structured Output).
- [ ] **libsecret-Persistenz für API-Keys** — aktuell in-memory only, CLAUDE.md verlangt es.

### Spätere Quellen (optional)
- [x] `pipx` (Python-CLI-Tools, rootless, `pipx list --json`).
- [ ] `cargo install` (Rust-Tools, `cargo install --list`).
- [ ] `npm -g` (globale Node-Pakete, `npm list -g --json`).
- [ ] `toolbx` parallel zu Distrobox.

### v4 — Kroste-Standards nachgezogen (2026-08-22)
- [x] **Infrastruktur:** `Directory.Build.props` + `Directory.Packages.props` (CPM),
      MinVer statt manuellem `<Version>`, `.slnx`, `ci.yml`, `dependabot.yml`,
      `LICENSE`, `FUNDING.yml`, Release-Action auf Node-24-Action-Majors.
- [x] **xunit.v3** statt xunit 2.x (deprecated) — inkl. MTP-Opt-in in `global.json`.
- [x] **Echte Umlaute** im gesamten Repo (322 Stellen).
- [x] **DI-Container** (`ServiceRegistration`) — vorher baute sich das
      MainWindowViewModel seinen Objektgraph selbst zusammen.
- [x] **GlobalExceptionHandler** (AppDomain + TaskScheduler + Dispatcher).
- [x] **NLog:** Trace-Level + `${masked}`-Renderer gegen Secrets im Log.
- [x] **UpdateService** mit echtem Self-Update (AppImage ersetzt sich per `cp -f`,
      tar.gz wird über das Installationsverzeichnis entpackt) + „⬇ Update
      installieren"-Button im InfoWindow.
- [x] **System-Tray** (`TrayController`): Minimieren → Tray, Menü Anzeigen/Beenden.
- [x] **Persistenz atomar** (tmp + Move) mit `.broken`-Quarantäne nur bei `JsonException`.
- [x] **App-Icon als Datei** (`Allpaca/Assets/allpaca.png`) über `scripts/build_icon.sh`,
      das den vorhandenen SkiaSharp-Renderer aufruft.
- [ ] **Card-Look / Kroste-Palette**: bewusst ausgeklammert — die Views nutzen 163
      hartkodierte `#XXXXXX`-Literale statt `DynamicResource`-Keys, und `Border.card`
      kommt nirgends vor. Eigener Umbau, kein Nebenbei-Fix.
- [ ] **Localization EN+DE**: ebenfalls ausgeklammert, die UI ist komplett hart deutsch.

### Polishing & QoL (laufend)
- [x] App-Icon (Alpaca-Silhouette via SkiaSharp, einheitlich auf allen Fenstern).
- [x] **Per-App-Icons** in der Liste (PNG aus hicolor + SVG via `Svg.Skia` für
      KDE-Apps; Sandbox-Cache-Sync von `/var/lib/flatpak/.../hicolor` nach
      `~/.cache/Allpaca/flatpak-system-icons/`).
- [x] **Tastatur-Shortcuts**: Esc (Subfenster), Ctrl+R/F5 (Refresh), Ctrl+F (Suche),
      Ctrl+I (Install), Ctrl+, (Settings).
- [x] **Empty-State-Texte** in der Paketliste (Loading / nichts installiert / kein
      Treffer / Filter zu).
- [x] **„Untrusted-Tap"-Erkennung** im LogWindow (Bazzite ublue-os/tap) mit One-Click
      `brew trust`-Button.
- [x] Resizable für alle Fenster (manueller Edge-Resize in ChromeWindow, weil
      KDE/Wayland-BorderOnly oft keinen treffbaren Griff hat).
- [x] **Toast-Notification** beim Hintergrund-Update-Check (`notify-send` über
      ProcessRunner; nur bei Änderung der Anzahl, kein Spam bei wiederholtem Refresh).
- [x] **Auto-Refresh-Intervall** (Settings-ComboBox Aus/5/15/30/60 Min, DispatcherTimer
      in MainWindowViewModel, persistiert in settings.json).
- [x] **Per-Source-Recovery-Hint** als ToolTip beim „nicht verfügbar"-Text in der
      Sidebar - konkrete Install-Befehle pro Quelle (Bazzite-zentriert).
- [ ] Drag-and-drop AppImages in MainWindow → bewusst skipped (entschieden 2026-06-17:
      Gear Lever deckt's ab).

---

## 7. KI-Integration (Multi-Provider, Gerüst steht)

**Designprinzip: lokal first, aber Multi-Provider.** Die Abstraktion in `Services/Ai`
unterstützt **Ollama, ChatGPT (OpenAI), Claude (Anthropic) und Gemini (Google)**. Ollama ist
der datenschutzfreundliche Default. Implementiert ist das Gerüst (Interface + 4 Provider +
Factory); die UI-Verdrahtung folgt in v3.

### 7.1 Abstraktion (umgesetzt)
```csharp
public interface IAiAssistant
{
    AiProvider Provider { get; }
    bool IsConfigured { get; }
    Task<string> CompleteAsync(string system, string user, CancellationToken ct = default);
}
```
- `AiProvider` { Ollama, OpenAi, Anthropic, Gemini }; `AiSettings` (Provider/Endpoint/Model/ApiKey),
  Defaults in `AiDefaults`. `AiAssistantFactory.Create(settings)` liefert den passenden Client.
- **Ollama + OpenAI** teilen sich `OpenAiCompatibleAssistant` (beide `/chat/completions`).
  **Claude** = native Messages-API, **Gemini** = `:generateContent`.
- **Niemals** das ganze System dumpen: nur Paketnamen/IDs als Kontext, keine personenbezogenen Pfade.
- Streaming ist v3 (aktuell Single-Shot `CompleteAsync`).

### 7.2 Features
1. ✅ **NL-Suche/Empfehlung:** SearchWindow „🤖 Mit KI" — System-Prompt erzwingt
   `PROVIDER|PAKET-ID|BEGRÜNDUNG`-Format, `AiSuggestionParser` mappt auf PackageInfo.
2. ✅ **Aufräum-Analyse:** Eigener Button (🧹) in der MainWindow-Toolbar, CleanupAnalysis-
   Window. `CleanupPromptBuilder` cappt pro Quelle bei 200 Einträgen, lässt Runtimes raus,
   markiert Cross-Source-Duplikate vor. Antwortformat: DUPLIKATE / WAISEN-VERDACHT /
   EVENTUELL ÜBERFLÜSSIG.
3. ✅ **Fehlerdiagnose** aus LogWindow — bei `State=Failed` erscheint 🤖-Sektion mit
   Analysieren-Button. `DiagnosisPromptBuilder` packt die letzten 50 Log-Zeilen (jede
   bis 300 Zeichen), 120 s Hard-Timeout.
4. Paket erklären / Quelle empfehlen für Tool X — folgt, sobald Bedarf da ist.

### 7.3 Architektur-Hinweise
- KI ist **additiv**, nie im kritischen Pfad: fällt der Provider aus, läuft Allpaca normal.
- Tool-/Function-Calling später: KI **schlägt vor**, führt nie selbst aus — Ausführung nur
  mit User-Bestätigung über den normalen v2-Pfad.

---

## 8. Definition of Done (pro Änderung)

1. Version kommt aus dem Git-Tag (MinVer) — kein manuelles Hochzählen mehr.
2. Baut in der `dotnet10`-Distrobox ohne Warnungen (`TreatWarningsAsErrors`);
   `dotnet test` grün; CI nach dem Push grün (`gh run list`).
3. Neue Fenster erben von `ChromeWindow`, `x:DataType` gesetzt.
4. Keine Host-Binary direkt aufgerufen (immer `ProcessRunner`).
5. UI-Collections nur auf dem UI-Thread verändert.
6. Kurz auf dem **Host** gegengetestet (nicht nur in der Box).
7. Neue testbare Logik hat Tests; `.vscode`-Tasks & Release-Action bleiben lauffähig.
8. InfoBox/BMC vorhanden und funktionsfähig.
9. Neue Services über `ServiceRegistration` einhängen, nicht per `new` im ViewModel.
10. Nach dem Push CI prüfen: `gh run list --repo Kroste/Allpaca --limit 3`.
