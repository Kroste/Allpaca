using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Allpaca.Models;
using NLog;

namespace Allpaca.Services;

/// <summary>
/// Zentrale, sandbox-bewusste Prozessausfuehrung. Kapselt die Entscheidung,
/// ob ein Kommando direkt oder via "flatpak-spawn --host" laeuft, sodass
/// alle Quellen denselben Aufrufpfad nutzen.
/// </summary>
public sealed class ProcessRunner
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly bool _wrapHost;

    public ProcessRunner(SandboxDetector sandbox)
    {
        _wrapHost = sandbox.IsSandboxed;
        if (_wrapHost)
            Log.Info("Sandbox erkannt ({0}) – Host-Kommandos via flatpak-spawn --host", sandbox.Kind);
    }

    public sealed record Result(int ExitCode, string StdOut, string StdErr)
    {
        public bool Success => ExitCode == 0;
    }

    /// <summary>Einmalige Ausfuehrung, sammelt stdout/stderr vollstaendig.</summary>
    public async Task<Result> RunAsync(string file, IReadOnlyList<string> args, CancellationToken ct = default)
    {
        using var proc = new Process { StartInfo = BuildStartInfo(file, args) };
        var so = new StringBuilder();
        var se = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) so.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) se.AppendLine(e.Data); };

        try
        {
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Start fehlgeschlagen: {0}", file);
            return new Result(-1, so.ToString(), ex.Message);
        }

        return new Result(proc.ExitCode, so.ToString(), se.ToString());
    }

    /// <summary>Streamt Ausgabe zeilenweise – fuer lange Operationen (v2).</summary>
    public async IAsyncEnumerable<ProgressLine> StreamAsync(
        string file, IReadOnlyList<string> args,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var proc = new Process { StartInfo = BuildStartInfo(file, args) };
        var channel = Channel.CreateUnbounded<ProgressLine>();

        proc.OutputDataReceived += (_, e) => { if (e.Data != null) channel.Writer.TryWrite(new ProgressLine(e.Data, false)); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) channel.Writer.TryWrite(new ProgressLine(e.Data, true)); };
        proc.Exited += (_, _) => channel.Writer.TryComplete();
        proc.EnableRaisingEvents = true;

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await foreach (var line in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            yield return line;

        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
    }

    private ProcessStartInfo BuildStartInfo(string file, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (_wrapHost)
        {
            psi.FileName = "flatpak-spawn";
            psi.ArgumentList.Add("--host");
            psi.ArgumentList.Add(file);
        }
        else
        {
            psi.FileName = file;
        }

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        return psi;
    }
}
