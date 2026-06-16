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
- **Bei JEDER Änderung `<Version>` in `Allpaca.csproj` erhöhen.** Nicht vergessen.
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
- **Tests:** immer ein eigenes Testprojekt (`tests/Allpaca.Tests`, xUnit). Reine Logik
  (Parser, Defaults) wird per `InternalsVisibleTo` testbar gemacht.
- **Release:** GitHub-Action (`.github/workflows/release.yml`), die auf Tag `v*.*.*` fertige
  Pakete für **Windows (ZIP)**, **Linux (tar.gz)** und **AppImage** baut. **Node 24** im
  Linux-Job. Auslösung bequem über den VS-Code-Task `release (tag + push)` bzw.
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

### 3.10 InitializeComponent / XAML-Loader
- In **Views**: `public MyWindow() { InitializeComponent(); }` — die Methode wird vom
  Source-Generator erzeugt, **nicht** selbst definieren.
- Nur in **`App.axaml.cs`**: `AvaloniaXamlLoader.Load(this)`.

### 3.11 Fonts
- `WithInterFont()` in `Program.cs` braucht das Paket **`Avalonia.Fonts.Inter`** — sonst
  Build-Fehler. (Schon referenziert.)

### 3.12 DataGrid (falls je gebraucht)
- Eigenes Paket `Avalonia.Controls.DataGrid` **plus** Theme-Include:
  `<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"/>`.
- Für Allpaca v1 bewusst **kein** DataGrid — gestyltes `ListBox` reicht und sieht sauberer aus.

---

## 4. Projektarchitektur

```
IPackageSource ── FlatpakSource     flatpak list --columns=…
              ├─ HomebrewSource     brew info --json=v2 --installed
              ├─ RpmOstreeSource    rpm-ostree status --json
              ├─ DistroboxSource    distrobox list --no-color
              └─ AppImageSource     .desktop-Scan + Dateisystem

ProcessRunner   ── zentrale, sandbox-bewusste Prozessausführung
SandboxDetector ── erkennt Flatpak/Container → flatpak-spawn --host
PackageAggregator ── lädt jede Quelle parallel & fehlertolerant
MainWindowViewModel ── Filter, Suche, Live-Befüllung
ChromeWindow ── Fenster-Basis (Chrome + Auflösungs-Clamp)
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

dotnet test                       # Testprojekt (xUnit)
bash scripts/release.sh           # taggt vX.Y.Z + push → löst Release-Action aus
```

VS-Code-Tasks: `build` (Default), `test`, `clean`, `clean-hard`, `rebuild`, `release (tag + push)`.

- **Avalonia-Version** in `Allpaca.csproj` steht auf `12.0.0` — auf die exakte 12.x angleichen,
  die Magnat/NetScanner nutzen, falls abweichend.
- **Zum echten Test auf dem Host starten** (oder `distrobox-host-exec`). In der Distrobox
  greift zwar der Host-Wrapper, aber das AppImage-Dateisystem-Scanning sieht dann nur das
  geteilte Home.
- Logs: `~/.local/share/Allpaca/logs/` bzw. `${ApplicationData}/Allpaca/logs/`.

---

## 6. Roadmap

### v1.x — Inventar-Feinschliff
- [ ] Leere Detailfelder ausblenden (Converter `StringConverters.IsNotNullOrEmpty`).
- [ ] Spaltensortierung (Name / Größe / Quelle) + Sortier-Umschalter.
- [ ] Flatpak-**Runtimes** optional einblenden (Toggle), aktuell nur `--app`.
- [ ] Distrobox-**Status** farbig (running/created/exited).
- [ ] Duplikat-Hinweis (gleiche App als Flatpak *und* AppImage).

### v2 — Verwaltung
- [ ] Install/Uninstall/Update je Quelle mit **Live-Log-Fenster** (nutzt `StreamAsync` →
      `IAsyncEnumerable<ProgressLine>`, schon vorhanden).
- [ ] **`pkexec`-Flow** für rpm-ostree inkl. **Reboot-Hinweis** (Caps: `RequiresReboot`).
- [ ] Bestätigungsdialog vor destruktiven Aktionen.
- [ ] **Distrobox-Drill-down:** Pakete *innerhalb* eines Containers (enter + pm list).
- [ ] Fehler-Handling: nicht-null Exit-Codes sauber im Log-Fenster zeigen.

### v2.x — Komfort
- [ ] **Update-Check:** `brew outdated --json`, `flatpak remote-ls --updates`,
      `rpm-ostree upgrade --check`. Badge „Updates verfügbar".
- [ ] **Mehrfachauswahl + Batch** (mehrere deinstallieren/aktualisieren).
- [ ] Settings-Persistenz (Theme, Ollama-Endpoint, Quellen-Defaults).

### v3 — KI-Unterstützung  (§7)
- [x] Multi-Provider-Gerüst: `IAiAssistant` + Ollama/OpenAI/Claude/Gemini + Factory.
- [ ] UI-Verdrahtung + Settings (Provider/Endpoint/Key) + Streaming.
- [ ] Natürlichsprachige Suche/Empfehlung.
- [ ] Aufräum-Analyse (Waisen/Duplikate/„brauchst du das noch?").
- [ ] Fehlerdiagnose aus Operation-Logs.

### Später — weitere Quellen (optional)
- [ ] `pipx`, `cargo install`, `npm -g`, `toolbx` (parallel zu Distrobox).

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

### 7.2 Features (von einfach nach komplex) — v3
1. **NL-Suche/Empfehlung:** „Tool zum Videoschneiden" → Paket + passende Quelle (Flatpak vs.
   Distrobox vs. brew) mit Begründung.
2. **Paket erklären** (Kontext = installierte Liste).
3. **Quelle empfehlen** für Tool X unter Bazzite (immutable beachten!).
4. **Aufräum-Analyse:** Duplikate/Waisen über Quellen hinweg.
5. **Fehlerdiagnose** aus v2-Operation-Logs.

### 7.3 Architektur-Hinweise
- KI ist **additiv**, nie im kritischen Pfad: fällt der Provider aus, läuft Allpaca normal.
- Tool-/Function-Calling später: KI **schlägt vor**, führt nie selbst aus — Ausführung nur
  mit User-Bestätigung über den normalen v2-Pfad.

---

## 8. Definition of Done (pro Änderung)

1. `<Version>` erhöht.
2. Baut in der `dotnet10`-Distrobox ohne Warnungen; `dotnet test` grün.
3. Neue Fenster erben von `ChromeWindow`, `x:DataType` gesetzt.
4. Keine Host-Binary direkt aufgerufen (immer `ProcessRunner`).
5. UI-Collections nur auf dem UI-Thread verändert.
6. Kurz auf dem **Host** gegengetestet (nicht nur in der Box).
7. Neue testbare Logik hat Tests; `.vscode`-Tasks & Release-Action bleiben lauffähig.
8. InfoBox/BMC vorhanden und funktionsfähig.
