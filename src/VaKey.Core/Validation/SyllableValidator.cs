using VaKey.Core.Data;

namespace VaKey.Core.Validation;

/// <summary>
/// Kiểm tra một từ đang gõ có thể là (tiền tố của) một âm tiết tiếng Việt hợp lệ hay không.
/// Chữ chưa có dấu phụ được coi là "có thể nhận dấu sau" (ví dụ "uo" khớp "uô"/"ươ").
/// </summary>
public static class SyllableValidator
{
    private const int MaxSyllableLength = 10;

    /// <summary>Danh sách vần (không thanh điệu).</summary>
    private static readonly string[] RhymeSource =
    [
        "a", "ac", "ach", "ai", "am", "an", "ang", "anh", "ao", "ap", "at", "au", "ay",
        "ăc", "ăm", "ăn", "ăng", "ăp", "ăt",
        "âc", "âm", "ân", "âng", "âp", "ât", "âu", "ây",
        "e", "ec", "em", "en", "eng", "eo", "ep", "et",
        "ê", "êch", "êm", "ên", "ênh", "êp", "êt", "êu",
        "i", "ia", "ich", "im", "in", "inh", "ip", "it", "iu",
        "iêc", "iêm", "iên", "iêng", "iêp", "iêt", "iêu",
        "o", "oa", "oac", "oach", "oai", "oam", "oan", "oang", "oanh", "oao", "oap", "oat", "oay",
        "oăc", "oăm", "oăn", "oăng", "oăt",
        "oc", "oe", "oen", "oeo", "oet", "oi", "om", "on", "ong", "ooc", "oong", "op", "ot",
        "ô", "ôc", "ôi", "ôm", "ôn", "ông", "ôp", "ôt",
        "ơ", "ơi", "ơm", "ơn", "ơp", "ơt",
        "u", "ua", "uân", "uâng", "uât", "uây", "uc", "uê", "uêch", "uênh", "ui", "um", "un", "ung", "up", "ut",
        "uơ", "uôc", "uôi", "uôm", "uôn", "uông", "uôt",
        "uy", "uya", "uych", "uyên", "uyêt", "uyn", "uynh", "uyp", "uyt", "uyu",
        "ư", "ưa", "ưc", "ưi", "ưm", "ưn", "ưng", "ưt", "ưu",
        "ươc", "ươi", "ươm", "ươn", "ương", "ươp", "ươt", "ươu",
        "y", "yêm", "yên", "yêng", "yêt", "yêu",
    ];

    private static readonly (char Base, Mark Mark)[][] Rhymes = RhymeSource.Select(ToPattern).ToArray();

    /// <summary>Danh sách vần hợp lệ (chỉ để tài liệu/test).</summary>
    public static IReadOnlyList<string> KnownRhymes => RhymeSource;

    public static bool IsValidInitial(ReadOnlySpan<char> s) => s switch
    {
        "" or "b" or "c" or "ch" or "d" or "đ" or "g" or "gh" or "gi" or "h" or "k" or "kh" or "l" or "m"
            or "n" or "ng" or "ngh" or "nh" or "p" or "ph" or "qu" or "r" or "s" or "t" or "th" or "tr"
            or "v" or "x" => true,
        _ => false,
    };

    private static bool IsInitialPrefix(ReadOnlySpan<char> s) => IsValidInitial(s) || s is "q";

    public static bool IsValidPrefix(ReadOnlySpan<VChar> w, Tone tone)
    {
        if (w.IsEmpty) return true;
        if (w.Length > MaxSyllableLength) return false;

        foreach (var c in w)
        {
            if (c.Base < 'a' || c.Base > 'z') return false;
        }

        var p = Syllable.Parse(w);

        Span<char> initial = stackalloc char[p.InitialLength];
        for (int k = 0; k < p.InitialLength; k++)
        {
            var c = w[k];
            if (c.Base == 'd' && c.Mark == Mark.Stroke) initial[k] = 'đ';
            else if (c.Mark != Mark.None) return false;
            else initial[k] = c.Base;
        }

        if (!p.HasVowel)
        {
            return tone == Tone.None && IsInitialPrefix(initial);
        }

        if (!IsValidInitial(initial)) return false;

        var rhyme = w[p.VowelStart..];

        // Ràng buộc chính tả giữa phụ âm đầu và nguyên âm đầu tiên.
        char first = rhyme[0].Base;
        bool frontVowel = first is 'i' or 'e' or 'y';
        switch (initial)
        {
            case "k" or "gh" or "ngh" when !frontVowel:
            case "c" or "ng" when frontVowel:
            case "g" when first == 'e':
                return false;
        }

        // Âm cuối tắc (c, ch, p, t) chỉ đi với thanh sắc hoặc nặng.
        if (tone is Tone.Grave or Tone.Hook or Tone.Tilde)
        {
            var final = w[p.FinalStart..];
            if (IsStopFinal(final)) return false;
        }

        if (MatchesRhymePrefix(rhyme, '\0')) return true;
        if (initial is "gi" && MatchesRhymePrefix(rhyme, 'i')) return true;   // gi + êng = giêng
        if (initial is "qu" && MatchesRhymePrefix(rhyme, 'u')) return true;   // qu + ynh = quỳnh
        return false;
    }

    private static bool IsStopFinal(ReadOnlySpan<VChar> final)
    {
        if (final.Length == 1) return final[0].Base is 'c' or 'p' or 't';
        return final.Length == 2 && final[0].Base == 'c' && final[1].Base == 'h';
    }

    private static bool MatchesRhymePrefix(ReadOnlySpan<VChar> rhyme, char lead)
    {
        int offset = lead == '\0' ? 0 : 1;
        foreach (var t in Rhymes)
        {
            if (t.Length - offset < rhyme.Length) continue;
            if (offset == 1 && (t[0].Base != lead || t[0].Mark != Mark.None)) continue;

            bool ok = true;
            for (int k = 0; k < rhyme.Length; k++)
            {
                var r = rhyme[k];
                var tc = t[k + offset];
                if (r.Base != tc.Base || (r.Mark != tc.Mark && r.Mark != Mark.None))
                {
                    ok = false;
                    break;
                }
            }

            if (ok) return true;
        }

        return false;
    }

    private static (char, Mark)[] ToPattern(string s)
    {
        var result = new (char, Mark)[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            VowelTable.TryDecompose(s[i], out var b, out var m, out _, out _);
            result[i] = (b, m);
        }

        return result;
    }
}
