using Allpaca.Services.Ai;
using Xunit;

namespace Allpaca.Tests;

public class OpenAiStreamParserTests
{
    [Fact]
    public void Extract_ReturnsContent_FromDeltaChunk()
    {
        var data = """{"id":"x","choices":[{"index":0,"delta":{"content":"Hallo"},"finish_reason":null}]}""";
        Assert.Equal("Hallo", OpenAiStreamParser.ExtractContent(data));
    }

    [Fact]
    public void Extract_ReturnsNull_ForFirstChunkWithoutContent()
    {
        // Erstes Event hat oft nur "role":"assistant" ohne content.
        var data = """{"choices":[{"index":0,"delta":{"role":"assistant"},"finish_reason":null}]}""";
        Assert.Null(OpenAiStreamParser.ExtractContent(data));
    }

    [Fact]
    public void Extract_ReturnsNull_ForFinishChunk()
    {
        var data = """{"choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}""";
        Assert.Null(OpenAiStreamParser.ExtractContent(data));
    }

    [Fact]
    public void Extract_ReturnsNull_ForGarbageOrEmpty()
    {
        Assert.Null(OpenAiStreamParser.ExtractContent(""));
        Assert.Null(OpenAiStreamParser.ExtractContent("   "));
        Assert.Null(OpenAiStreamParser.ExtractContent("not json"));
        Assert.Null(OpenAiStreamParser.ExtractContent(OpenAiStreamParser.DoneSentinel));
    }
}

public class AnthropicStreamParserTests
{
    [Fact]
    public void Extract_ReturnsText_FromContentBlockDelta()
    {
        var data = """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hallo"}}""";
        Assert.Equal("Hallo", AnthropicStreamParser.ExtractContent(data));
    }

    [Fact]
    public void Extract_SkipsMessageStartEvent()
    {
        var data = """{"type":"message_start","message":{"id":"x","role":"assistant","content":[]}}""";
        Assert.Null(AnthropicStreamParser.ExtractContent(data));
    }

    [Fact]
    public void Extract_SkipsContentBlockStart()
    {
        var data = """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""";
        Assert.Null(AnthropicStreamParser.ExtractContent(data));
    }

    [Fact]
    public void Extract_SkipsNonTextDelta()
    {
        // z. B. tool_use_delta - das wollen wir hier nicht.
        var data = """{"type":"content_block_delta","index":0,"delta":{"type":"tool_use_delta","partial_json":"{}"}}""";
        Assert.Null(AnthropicStreamParser.ExtractContent(data));
    }

    [Fact]
    public void Extract_ReturnsNull_ForGarbageOrEmpty()
    {
        Assert.Null(AnthropicStreamParser.ExtractContent(""));
        Assert.Null(AnthropicStreamParser.ExtractContent("not json"));
    }
}

public class GeminiStreamParserTests
{
    [Fact]
    public void Extract_ReturnsText_FromCandidate()
    {
        var data = """{"candidates":[{"content":{"parts":[{"text":"Hallo"}],"role":"model"}}]}""";
        Assert.Equal("Hallo", GeminiStreamParser.ExtractContent(data));
    }

    [Fact]
    public void Extract_ReturnsNull_ForCandidateWithoutText()
    {
        // Manche Gemini-Frames enthalten nur safetyRatings ohne text.
        var data = """{"candidates":[{"content":{"parts":[],"role":"model"}}]}""";
        Assert.Null(GeminiStreamParser.ExtractContent(data));
    }

    [Fact]
    public void Extract_ReturnsNull_ForGarbageOrEmpty()
    {
        Assert.Null(GeminiStreamParser.ExtractContent(""));
        Assert.Null(GeminiStreamParser.ExtractContent("not json"));
        Assert.Null(GeminiStreamParser.ExtractContent("""{}"""));
    }
}
