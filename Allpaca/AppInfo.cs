using System.Reflection;

namespace Allpaca;

/// <summary>
/// Zentrale Stelle für App-Metadaten. Die Version kommt aus dem Assembly und damit
/// aus dem Git-Tag (MinVer) -- nirgends im Code steht eine Versionsnummer.
/// </summary>
public static class AppInfo
{
    public const string Name = "Allpaca";
    public const string RepoOwner = "Kroste";
    public const string RepoName = "Allpaca";
    public const string GithubUrl = $"https://github.com/{RepoOwner}/{RepoName}";
    public const string CoffeeUrl = "https://www.buymeacoffee.com/kroste";

    /// <summary>Anzeigeversion (X.Y.Z), aus der InformationalVersion des Assemblys.</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            // MinVer hängt bei Nicht-Tag-Builds "+<sha>" an -- das interessiert die UI nicht.
            var plus = info.IndexOf('+');
            return plus > 0 ? info[..plus] : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
