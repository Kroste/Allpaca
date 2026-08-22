using Allpaca.Logging;
using Xunit;

namespace Allpaca.Tests;

public class MaskingLayoutRendererTests
{
    [Theory]
    [InlineData("Key sk-ant-api03-AAAABBBBCCCCDDDD gesetzt")]
    [InlineData("Bearer-Key sk-proj-AAAABBBBCCCCDDDDEEEE")]
    [InlineData("Google-Key AIzaSyA-BBBBCCCCDDDDEEEEFFFF")]
    public void Provider_Keyformate_werden_maskiert(string input)
    {
        var masked = MaskingLayoutRenderer.Mask_(input);

        Assert.DoesNotContain("sk-ant-api03", masked);
        Assert.DoesNotContain("sk-proj", masked);
        Assert.DoesNotContain("AIzaSy", masked);
        Assert.Contains("***", masked);
    }

    [Theory]
    [InlineData("api_key=supergeheim123", "api_key=")]
    [InlineData("x-api-key: supergeheim123", "x-api-key: ")]
    [InlineData("Authorization: Bearer supergeheim123", "Authorization: Bearer ")]
    [InlineData("?key=supergeheim123", "?key=")]
    [InlineData("password: supergeheim123", "password: ")]
    public void Header_und_Query_Formen_behalten_den_Praefix(string input, string keptPrefix)
    {
        var masked = MaskingLayoutRenderer.Mask_(input);

        Assert.DoesNotContain("supergeheim123", masked);
        Assert.Contains(keptPrefix, masked);
        Assert.Contains("***", masked);
    }

    [Fact]
    public void Harmlose_Meldungen_bleiben_unveraendert()
    {
        const string msg = "Flatpak: 42 Einträge geladen in 130 ms";

        Assert.Equal(msg, MaskingLayoutRenderer.Mask_(msg));
    }

    [Fact]
    public void Leere_Eingabe_faellt_nicht_um()
    {
        Assert.Equal(string.Empty, MaskingLayoutRenderer.Mask_(null));
        Assert.Equal(string.Empty, MaskingLayoutRenderer.Mask_(""));
    }
}
