using VaKey.Core.Settings;

namespace VaKey.Core.Tests;

public class TelexTests
{
    [Theory]
    // Bảng unit test bắt buộc trong kế hoạch
    [InlineData("Vieetj Nam", "Việt Nam")]
    [InlineData("nguoiwf", "người")]
    [InlineData("dduwowcj", "được")]
    [InlineData("hoaf", "hòa")]
    [InlineData("quar", "quả")]
    [InlineData("giuwowngf", "giường")]
    [InlineData("ass", "as")]
    [InlineData("window", "window")]
    // Dấu mũ, móc, trăng, đ
    [InlineData("aa", "â")]
    [InlineData("aw", "ă")]
    [InlineData("ee", "ê")]
    [InlineData("oo", "ô")]
    [InlineData("ow", "ơ")]
    [InlineData("uw", "ư")]
    [InlineData("w", "ư")]
    [InlineData("dd", "đ")]
    [InlineData("DD", "Đ")]
    [InlineData("Dd", "Đ")]
    // Dấu thanh
    [InlineData("as", "á")]
    [InlineData("af", "à")]
    [InlineData("ar", "ả")]
    [InlineData("ax", "ã")]
    [InlineData("aj", "ạ")]
    [InlineData("asz", "a")]
    [InlineData("asf", "à")]
    // Gõ lặp → trả về ký tự gốc
    [InlineData("aaa", "aa")]
    [InlineData("ddd", "dd")]
    [InlineData("ww", "w")]
    [InlineData("aww", "aw")]
    [InlineData("ooo", "oo")]
    [InlineData("tieengs", "tiếng")]
    // Đặt dấu
    [InlineData("toans", "toán")]
    [InlineData("quyeets", "quyết")]
    [InlineData("gif", "gì")]
    [InlineData("gifn", "gìn")]
    [InlineData("thuyr", "thủy")]
    [InlineData("khoer", "khỏe")]
    [InlineData("ngoaif", "ngoài")]
    [InlineData("khuyur", "khuỷu")]
    [InlineData("ruwowuj", "rượu")]
    [InlineData("cuar", "của")]
    [InlineData("muaw", "mưa")]
    [InlineData("hoawcj", "hoặc")]
    [InlineData("thuowr", "thuở")]
    [InlineData("tuowng", "tương")]
    [InlineData("nhw", "như")]
    [InlineData("khoong", "không")]
    [InlineData("nhuwngx", "những")]
    [InlineData("nghieengs", "nghiếng")]
    [InlineData("quyr", "quỷ")]
    [InlineData("tuaanf", "tuần")]
    [InlineData("chuyeenj", "chuyện")]
    // Dấu tự do: gõ dấu thanh/mũ sau âm cuối
    [InlineData("vietej", "việt")]
    [InlineData("vieejt", "việt")]
    [InlineData("tranaf", "trần")]
    [InlineData("nguoiw", "ngươi")]
    // Chữ hoa
    [InlineData("VIEETJ", "VIỆT")]
    [InlineData("NGUOWIF", "NGƯỜI")]
    [InlineData("Ddaay", "Đây")]
    public void Telex(string keys, string expected) => Assert.Equal(expected, Typist.Type(keys));

    [Theory]
    [InlineData("hoaf", "hoà")]
    [InlineData("thuys", "thuý")]
    [InlineData("khoer", "khoẻ")]
    [InlineData("toans", "toán")]
    [InlineData("cuar", "của")]
    public void OldToneStyle(string keys, string expected) =>
        Assert.Equal(expected, Typist.Type(keys, new EngineOptions { ToneStyle = ToneStyle.Old }));

    [Fact]
    public void WStandaloneCanBeDisabled()
    {
        var options = new EngineOptions { WStandaloneToU = false, SpellCheck = false };
        Assert.Equal("w", Typist.Type("w", options));
        Assert.Equal("tw", Typist.Type("tw", options));
        Assert.Equal("tư", Typist.Type("tuw", options));
    }

    [Fact]
    public void WordBreakResetsBuffer()
    {
        Assert.Equal("a sa", Typist.Type("a sa"));
        Assert.Equal("tôi, bạn.", Typist.Type("tooi, banj."));
    }

    [Fact]
    public void ToneMovesWhenVowelChanges()
    {
        // Gõ dấu thanh trước dấu mũ/móc: dấu thanh tự chuyển sang nguyên âm đúng.
        Assert.Equal("người", Typist.Type("nguoifw"));
        Assert.Equal("việt", Typist.Type("viejte"));
    }
}
