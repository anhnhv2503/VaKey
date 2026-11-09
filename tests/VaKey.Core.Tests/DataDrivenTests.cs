using System.Diagnostics;
using VaKey.Core.Data;
using VaKey.Core.Engine;
using VaKey.Core.Settings;

namespace VaKey.Core.Tests;

public class DataDrivenTests
{
    private static readonly EngineOptions Vni = new() { Method = InputMethodKind.Vni };

    public static IEnumerable<object[]> CsvCases(string file) =>
        TestData.Lines(file).Select(l =>
        {
            var parts = l.Split(',', 2);
            return new object[] { parts[0], parts[1] };
        });

    public static IEnumerable<object[]> VietnameseWords() =>
        TestData.Words("vietnamese-words.txt").Distinct().Select(w => new object[] { w });

    [Theory]
    [MemberData(nameof(CsvCases), "telex.csv")]
    public void TelexCsv(string keys, string expected) => Assert.Equal(expected, Typist.Type(keys));

    [Theory]
    [MemberData(nameof(CsvCases), "vni.csv")]
    public void VniCsv(string keys, string expected) => Assert.Equal(expected, Typist.Type(keys, Vni));

    [Theory]
    [MemberData(nameof(VietnameseWords))]
    public void TelexRoundTrip(string word) => Assert.Equal(word, Typist.Type(KeyEncoder.Telex(word)));

    [Theory]
    [MemberData(nameof(VietnameseWords))]
    public void VniRoundTrip(string word) => Assert.Equal(word, Typist.Type(KeyEncoder.Vni(word), Vni));

    [Theory]
    [MemberData(nameof(VietnameseWords))]
    public void UppercaseRoundTrip(string word)
    {
        var upper = word.ToUpperInvariant();
        Assert.Equal(upper, Typist.Type(KeyEncoder.Telex(upper)));
    }

    /// <summary>Gõ rồi xóa từng ký tự: màn hình luôn khớp engine và cuối cùng về rỗng.</summary>
    [Theory]
    [MemberData(nameof(VietnameseWords))]
    public void TypeThenDeleteAll(string word)
    {
        var t = new Typist().Keys(KeyEncoder.Telex(word));
        for (int i = word.Length; i > 0; i--)
        {
            Assert.Equal(i, t.Screen.Length);
            t.Backspace();
        }

        Assert.Equal("", t.Screen);
    }

    [Fact]
    public void EveryVowelFormRoundTrips()
    {
        foreach (var b in "aeiouy")
        foreach (var m in Enum.GetValues<Mark>())
        foreach (var tone in Enum.GetValues<Tone>())
        foreach (var up in new[] { false, true })
        {
            if (!VowelTable.CanTakeMark(b, m) || m == Mark.Stroke) continue;
            var c = VowelTable.Compose(b, m, tone, up);
            Assert.True(VowelTable.TryDecompose(c, out var b2, out var m2, out var t2, out var up2));
            Assert.Equal((b, m, tone, up), (b2, m2, t2, up2));
            Assert.True(c.ToString().IsNormalized(System.Text.NormalizationForm.FormC));
        }
    }

    [Fact]
    public void ProcessingIsFast()
    {
        var words = TestData.Words("vietnamese-words.txt").Select(KeyEncoder.Telex).ToArray();
        var session = new InputSession();
        int keys = 0;
        var sw = Stopwatch.StartNew();
        for (int round = 0; round < 20; round++)
        {
            foreach (var w in words)
            {
                foreach (var k in w)
                {
                    session.Process(k);
                    keys++;
                }

                session.Process(' ');
            }
        }

        sw.Stop();
        double perKeyMicros = sw.Elapsed.TotalMilliseconds * 1000 / keys;
        Assert.True(perKeyMicros < 1000, $"{perKeyMicros:F2} µs/phím");
    }
}
