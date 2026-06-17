using Allpaca.Services.Ai;
using Xunit;

namespace Allpaca.Tests;

public class OllamaPullProgressParserTests
{
    [Fact]
    public void ParseLine_PullingManifest()
    {
        var evt = OllamaPullProgressParser.ParseLine("""{"status":"pulling manifest"}""");
        Assert.NotNull(evt);
        Assert.Equal("pulling manifest", evt!.Status);
        Assert.Null(evt.Completed);
        Assert.Null(evt.Total);
        Assert.False(evt.IsError);
    }

    [Fact]
    public void ParseLine_DownloadingWithProgress()
    {
        var line = """{"status":"downloading","digest":"sha256:abc","total":9000000000,"completed":3500000000}""";
        var evt = OllamaPullProgressParser.ParseLine(line);

        Assert.NotNull(evt);
        Assert.Equal("downloading", evt!.Status);
        Assert.Equal(3500000000L, evt.Completed);
        Assert.Equal(9000000000L, evt.Total);
        Assert.Equal("sha256:abc", evt.Digest);
    }

    [Fact]
    public void ParseLine_Success()
    {
        var evt = OllamaPullProgressParser.ParseLine("""{"status":"success"}""");
        Assert.NotNull(evt);
        Assert.Equal("success", evt!.Status);
    }

    [Fact]
    public void ParseLine_ErrorEvent()
    {
        var evt = OllamaPullProgressParser.ParseLine("""{"error":"pull model manifest: file not found"}""");
        Assert.NotNull(evt);
        Assert.True(evt!.IsError);
        Assert.Equal("error", evt.Status);
        Assert.Equal("pull model manifest: file not found", evt.ErrorMessage);
    }

    [Fact]
    public void ParseLine_ReturnsNull_ForGarbageOrEmpty()
    {
        Assert.Null(OllamaPullProgressParser.ParseLine(""));
        Assert.Null(OllamaPullProgressParser.ParseLine("    "));
        Assert.Null(OllamaPullProgressParser.ParseLine("not json"));
        Assert.Null(OllamaPullProgressParser.ParseLine("{ broken json"));
    }

    [Fact]
    public void ParseLine_HandlesMissingBytesFields()
    {
        // "verifying sha256 digest" hat keine bytes-Felder.
        var evt = OllamaPullProgressParser.ParseLine("""{"status":"verifying sha256 digest"}""");
        Assert.NotNull(evt);
        Assert.Equal("verifying sha256 digest", evt!.Status);
        Assert.Null(evt.Completed);
        Assert.Null(evt.Total);
    }
}
