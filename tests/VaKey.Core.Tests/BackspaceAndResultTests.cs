using VaKey.Core.Engine;
using VaKey.Core.Settings;

namespace VaKey.Core.Tests;

public class BackspaceAndResultTests
{
    [Fact]
    public void BackspaceRestoresPreviousWord()
    {
        var t = new Typist().Keys("vieet");
        Assert.Equal("viêt", t.Screen);
        t.Backspace();
        Assert.Equal("viê", t.Screen);
        Assert.Equal("viê", t.Session.CurrentWord);
        t.Keys("ns");
        Assert.Equal("viến", t.Screen);
    }

    [Fact]
    public void BackspaceMovesToneToCorrectVowel()
    {
        var t = new Typist().Keys("hoans");
        Assert.Equal("hoán", t.Screen);
        t.Backspace();
        Assert.Equal("hóa", t.Screen); // kiểu mới: không có âm cuối → dấu lên o
    }

    [Fact]
    public void BackspaceAllClearsWord()
    {
        var t = new Typist().Keys("dduwowcj");
        for (int i = 0; i < 4; i++) t.Backspace();
        Assert.Equal("", t.Screen);
        Assert.Equal("", t.Session.CurrentWord);
        t.Backspace(); // buffer rỗng → chuyển tiếp phím
        t.Keys("ddi");
        Assert.Equal("đi", t.Screen);
    }

    [Fact]
    public void BackspaceRemovesToneWhenNoVowelLeft()
    {
        var t = new Typist().Keys("bas");
        t.Backspace();
        t.Keys("i");
        Assert.Equal("bi", t.Screen);
    }

    [Fact]
    public void PlainLettersPassThrough()
    {
        var s = new InputSession();
        Assert.False(s.Process('b').SwallowKey);
        Assert.False(s.Process('a').SwallowKey);
    }

    [Fact]
    public void ResultIsMinimal()
    {
        var s = new InputSession();
        s.Process('v');
        s.Process('i');
        s.Process('e');
        var r = s.Process('e');
        Assert.True(r.SwallowKey);
        Assert.Equal(1, r.BackspaceCount);
        Assert.Equal("ê", r.TextToInsert);

        s.Process('t');
        r = s.Process('j');
        Assert.Equal(2, r.BackspaceCount);
        Assert.Equal("ệt", r.TextToInsert);
    }

    [Fact]
    public void NonWordKeyResets()
    {
        var s = new InputSession();
        s.Process('a');
        Assert.False(s.Process(' ').SwallowKey);
        Assert.Equal("", s.CurrentWord);
        Assert.False(s.Process('s').SwallowKey);
    }

    [Fact]
    public void ChangingOptionsResets()
    {
        var s = new InputSession();
        s.Process('a');
        s.Options = new EngineOptions { Method = InputMethodKind.Vni };
        Assert.Equal("", s.CurrentWord);
        Assert.IsType<VniMethod>(s.Method);
    }

    [Fact]
    public void EnglishExceptionsAreNotTransformed()
    {
        var options = new EngineOptions { EnglishExceptions = ["tax", "If"] };
        Assert.Equal("tax", Typist.Type("tax", options));
        Assert.Equal("if", Typist.Type("if", options));
        Assert.Equal("tã", Typist.Type("tax"));
        Assert.Equal("tá", Typist.Type("tas", options));
    }

    [Fact]
    public void BackspaceAfterSpellCheckFailureAllowsRetry()
    {
        // Gõ sai phím dấu (k thay vì j) → từ bị trả về chuỗi gốc và tắt biến đổi.
        var t = new Typist().Keys("vieetk");
        Assert.Equal("vieetk", t.Screen);

        // Chỉ cần xóa phím sai rồi gõ lại, không phải xóa cả từ.
        t.Backspace();
        Assert.Equal("viêt", t.Screen);
        t.Keys("j");
        Assert.Equal("việt", t.Screen);
    }

    [Fact]
    public void BackspaceAfterInvalidExtraKeyReplaysRemainingKeys()
    {
        var t = new Typist().Keys("nguoiw");
        Assert.Equal("ngươi", t.Screen);

        // Thêm 'q' làm từ sai chính tả → quay về chuỗi phím gốc.
        t.Keys("q");
        Assert.Equal("nguoiwq", t.Screen);

        // Xóa 'q': phần còn lại được dựng lại thành tiếng Việt để gõ tiếp.
        t.Backspace();
        Assert.Equal("ngươi", t.Screen);
        t.Keys("f");
        Assert.Equal("người", t.Screen);
    }

    [Fact]
    public void BackspaceAfterInvalidExtraKeyWorksWithVni()
    {
        var options = new EngineOptions { Method = InputMethodKind.Vni };
        var t = new Typist(options).Keys("vie6t5");
        Assert.Equal("việt", t.Screen);

        t.Keys("k");
        Assert.Equal("vie6t5k", t.Screen);

        t.Backspace();
        Assert.Equal("việt", t.Screen);
        t.Keys("1");
        Assert.Equal("viết", t.Screen);
    }

    [Fact]
    public void BackspaceAfterInvalidExtraKeyPreservesUppercase()
    {
        var t = new Typist().Keys("VIEETK");
        Assert.Equal("VIEETK", t.Screen);

        t.Backspace();
        t.Keys("J");
        Assert.Equal("VIỆT", t.Screen);
    }

    [Fact]
    public void BackspaceAfterRepeatedTransformUndoResumesConversion()
    {
        // "aaa" → "aa" (gõ lặp phím biến đổi). Xóa một 'a' rồi gõ lại phải ra "â".
        var t = new Typist().Keys("aaa");
        Assert.Equal("aa", t.Screen);

        t.Backspace();
        Assert.Equal("a", t.Screen);
        t.Keys("a");
        Assert.Equal("â", t.Screen);
    }

    [Fact]
    public void BackspaceAfterBreveUndoResumesConversion()
    {
        // "aww" → "aw". Xóa 'w' rồi gõ lại phải ra "ă".
        var t = new Typist().Keys("aww");
        Assert.Equal("aw", t.Screen);

        t.Backspace();
        Assert.Equal("a", t.Screen);
        t.Keys("w");
        Assert.Equal("ă", t.Screen);
    }

    [Fact]
    public void MethodsReportTransformKeys()
    {
        Assert.True(TelexMethod.Instance.IsTransformKey('s'));
        Assert.True(TelexMethod.Instance.IsTransformKey('W'));
        Assert.False(TelexMethod.Instance.IsTransformKey('b'));
        Assert.True(VniMethod.Instance.IsTransformKey('6'));
        Assert.False(VniMethod.Instance.IsTransformKey('s'));
        Assert.Equal("[pass]", EngineResult.PassThrough.ToString());
    }
}
