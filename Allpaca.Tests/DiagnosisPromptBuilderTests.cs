using Allpaca.Models;
using Allpaca.Services.Ai;
using Xunit;

namespace Allpaca.Tests;

public class DiagnosisPromptBuilderTests
{
    [Fact]
    public void BuildUserPrompt_IncludesTitleAndExitCode()
    {
        var prompt = DiagnosisPromptBuilder.BuildUserPrompt(
            "Flatpak: foo deinstallieren", exitCode: 1,
            new List<ProgressLine> { new("Error: something", IsError: true) });

        Assert.Contains("Operation: Flatpak: foo deinstallieren", prompt);
        Assert.Contains("Exit-Code: 1", prompt);
        Assert.Contains("[ERR] Error: something", prompt);
    }

    [Fact]
    public void BuildUserPrompt_OmitsExitCode_WhenNull()
    {
        var prompt = DiagnosisPromptBuilder.BuildUserPrompt(
            "foo", exitCode: null,
            new List<ProgressLine> { new("hello", IsError: false) });

        Assert.Contains("Operation: foo", prompt);
        Assert.DoesNotContain("Exit-Code", prompt);
    }

    [Fact]
    public void BuildUserPrompt_TailsLongLogs()
    {
        var lines = new List<ProgressLine>();
        for (int i = 0; i < 200; i++)
            lines.Add(new ProgressLine($"line-{i}", IsError: false));

        var prompt = DiagnosisPromptBuilder.BuildUserPrompt("op", null, lines);

        // Letzte 50 müssen drin sein (line-150..line-199), die ersten nicht.
        Assert.Contains("line-199", prompt);
        Assert.Contains("line-150", prompt);
        Assert.DoesNotContain("line-149", prompt);
        Assert.DoesNotContain("line-0\n", prompt);
        Assert.Contains("frühere Zeilen ausgelassen", prompt);
    }

    [Fact]
    public void BuildUserPrompt_TruncatesLongIndividualLines()
    {
        var longText = new string('x', 500);
        var lines = new List<ProgressLine> { new(longText, IsError: false) };

        var prompt = DiagnosisPromptBuilder.BuildUserPrompt("op", null, lines);

        // Sollte gekürzt sein - 300 chars + Ellipsis.
        Assert.Contains(new string('x', 300) + "…", prompt);
        Assert.DoesNotContain(new string('x', 301) + "x", prompt);
    }

    [Fact]
    public void BuildUserPrompt_MarksErrorAndNormalLines()
    {
        var lines = new List<ProgressLine>
        {
            new("normal output", IsError: false),
            new("bad stderr", IsError: true),
        };

        var prompt = DiagnosisPromptBuilder.BuildUserPrompt("op", null, lines);

        Assert.Contains("[ERR] bad stderr", prompt);
        Assert.Contains("      normal output", prompt);
    }
}
