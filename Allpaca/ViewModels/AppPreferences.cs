using Allpaca.Services.Ai;

namespace Allpaca.ViewModels;

/// <summary>
/// Buendelt alle vom SettingsWindow editierbaren Preferences in einem Record.
/// Damit kann OpenSettings die KI-Konfiguration + Allgemeine Settings (z. B.
/// Auto-Refresh) in einem Rutsch reichen, ohne dass MainWindowViewModel und
/// SettingsWindowViewModel mehrere Delegates separat managen muessen.
/// </summary>
public sealed record AppPreferences(AiSettings Ai, int AutoRefreshMinutes);

/// <summary>Ein Auswahl-Preset fuer das Auto-Refresh-Intervall in der SettingsWindow-
/// ComboBox. Minutes=0 bedeutet "aus".</summary>
public sealed record AutoRefreshPreset(int Minutes, string Label);
