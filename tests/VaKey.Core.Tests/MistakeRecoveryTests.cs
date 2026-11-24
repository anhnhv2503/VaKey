using VaKey.Core.Settings;

namespace VaKey.Core.Tests;

public class MistakeRecoveryTests
{
    [Theory]
    // Telex: sai phím dấu ở cuối
    [InlineData("vieetk\bj", "việt")]
    [InlineData("nguoiwq\bf", "người")]
    [InlineData("dduwowcjq\b", "được")]
    [InlineData("hoasq\bf", "hòa")]
    [InlineData("quarq\b", "quả")]
    [InlineData("giuwowngfq\b", "giường")]
    // Gõ lặp phím biến đổi (hoàn tác) rồi sửa
    [InlineData("aaa\ba", "â")]
    [InlineData("aww\bw", "ă")]
    [InlineData("ddd\bd", "đ")]
    [InlineData("vieetjj\bj", "việt")]
    // Chữ hoa
    [InlineData("VIEETK\bJ", "VIỆT")]
    [InlineData("NGUOIWQ\bF", "NGƯỜI")]
    // Xóa cả từ rồi gõ lại
    [InlineData("vieetk\b\b\b\b\b\bvieetj", "việt")]
    public void MistakeThenBackspaceThenContinue(string keys, string expected) =>
        Assert.Equal(expected, Typist.Type(keys));

    [Theory]
    // Từ tiếng Anh không được "hồi sinh" thành tiếng Việt sau khi xóa.
    [InlineData("window\bw", "window")]
    [InlineData("process\bs", "process")]
    [InlineData("class\bs", "class")]
    [InlineData("static\bc", "static")]
    [InlineData("where\be", "where")]
    public void EnglishWordsStayLiteralAfterBackspace(string keys, string expected) =>
        Assert.Equal(expected, Typist.Type(keys));

    [Theory]
    [InlineData("vie6t5k\b1", "viết")]
    [InlineData("nguoi7q\b2", "người")]
    [InlineData("d9u7o7c5q\b", "được")]
    public void VniMistakeThenBackspaceThenContinue(string keys, string expected) =>
        Assert.Equal(expected, Typist.Type(keys, new EngineOptions { Method = InputMethodKind.Vni }));
}
