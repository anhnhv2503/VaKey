using VaKey.Core.Data;
using VaKey.Core.Settings;
using VaKey.Core.Validation;

namespace VaKey.Core.Tests;

public class SpellCheckTests
{
    public static IEnumerable<object[]> EnglishWords() =>
        TestData.Words("english-words.txt").Select(w => new object[] { w });

    [Theory]
    [MemberData(nameof(EnglishWords))]
    public void EnglishWordIsNotTransformed(string word) => Assert.Equal(word, Typist.Type(word));

    [Fact]
    public void CollidingEnglishWordsNeedExceptionList()
    {
        var words = TestData.Words("english-collisions.txt").ToArray();
        var options = new EngineOptions { EnglishExceptions = words };
        foreach (var w in words)
        {
            Assert.NotEqual(w, Typist.Type(w));          // tự động: bị biến đổi (giống Unikey)
            Assert.Equal(w, Typist.Type(w, options));    // có ngoại lệ: giữ nguyên
        }
    }

    [Fact]
    public void SpellCheckOffTransformsEverything()
    {
        var options = new EngineOptions { SpellCheck = false };
        Assert.NotEqual("window", Typist.Type("window", options));
    }

    [Theory]
    [InlineData("vi", true)]
    [InlineData("ngh", true)]
    [InlineData("q", true)]
    [InlineData("qu", true)]
    [InlineData("cl", false)]
    [InlineData("str", false)]
    [InlineData("ki", true)]
    [InlineData("ka", false)]
    [InlineData("ce", false)]
    [InlineData("ge", false)]
    [InlineData("ghe", true)]
    [InlineData("nge", false)]
    [InlineData("giêng", true)]
    [InlineData("quynh", true)]
    [InlineData("ưi", true)]
    [InlineData("ưin", false)]
    [InlineData("uo", true)]
    [InlineData("bana", false)]
    [InlineData("x2", false)]
    public void ValidPrefix(string text, bool expected)
    {
        var chars = text.Select(c =>
        {
            VowelTable.TryDecompose(c, out var b, out var m, out _, out var up);
            return new VChar(b, m, up);
        }).ToArray();
        Assert.Equal(expected, SyllableValidator.IsValidPrefix(chars, Tone.None));
    }

    [Fact]
    public void StopFinalOnlyTakesAcuteOrDot()
    {
        var chars = new[] { new VChar('m'), new VChar('a'), new VChar('t') };
        Assert.True(SyllableValidator.IsValidPrefix(chars, Tone.Acute));
        Assert.True(SyllableValidator.IsValidPrefix(chars, Tone.Dot));
        Assert.False(SyllableValidator.IsValidPrefix(chars, Tone.Grave));
        Assert.Equal("matf", Typist.Type("matf"));
    }

    [Fact]
    public void KnownRhymesAreExposed() => Assert.Contains("ương", SyllableValidator.KnownRhymes);
}
