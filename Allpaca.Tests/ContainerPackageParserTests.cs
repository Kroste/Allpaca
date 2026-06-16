using Allpaca.Services.Sources;
using Xunit;

namespace Allpaca.Tests;

public class ContainerPackageParserTests
{
    [Fact]
    public void Parse_DpkgStyleOutput()
    {
        // dpkg-query -W -f='${Package}\t${Version}\n'
        var input = "bash\t5.2.21-2ubuntu4\nlibc6\t2.39-0ubuntu8.1\nzlib1g\t1:1.3.dfsg-3.1ubuntu2";
        var result = ContainerPackageParser.Parse(input);

        Assert.Equal(3, result.Count);
        Assert.Equal("bash", result[0].Name);
        Assert.Equal("5.2.21-2ubuntu4", result[0].Version);
        Assert.Equal("zlib1g", result[2].Name);
        Assert.Equal("1:1.3.dfsg-3.1ubuntu2", result[2].Version);
    }

    [Fact]
    public void Parse_RpmStyleOutput()
    {
        // rpm -qa --queryformat '%{NAME}\t%{VERSION}-%{RELEASE}\n'
        var input = "bash\t5.2.32-1.fc41\ndnf\t4.21.0-1.fc41\n";
        var result = ContainerPackageParser.Parse(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("dnf", result[1].Name);
        Assert.Equal("4.21.0-1.fc41", result[1].Version);
    }

    [Fact]
    public void Parse_HandlesEmptyAndWhitespace()
    {
        Assert.Empty(ContainerPackageParser.Parse(""));
        Assert.Empty(ContainerPackageParser.Parse("   "));
        Assert.Empty(ContainerPackageParser.Parse("\n\n\n"));
    }

    [Fact]
    public void Parse_TrimsAndSkipsEmptyNames()
    {
        var input = "  bash  \t  5.2  \n\t1.0\nfoo\nbar\t2.0";
        var result = ContainerPackageParser.Parse(input);

        Assert.Equal(3, result.Count);
        Assert.Equal("bash", result[0].Name);
        Assert.Equal("5.2", result[0].Version);
        Assert.Equal("foo", result[1].Name);
        Assert.Equal("", result[1].Version);  // keine Versionsspalte
        Assert.Equal("bar", result[2].Name);
        Assert.Equal("2.0", result[2].Version);
    }

    [Fact]
    public void Parse_StripsCarriageReturns()
    {
        var input = "bash\t5.2\r\nlibc6\t2.39\r\n";
        var result = ContainerPackageParser.Parse(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("5.2", result[0].Version);
        Assert.Equal("2.39", result[1].Version);
    }
}
