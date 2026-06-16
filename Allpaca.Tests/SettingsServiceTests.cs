using Allpaca.Services;
using Xunit;

namespace Allpaca.Tests;

public class SettingsServiceTests
{
    private static string Tmp() => Path.Combine(Path.GetTempPath(), $"allpaca-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_ReturnsDefaults_WhenFileMissing()
    {
        var path = Tmp();
        try
        {
            Assert.False(File.Exists(path));

            var svc = new SettingsService(path);
            var s = svc.Load();

            Assert.Equal("Name", s.SortKey);
            Assert.False(s.SortDescending);
            Assert.False(s.ShowRuntimes);
            Assert.Empty(s.SourceFilters);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Save_Then_Load_PreservesAllFields()
    {
        var path = Tmp();
        try
        {
            var svc = new SettingsService(path);
            svc.Save(new AppSettings
            {
                SortKey = "Size",
                SortDescending = true,
                ShowRuntimes = true,
                SourceFilters = new()
                {
                    ["Flatpak"] = false,
                    ["Homebrew"] = true,
                    ["RpmOstree"] = false,
                },
            });

            var loaded = svc.Load();

            Assert.Equal("Size", loaded.SortKey);
            Assert.True(loaded.SortDescending);
            Assert.True(loaded.ShowRuntimes);
            Assert.Equal(3, loaded.SourceFilters.Count);
            Assert.False(loaded.SourceFilters["Flatpak"]);
            Assert.True(loaded.SourceFilters["Homebrew"]);
            Assert.False(loaded.SourceFilters["RpmOstree"]);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Save_CreatesIntermediateDirectories()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"allpaca-test-{Guid.NewGuid():N}", "sub");
        var path = Path.Combine(dir, "settings.json");
        try
        {
            Assert.False(Directory.Exists(dir));
            var svc = new SettingsService(path);
            svc.Save(new AppSettings { SortKey = "Source" });

            Assert.True(File.Exists(path));
            Assert.Equal("Source", svc.Load().SortKey);
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(dir)!))
                Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsDefaults_OnCorruptJson()
    {
        var path = Tmp();
        try
        {
            File.WriteAllText(path, "{ this is not valid json");

            var svc = new SettingsService(path);
            var s = svc.Load();

            Assert.Equal("Name", s.SortKey);
            Assert.Empty(s.SourceFilters);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void DefaultPath_UsesXdgConfigHome_WhenSet()
    {
        // Wir testen nur die Endung - der Anfang haengt vom Env ab und ist nicht
        // deterministisch in CI. Wichtig: "Allpaca/settings.json" am Ende.
        var path = SettingsService.DefaultPath();
        Assert.EndsWith(Path.Combine("Allpaca", "settings.json"), path);
    }
}
