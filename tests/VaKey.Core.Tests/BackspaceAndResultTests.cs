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
