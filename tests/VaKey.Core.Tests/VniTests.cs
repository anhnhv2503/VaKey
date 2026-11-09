using VaKey.Core.Settings;

namespace VaKey.Core.Tests;

public class VniTests
{
    private static readonly EngineOptions Vni = new() { Method = InputMethodKind.Vni };

    [Theory]
    [InlineData("Vie65t Nam", "Việt Nam")]
    [InlineData("Viet65 Nam", "Việt Nam")]
    [InlineData("nguoi72", "người")]
    [InlineData("d9uoc75", "được")]
    [InlineData("hoa2", "hòa")]
    [InlineData("qua3", "quả")]
    [InlineData("giuong72", "giường")]
    [InlineData("a1", "á")]
    [InlineData("a2", "à")]
    [InlineData("a3", "ả")]
    [InlineData("a4", "ã")]
    [InlineData("a5", "ạ")]
    [InlineData("a10", "a")]
    [InlineData("a6", "â")]
    [InlineData("e6", "ê")]
    [InlineData("o6", "ô")]
    [InlineData("o7", "ơ")]
    [InlineData("u7", "ư")]
    [InlineData("a8", "ă")]
    [InlineData("d9", "đ")]
    [InlineData("a11", "a1")]
    [InlineData("a66", "a6")]
    [InlineData("d99", "d9")]
    [InlineData("tuye6t1", "tuyết")]
    [InlineData("thuo73", "thuở")]
    [InlineData("hoa8c5", "hoặc")]
    [InlineData("2024", "2024")]
    [InlineData("x2", "x2")]
    public void Vni_(string keys, string expected) => Assert.Equal(expected, Typist.Type(keys, Vni));

    [Fact]
    public void TelexKeysAreLiteralInVni()
    {
        Assert.Equal("as", Typist.Type("as", Vni));
        Assert.Equal("dd", Typist.Type("dd", Vni));
    }
}
