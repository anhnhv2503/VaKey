namespace VaKey.Core.Data;

/// <summary>Dấu phụ của một chữ cái: mũ (â ê ô), trăng (ă), móc (ơ ư), gạch (đ).</summary>
public enum Mark : byte
{
    None = 0,
    Circumflex,
    Breve,
    Horn,
    Stroke,
}

/// <summary>Thanh điệu. Giá trị số trùng với chỉ số trong bảng <see cref="VowelTable"/>.</summary>
public enum Tone : byte
{
    None = 0,
    Acute,  // sắc
    Grave,  // huyền
    Hook,   // hỏi
    Tilde,  // ngã
    Dot,    // nặng
}

/// <summary>Bảng nguyên âm tiếng Việt: mọi tổ hợp nguyên âm + dấu phụ + thanh điệu (Unicode NFC).</summary>
public static class VowelTable
{
    private static readonly Dictionary<char, (char Base, Mark Mark, Tone Tone, bool Upper)> Reverse = BuildReverse();

    public static bool IsVowel(char baseLower) =>
        baseLower is 'a' or 'e' or 'i' or 'o' or 'u' or 'y';

    public static bool CanTakeMark(char baseLower, Mark mark) => mark switch
    {
        Mark.None => true,
        Mark.Circumflex => baseLower is 'a' or 'e' or 'o',
        Mark.Breve => baseLower == 'a',
        Mark.Horn => baseLower is 'o' or 'u',
        Mark.Stroke => baseLower == 'd',
        _ => false,
    };

    /// <summary>Sáu dạng thanh điệu của một nguyên âm (chỉ số = <see cref="Tone"/>), hoặc null.</summary>
    private static string? Forms(char baseLower, Mark mark) => (baseLower, mark) switch
    {
        ('a', Mark.None) => "aáàảãạ",
        ('a', Mark.Breve) => "ăắằẳẵặ",
        ('a', Mark.Circumflex) => "âấầẩẫậ",
        ('e', Mark.None) => "eéèẻẽẹ",
        ('e', Mark.Circumflex) => "êếềểễệ",
        ('i', Mark.None) => "iíìỉĩị",
        ('o', Mark.None) => "oóòỏõọ",
        ('o', Mark.Circumflex) => "ôốồổỗộ",
        ('o', Mark.Horn) => "ơớờởỡợ",
        ('u', Mark.None) => "uúùủũụ",
        ('u', Mark.Horn) => "ưứừửữự",
        ('y', Mark.None) => "yýỳỷỹỵ",
        _ => null,
    };

    /// <summary>Ghép chữ cái gốc + dấu phụ + thanh điệu thành một ký tự dựng sẵn.</summary>
    public static char Compose(char baseLower, Mark mark, Tone tone, bool upper)
    {
        char c;
        if (baseLower == 'd' && mark == Mark.Stroke)
        {
            c = 'đ';
        }
        else
        {
            var forms = Forms(baseLower, mark);
            c = forms is null ? baseLower : forms[(int)tone];
        }

        return upper ? char.ToUpperInvariant(c) : c;
    }

    /// <summary>Tách một ký tự tiếng Việt dựng sẵn thành chữ gốc, dấu phụ, thanh điệu.</summary>
    public static bool TryDecompose(char c, out char baseLower, out Mark mark, out Tone tone, out bool upper)
    {
        if (Reverse.TryGetValue(c, out var v))
        {
            (baseLower, mark, tone, upper) = v;
            return true;
        }

        baseLower = c;
        mark = Mark.None;
        tone = Tone.None;
        upper = false;
        return false;
    }

    private static Dictionary<char, (char, Mark, Tone, bool)> BuildReverse()
    {
        var map = new Dictionary<char, (char, Mark, Tone, bool)>();
        foreach (var b in "aeiouy")
        {
            foreach (var m in new[] { Mark.None, Mark.Circumflex, Mark.Breve, Mark.Horn })
            {
                var forms = Forms(b, m);
                if (forms is null) continue;
                for (int t = 0; t < forms.Length; t++)
                {
                    map[forms[t]] = (b, m, (Tone)t, false);
                    map[char.ToUpperInvariant(forms[t])] = (b, m, (Tone)t, true);
                }
            }
        }

        map['đ'] = ('d', Mark.Stroke, Tone.None, false);
        map['Đ'] = ('d', Mark.Stroke, Tone.None, true);
        for (char c = 'a'; c <= 'z'; c++)
        {
            map.TryAdd(c, (c, Mark.None, Tone.None, false));
            map.TryAdd(char.ToUpperInvariant(c), (c, Mark.None, Tone.None, true));
        }

        return map;
    }
}
