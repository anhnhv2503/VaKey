using System.Text;
using VaKey.Core.Data;
using VaKey.Core.Engine;
using VaKey.Core.Settings;

namespace VaKey.Core.Tests;

/// <summary>
/// Mô phỏng một ô soạn thảo: áp dụng EngineResult giống như hook + SendInput thật,
/// đồng thời kiểm tra màn hình luôn khớp với CurrentWord của engine.
/// Ký tự '\b' trong chuỗi phím nghĩa là Backspace.
/// </summary>
internal sealed class Typist
{
    private readonly StringBuilder _screen = new();
    private int _wordStart;

    public Typist(EngineOptions? options = null) => Session = new InputSession(options);

    public InputSession Session { get; }

    public string Screen => _screen.ToString();

    public static string Type(string keys, EngineOptions? options = null)
    {
        var t = new Typist(options);
        t.Keys(keys);
        return t.Screen;
    }

    public Typist Keys(string keys)
    {
        foreach (var k in keys)
        {
            if (k == '\b') Backspace();
            else Key(k);
        }

        return this;
    }

    public void Key(char k)
    {
        var r = Session.Process(k);
        Apply(r, k);
        if (Session.CurrentWord.Length == 0) _wordStart = _screen.Length;
        AssertConsistent();
    }

    public void Backspace()
    {
        var r = Session.Backspace();
        if (!r.SwallowKey)
        {
            if (_screen.Length > 0) _screen.Length--;
        }
        else
        {
            Apply(r, '\0');
        }

        if (Session.CurrentWord.Length == 0) _wordStart = _screen.Length;
        AssertConsistent();
    }

    private void Apply(EngineResult r, char key)
    {
        if (!r.SwallowKey)
        {
            Assert.Equal(0, r.BackspaceCount);
            Assert.Equal("", r.TextToInsert);
            _screen.Append(key);
            return;
        }

        Assert.True(r.BackspaceCount <= _screen.Length, "Backspace vượt quá văn bản");
        _screen.Length -= r.BackspaceCount;
        _screen.Append(r.TextToInsert);
    }

    private void AssertConsistent()
    {
        var word = Session.CurrentWord;
        if (word.Length == 0) return;
        var tail = _screen.ToString(_screen.Length - word.Length, word.Length);
        Assert.Equal(word, tail);
    }
}

/// <summary>Sinh chuỗi phím Telex/VNI chuẩn cho một từ tiếng Việt (để test khứ hồi).</summary>
internal static class KeyEncoder
{
    public static string Telex(string word) => Encode(word, telex: true);

    public static string Vni(string word) => Encode(word, telex: false);

    private static string Encode(string word, bool telex)
    {
        var sb = new StringBuilder();
        var tone = Tone.None;
        foreach (var ch in word)
        {
            VowelTable.TryDecompose(ch, out var b, out var m, out var t, out var upper);
            if (t != Tone.None) tone = t;
            char baseChar = upper ? char.ToUpperInvariant(b) : b;
            sb.Append(baseChar);
            switch (m)
            {
                case Mark.Circumflex: sb.Append(telex ? baseChar : '6'); break;
                case Mark.Breve: sb.Append(telex ? 'w' : '8'); break;
                case Mark.Horn: sb.Append(telex ? 'w' : '7'); break;
                case Mark.Stroke: sb.Append(telex ? baseChar : '9'); break;
            }
        }

        if (tone != Tone.None)
        {
            sb.Append(telex ? "_sfrxj"[(int)tone] : (char)('0' + (int)tone));
        }

        return sb.ToString();
    }
}

internal static class TestData
{
    public static string PathOf(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "Data", name);

    public static IEnumerable<string> Lines(string name) =>
        File.ReadLines(PathOf(name), Encoding.UTF8)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'));

    public static IEnumerable<string> Words(string name) =>
        Lines(name).SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
