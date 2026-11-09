using System.Runtime.InteropServices;
using System.Text;
using VaKey.Core.Data;
using VaKey.Core.Settings;
using VaKey.Core.Validation;

namespace VaKey.Core.Engine;

/// <summary>
/// Trạng thái của từ đang gõ. Không có trạng thái toàn cục: mọi thứ nằm trong đối tượng này.
/// Không an toàn đa luồng; mỗi luồng hook dùng một phiên riêng.
/// </summary>
public sealed class InputSession
{
    private const int MaxWordLength = 48;

    private readonly List<VChar> _buf = new(MaxWordLength);
    private readonly List<VChar> _snapshot = new(MaxWordLength);
    private readonly StringBuilder _raw = new(MaxWordLength);
    private Tone _tone;
    private bool _disabled;
    private bool _afterUndo;
    private string _rendered = "";

    private EngineOptions _options = EngineOptions.Default;
    private ITypingMethod _method = TelexMethod.Instance;
    private HashSet<string>? _exceptions;

    public InputSession(EngineOptions? options = null)
    {
        if (options is not null) Options = options;
    }

    public EngineOptions Options
    {
        get => _options;
        set
        {
            _options = value ?? EngineOptions.Default;
            _method = TypingMethods.For(_options.Method);
            _exceptions = _options.EnglishExceptions.Count == 0
                ? null
                : new HashSet<string>(_options.EnglishExceptions.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()),
                    StringComparer.OrdinalIgnoreCase);
            Reset();
        }
    }

    public ITypingMethod Method => _method;

    /// <summary>Từ đang hiển thị trên màn hình (theo engine).</summary>
    public string CurrentWord => _rendered;

    /// <summary>Chuỗi phím gốc đã gõ cho từ hiện tại.</summary>
    public string RawKeys => _raw.ToString();

    /// <summary>true khi từ hiện tại đã bị tắt biến đổi (hoàn tác, sai chính tả, ngoại lệ).</summary>
    public bool IsWordLiteral => _disabled;

    /// <summary>Kết thúc từ: Space, Enter, đổi cửa sổ, click chuột...</summary>
    public void Reset()
    {
        _buf.Clear();
        _raw.Clear();
        _tone = Tone.None;
        _disabled = false;
        _afterUndo = false;
        _rendered = "";
    }

    public EngineResult Process(char key)
    {
        if (!_method.IsWordKey(key))
        {
            Reset();
            return EngineResult.PassThrough;
        }

        string old = _rendered;
        _raw.Append(key);

        if (_disabled || _buf.Count >= MaxWordLength)
        {
            _disabled = true;
            AppendLiteral(key);

            // "ss" → "s" là hợp lý cho "ass", nhưng "pass" + "w..." rõ ràng là tiếng Anh: trả về phím gốc.
            if (_afterUndo && _options.SpellCheck && !IsValid())
            {
                _afterUndo = false;
                if (!RenderEquals(_raw)) RevertToRaw();
            }
        }
        else
        {
            ApplyKey(key);
            if (_exceptions is not null && _exceptions.Contains(RawKeys))
            {
                RevertToRaw();
            }
        }

        _rendered = Render();
        return Diff(old, _rendered, key);
    }

    public EngineResult Backspace()
    {
        if (_buf.Count == 0) return EngineResult.PassThrough;

        string old = _rendered;
        _buf.RemoveAt(_buf.Count - 1);
        if (_buf.Count == 0)
        {
            Reset();
            return EngineResult.PassThrough;
        }

        if (!HasVowel()) _tone = Tone.None;

        // Chuỗi phím gốc không còn khớp chính xác; giữ lại chữ cái gốc để hoàn tác khi sai chính tả.
        _raw.Clear();
        foreach (var c in _buf) _raw.Append(c.Upper ? char.ToUpperInvariant(c.Base) : c.Base);

        _rendered = Render();
        if (_rendered.Length == old.Length - 1 && old.StartsWith(_rendered, StringComparison.Ordinal))
        {
            return EngineResult.PassThrough;
        }

        return MakeDiff(old, _rendered);
    }

    // ---------------------------------------------------------------- core logic

    private enum Outcome
    {
        NotApplicable,
        Applied,
        Undone,
    }

    private Span<VChar> Buffer => CollectionsMarshal.AsSpan(_buf);

    private void ApplyKey(char key)
    {
        var action = _method.GetAction(key);
        if (action.Kind != KeyActionKind.None)
        {
            _snapshot.Clear();
            _snapshot.AddRange(_buf);
            var savedTone = _tone;

            var outcome = TryTransform(action, key);
            if (outcome == Outcome.Undone) return;
            if (outcome == Outcome.Applied)
            {
                if (!_options.SpellCheck || IsValid()) return;

                // Biến đổi làm từ sai chính tả → coi phím là ký tự thường.
                _buf.Clear();
                _buf.AddRange(_snapshot);
                _tone = savedTone;
            }
        }

        AppendLiteral(key);
        FixUoHorn();

        if (_options.SpellCheck && !IsValid())
        {
            // Từ không thể là tiếng Việt: trả về đúng chuỗi phím đã gõ và ngừng biến đổi.
            if (!RenderEquals(_raw)) RevertToRaw();
            _disabled = true;
        }
    }

    private Outcome TryTransform(KeyAction action, char key)
    {
        var w = Buffer;
        var p = Syllable.Parse(w);

        switch (action.Kind)
        {
            case KeyActionKind.Tone:
                if (!p.HasVowel) return Outcome.NotApplicable;
                if (_tone == action.Tone)
                {
                    _tone = Tone.None;
                    return Undo(key);
                }

                _tone = action.Tone;
                return Outcome.Applied;

            case KeyActionKind.RemoveTone:
                if (_tone == Tone.None) return Outcome.NotApplicable;
                _tone = Tone.None;
                return Outcome.Applied;

            case KeyActionKind.Circumflex:
                return ApplyCircumflex(w, p, action.Target, key);

            case KeyActionKind.Breve:
            {
                int idx = LastVowelWithBase(w, p, 'a');
                return idx < 0 ? Outcome.NotApplicable : ToggleMark(idx, Mark.Breve, key);
            }

            case KeyActionKind.Horn:
                return ApplyHorn(w, p, key, allowBreve: false);

            case KeyActionKind.HornOrBreve:
                return ApplyTelexW(w, p, key);

            case KeyActionKind.Stroke:
                if (w.Length == 0 || w[0].Base != 'd') return Outcome.NotApplicable;
                return ToggleMark(0, Mark.Stroke, key);

            default:
                return Outcome.NotApplicable;
        }
    }

    private Outcome ApplyCircumflex(Span<VChar> w, SyllableParts p, char target, char key)
    {
        int idx;
        if (target != '\0')
        {
            idx = LastVowelWithBase(w, p, target);
        }
        else
        {
            int pair = FindUoPair(w, p);
            idx = pair >= 0 ? pair + 1 : LastVowelWithBase(w, p, 'a', 'e', 'o');
        }

        if (idx < 0) return Outcome.NotApplicable;
        if (w[idx].Mark == Mark.Circumflex) return UndoMark(idx, key);

        w[idx].Mark = Mark.Circumflex;
        // ươ + o → uô
        if (w[idx].Base == 'o' && idx - 1 >= p.VowelStart && w[idx - 1].Base == 'u' && w[idx - 1].Mark == Mark.Horn)
        {
            w[idx - 1].Mark = Mark.None;
        }

        return Outcome.Applied;
    }

    private Outcome ApplyTelexW(Span<VChar> w, SyllableParts p, char key)
    {
        // "ww" → "w": hoàn tác 'ư' sinh ra từ 'w' đứng riêng.
        if (w.Length > 0 && w[^1].FromW)
        {
            _buf[^1] = VChar.FromKey(key);
            _disabled = true;
            return Outcome.Undone;
        }

        if (!p.HasVowel)
        {
            if (!_options.WStandaloneToU) return Outcome.NotApplicable;
            _buf.Add(new VChar('u', Mark.Horn, char.IsUpper(key)) { FromW = true });
            return Outcome.Applied;
        }

        return ApplyHorn(w, p, key, allowBreve: true);
    }

    private Outcome ApplyHorn(Span<VChar> w, SyllableParts p, char key, bool allowBreve)
    {
        int pair = FindUoPair(w, p);
        if (pair >= 0)
        {
            ref var u = ref w[pair];
            ref var o = ref w[pair + 1];
            if (o.Mark == Mark.Horn)
            {
                u.Mark = Mark.None;
                o.Mark = Mark.None;
                return Undo(key);
            }

            // "uơ" khi đứng cuối từ (thuở, huơ); thành "ươ" khi có âm phía sau (xem FixUoHorn).
            bool atEnd = pair + 2 == w.Length;
            o.Mark = Mark.Horn;
            if (!atEnd || u.Mark == Mark.Horn) u.Mark = Mark.Horn;
            return Outcome.Applied;
        }

        int vs = p.VowelStart, ve = p.FinalStart;

        // "oa" + w → "oă" (hoặc, xoăn)
        if (allowBreve)
        {
            for (int k = vs; k + 1 < ve; k++)
            {
                if (w[k].Base == 'o' && w[k + 1].Base == 'a') return ToggleMark(k + 1, Mark.Breve, key);
            }
        }

        for (int k = vs; k < ve; k++)
        {
            if (w[k].Base is 'u' or 'o') return ToggleMark(k, Mark.Horn, key);
        }

        if (allowBreve)
        {
            int a = LastVowelWithBase(w, p, 'a');
            if (a >= 0) return ToggleMark(a, Mark.Breve, key);
        }

        return Outcome.NotApplicable;
    }

    /// <summary>"uơ" + âm cuối/nguyên âm → "ươ" (tuơn → tươn).</summary>
    private void FixUoHorn()
    {
        var w = Buffer;
        var p = Syllable.Parse(w);
        int pair = FindUoPair(w, p);
        if (pair >= 0 && pair + 2 < w.Length && w[pair].Mark == Mark.None && w[pair + 1].Mark == Mark.Horn)
        {
            w[pair].Mark = Mark.Horn;
        }
    }

    private Outcome ToggleMark(int idx, Mark mark, char key)
    {
        if (_buf[idx].Mark == mark) return UndoMark(idx, key);
        var c = _buf[idx];
        c.Mark = mark;
        _buf[idx] = c;
        return Outcome.Applied;
    }

    private Outcome UndoMark(int idx, char key)
    {
        var c = _buf[idx];
        c.Mark = Mark.None;
        _buf[idx] = c;
        return Undo(key);
    }

    /// <summary>Gõ lặp phím biến đổi: trả về ký tự gốc và ngừng biến đổi cho từ này.</summary>
    private Outcome Undo(char key)
    {
        AppendLiteral(key);
        _disabled = true;
        _afterUndo = true;
        return Outcome.Undone;
    }

    private static int FindUoPair(ReadOnlySpan<VChar> w, SyllableParts p)
    {
        for (int k = p.VowelStart; k + 1 < p.FinalStart; k++)
        {
            if (w[k].Base == 'u' && w[k + 1].Base == 'o') return k;
        }

        return -1;
    }

    private static int LastVowelWithBase(ReadOnlySpan<VChar> w, SyllableParts p, char b1, char b2 = '\0', char b3 = '\0')
    {
        for (int k = p.FinalStart - 1; k >= p.VowelStart; k--)
        {
            char b = w[k].Base;
            if (b == b1 || b == b2 || b == b3) return k;
        }

        return -1;
    }

    private void AppendLiteral(char key) => _buf.Add(VChar.FromKey(key));

    private void RevertToRaw()
    {
        _buf.Clear();
        for (int i = 0; i < _raw.Length; i++) _buf.Add(VChar.FromKey(_raw[i]));
        _tone = Tone.None;
        _disabled = true;
        _afterUndo = false;
    }

    private bool HasVowel()
    {
        foreach (var c in _buf)
        {
            if (c.IsVowel) return true;
        }

        return false;
    }

    private bool IsValid() => SyllableValidator.IsValidPrefix(Buffer, _tone);

    // ---------------------------------------------------------------- rendering

    private string Render()
    {
        var w = Buffer;
        if (w.Length == 0) return "";

        int tonePos = -1;
        if (_tone != Tone.None)
        {
            var p = Syllable.Parse(w);
            tonePos = ToneMarker.FindTonePosition(w, p, _options.ToneStyle);
        }

        Span<char> chars = stackalloc char[w.Length];
        for (int i = 0; i < w.Length; i++)
        {
            var c = w[i];
            chars[i] = VowelTable.Compose(c.Base, c.Mark, i == tonePos ? _tone : Tone.None, c.Upper);
        }

        return new string(chars);
    }

    private bool RenderEquals(StringBuilder s)
    {
        var w = Buffer;
        if (_tone != Tone.None || w.Length != s.Length) return false;
        for (int i = 0; i < w.Length; i++)
        {
            var c = w[i];
            if (VowelTable.Compose(c.Base, c.Mark, Tone.None, c.Upper) != s[i]) return false;
        }

        return true;
    }

    private static EngineResult Diff(string old, string now, char key)
    {
        if (now.Length == old.Length + 1 && now[^1] == key && now.AsSpan(0, old.Length).SequenceEqual(old))
        {
            return EngineResult.PassThrough;
        }

        return MakeDiff(old, now);
    }

    private static EngineResult MakeDiff(string old, string now)
    {
        int prefix = old.AsSpan().CommonPrefixLength(now);
        return new EngineResult
        {
            BackspaceCount = old.Length - prefix,
            TextToInsert = now[prefix..],
            SwallowKey = true,
        };
    }
}
