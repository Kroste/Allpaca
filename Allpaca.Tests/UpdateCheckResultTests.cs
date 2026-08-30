using Allpaca.Services;
using Xunit;

namespace Allpaca.Tests;

public class UpdateCheckResultTests
{
    private static readonly UpdateRelease Release =
        new("v1.7.0", "1.7.0", "https://example.invalid", []);

    [Fact]
    public void Available_meldet_Update_und_keinen_Fehler()
    {
        var r = UpdateCheckResult.Available(Release);

        Assert.True(r.HasUpdate);
        Assert.False(r.IsError);
        Assert.Equal("1.7.0", r.Release!.Version);
    }

    [Fact]
    public void UpToDate_meldet_weder_Update_noch_Fehler()
    {
        var r = UpdateCheckResult.UpToDate();

        Assert.False(r.HasUpdate);
        Assert.False(r.IsError);
    }

    [Fact]
    public void Failed_ist_unterscheidbar_von_UpToDate()
    {
        var r = UpdateCheckResult.Failed("kein Netz");

        // Genau darum geht es: ohne diese Trennung meldet die UI bei jedem
        // fehlgeschlagenen Check "du bist aktuell", obwohl nichts geprüft wurde.
        Assert.False(r.HasUpdate);
        Assert.True(r.IsError);
        Assert.Equal("kein Netz", r.Error);
    }
}
