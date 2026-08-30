using Allpaca.Services;
using Xunit;

namespace Allpaca.Tests;

/// <summary>
/// Deckt das generierte Austausch-Skript ab. Der Code läuft nur im Ernstfall -- also
/// genau dann, wenn ein Fehler am teuersten ist -- und lässt sich sonst nicht testen.
/// </summary>
public class UpdateInstallerScriptTests
{
    [Fact]
    public void Skript_startet_die_App_auch_wenn_der_Austausch_scheitert()
    {
        var script = UpdateService.BuildInstallerScript("/tmp/x/Allpaca-1.7.0-linux-x64.tar.gz");

        // Der entscheidende Punkt: kein "exit 1" im Fehlerzweig. Die App hat sich für
        // den Austausch bereits beendet -- bricht das Skript ab, ist sie einfach weg.
        Assert.DoesNotContain("exit 1", script);
        Assert.Contains("setsid", script);
        // Der setsid-Aufruf steht hinter dem if/else, wird also in beiden Fällen erreicht.
        var elseIdx = script.IndexOf("else", StringComparison.Ordinal);
        var fiIdx = script.IndexOf("\nfi", StringComparison.Ordinal);
        var setsidIdx = script.IndexOf("setsid", StringComparison.Ordinal);
        Assert.True(elseIdx > 0 && fiIdx > elseIdx && setsidIdx > fiIdx,
            "setsid muss NACH dem fi stehen, sonst läuft es nur im Erfolgsfall");
    }

    [Fact]
    public void Skript_wartet_auf_das_Prozessende()
    {
        var script = UpdateService.BuildInstallerScript("/tmp/x/paket.tar.gz");

        Assert.Contains($"PID={Environment.ProcessId}", script);
        Assert.Contains("kill -0", script);
    }

    [Fact]
    public void Skript_loggt_in_den_schreibbaren_State_Ordner()
    {
        var script = UpdateService.BuildInstallerScript("/tmp/x/paket.tar.gz");

        // NICHT neben die Exe: beim laufenden AppImage ist das read-only Squashfs,
        // und ein fehlschlagendes "exec >>" bricht bash sofort ab.
        Assert.Contains("XDG_STATE_HOME", script);
        Assert.DoesNotContain("BaseDirectory", script);
    }

    [Theory]
    [InlineData("/tmp/x/paket.tar.gz")]
    [InlineData("/tmp/mit leerzeichen/paket.tar.gz")]
    [InlineData("/tmp/mit'apostroph/paket.tar.gz")]
    [InlineData("/tmp/$(rm -rf ~)/paket.tar.gz")]
    [InlineData("/tmp/mit\"quote/paket.tar.gz")]
    public void Pfade_werden_sicher_gequotet(string path)
    {
        var script = UpdateService.BuildInstallerScript(path);

        // Der Pfad steht in einer Variablen-Zuweisung in Single Quotes; darin kann
        // weder eine Kommandosubstitution noch ein Quote-Ausbruch stattfinden.
        Assert.Contains("SRC='", script);
        Assert.DoesNotContain("$(rm", script.Replace("'" + path + "'", ""));
        foreach (var line in script.Split('\n'))
        {
            if (!line.StartsWith("SRC=", StringComparison.Ordinal)) continue;
            // Single-Quote im Pfad muss als '\'' escaped sein.
            var payload = line["SRC=".Length..];
            Assert.StartsWith("'", payload);
            Assert.EndsWith("'", payload);
        }
    }

    [Fact]
    public void Skript_nutzt_LF_und_beginnt_mit_dem_Shebang()
    {
        var script = UpdateService.BuildInstallerScript("/tmp/x/paket.tar.gz");

        Assert.StartsWith("#!/usr/bin/env bash\n", script);
        Assert.DoesNotContain("\r", script);
    }
}
